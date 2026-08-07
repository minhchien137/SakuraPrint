using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;

namespace PrintApp.Services;

// ── Orchestrator cho "Universal Lookup" (/sakura/lookup) — nhận 1 mã quét, thử từng
// ISakuraLookupHandler theo độ ưu tiên (CanHandle=true trước, theo đúng thứ tự đăng ký DI), nhưng
// vẫn thử tiếp các handler còn lại nếu handler ưu tiên không tìm thấy dữ liệu thật trong DB — tránh
// bỏ sót khi format mã thực tế không khớp đúng như dự đoán (VD Carton Number không có format cố
// định, chỉ là mã ngoài hệ thống, dùng làm handler "catch-all" cuối cùng).
public class SakuraLookupService
{
    private readonly IReadOnlyList<ISakuraLookupHandler> _handlers;

    public SakuraLookupService(IEnumerable<ISakuraLookupHandler> handlers)
    {
        _handlers = handlers.ToList();
    }

    public async Task<SakuraLookupResponse?> ResolveAsync(string? rawCode)
    {
        string code = (rawCode ?? "").Trim();
        if (code.Length == 0) return null;

        foreach (var handler in _handlers.OrderByDescending(h => h.CanHandle(code) ? 1 : 0))
        {
            var result = await handler.ResolveAsync(code);
            if (result != null) return result;
        }
        return null;
    }
}

// ── Pallet ID / Pallet Number ────────────────────────────────────────────────────────────────
public class PalletLookupHandler : ISakuraLookupHandler
{
    private readonly AppDbContext _db;
    public PalletLookupHandler(AppDbContext db) => _db = db;

    public string TypeKey => "Pallet";

    // Pallet Number tự sinh luôn có prefix "P-" (xem SakuraService.BuildPalletLabelZplAsync).
    // Pallet ID do operator tự đặt (VD "PALLET-001") không có format cố định, nên chỉ dùng prefix
    // "P-" làm gợi ý ưu tiên — SakuraLookupService vẫn fallback thử handler này sau nếu cần.
    public bool CanHandle(string code) => code.StartsWith("P-", StringComparison.OrdinalIgnoreCase);

    public async Task<SakuraLookupResponse?> ResolveAsync(string code)
    {
        var rows = await _db.Database
            .SqlQuery<SakuraCartonTraceRow>($"EXEC dbo.sp_Sakura_Trace_Pallet @PalletCode = {code}")
            .ToListAsync();

        if (rows.Count == 0) return null;

        var first = rows[0];
        string? palletNumber = rows.Select(r => r.PalletNumber).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        var activeRows = rows.Where(r => !r.IsDeleted).ToList();
        int boxCount = activeRows.Count;
        int unitCount = activeRows.Sum(r => r.CountSerial);
        bool everReprinted = rows.Any(r => r.IsPalletReprint);
        DateTime? lastReprintAt = rows.Where(r => r.LastPalletReprintAt.HasValue)
            .Select(r => r.LastPalletReprintAt).OrderByDescending(x => x).FirstOrDefault();

        return new SakuraLookupResponse
        {
            TypeKey = TypeKey,
            TypeLabel = "Pallet",
            ScannedCode = code,
            Fields = new()
            {
                new("Pallet Number", palletNumber),
                new("Work Order", first.WorkOrder),
                new("Box Count (active)", boxCount.ToString()),
                new("Unit Count (active)", unitCount.ToString()),
                new("PO Number", first.PoNumber),
                new("Inbound Reference", first.InboundReference),
                new("Warehouse Reference", first.WarehouseReference),
                new("Delivery Address", first.DeliveryAddress),
                new("Pallet Reprinted?", everReprinted ? $"Yes ({rows.Max(r => r.PalletReprintCount)}x)" : "No"),
                new("Last Pallet Reprint At", lastReprintAt?.ToString("yyyy-MM-dd HH:mm")),
            },
            Sections = new()
            {
                new SakuraLookupSection
                {
                    Title = $"Cartons in this pallet ({rows.Count})",
                    Columns = new() { "Carton Number", "Work Order", "Color", "Condition", "Serial Count", "Scan Date", "Deleted?", "Reprinted?" },
                    Rows = rows.Select(r => new List<string?>
                    {
                        r.CartonNumber, r.WorkOrder, r.Color, r.Condition, r.CountSerial.ToString(),
                        r.ScanDate.ToString("yyyy-MM-dd HH:mm"), r.IsDeleted ? "Yes" : "No", r.IsReprint ? "Yes" : "No"
                    }).ToList()
                }
            }
        };
    }
}

// ── PO Number ─────────────────────────────────────────────────────────────────────────────────
public class PoNumberLookupHandler : ISakuraLookupHandler
{
    private readonly AppDbContext _db;
    public PoNumberLookupHandler(AppDbContext db) => _db = db;

    public string TypeKey => "PONumber";

    // PO Number là free text operator tự gõ lúc in tem Pallet (không có format cố định trong
    // PrintApp) — nhưng theo thực tế đang dùng toàn số (VD "72779673"), dùng làm gợi ý ưu tiên.
    public bool CanHandle(string code) => code.Length > 0 && code.All(char.IsDigit);

    public async Task<SakuraLookupResponse?> ResolveAsync(string code)
    {
        var rows = await _db.Database
            .SqlQuery<SakuraCartonTraceRow>($"EXEC dbo.sp_Sakura_Trace_PoNumber @PoNumber = {code}")
            .ToListAsync();

        if (rows.Count == 0) return null;

        var activeRows = rows.Where(r => !r.IsDeleted).ToList();

        // Inbound/Warehouse Reference + Delivery Address là snapshot RIÊNG theo từng lần in tem
        // Pallet (BuildPalletLabelZplAsync) — 1 PO có nhiều Pallet thì mỗi Pallet có thể mang giá
        // trị khác nhau, nên PHẢI giữ theo từng dòng Pallet, không được gộp chung 1 giá trị đại
        // diện cho cả PO (dễ hiểu nhầm là dữ liệu chung của PO).
        var pallets = rows.GroupBy(r => r.PalletNumber ?? r.PalletId ?? "(chưa gán Pallet)")
            .Select(g => new
            {
                PalletNumber = g.Key,
                WorkOrder = g.Select(x => x.WorkOrder).FirstOrDefault(),
                BoxCount = g.Count(r => !r.IsDeleted),
                UnitCount = g.Where(r => !r.IsDeleted).Sum(r => r.CountSerial),
                LastScan = g.Max(r => r.ScanDate),
                InboundReference = g.Select(x => x.InboundReference).FirstOrDefault(v => !string.IsNullOrEmpty(v)),
                WarehouseReference = g.Select(x => x.WarehouseReference).FirstOrDefault(v => !string.IsNullOrEmpty(v)),
                DeliveryAddress = g.Select(x => x.DeliveryAddress).FirstOrDefault(v => !string.IsNullOrEmpty(v))
            })
            .OrderBy(p => p.PalletNumber)
            .ToList();

        var workOrders = rows.Select(r => r.WorkOrder).Where(w => !string.IsNullOrEmpty(w)).Distinct().ToList();

        return new SakuraLookupResponse
        {
            TypeKey = TypeKey,
            TypeLabel = "PO Number",
            ScannedCode = code,
            Fields = new()
            {
                new("PO Number", code),
                new("Pallet Count", pallets.Count.ToString()),
                new("Carton Count (active)", activeRows.Count.ToString()),
                new("Unit Count (active)", activeRows.Sum(r => r.CountSerial).ToString()),
                new("Work Order(s)", string.Join(", ", workOrders)),
            },
            Sections = new()
            {
                new SakuraLookupSection
                {
                    Title = $"Pallets under this PO ({pallets.Count})",
                    Columns = new() { "Pallet Number", "Work Order", "Box Count", "Unit Count", "Last Scan", "Inbound Reference", "Warehouse Reference", "Delivery Address" },
                    Rows = pallets.Select(p => new List<string?>
                    {
                        p.PalletNumber, p.WorkOrder, p.BoxCount.ToString(), p.UnitCount.ToString(), p.LastScan.ToString("yyyy-MM-dd HH:mm"),
                        p.InboundReference, p.WarehouseReference, p.DeliveryAddress
                    }).ToList()
                },
                new SakuraLookupSection
                {
                    Title = $"All cartons under this PO ({rows.Count})",
                    Columns = new() { "Carton Number", "Pallet Number", "Work Order", "Color", "Condition", "Serial Count", "Scan Date", "Deleted?" },
                    Rows = rows.Select(r => new List<string?>
                    {
                        r.CartonNumber, r.PalletNumber, r.WorkOrder, r.Color, r.Condition, r.CountSerial.ToString(),
                        r.ScanDate.ToString("yyyy-MM-dd HH:mm"), r.IsDeleted ? "Yes" : "No"
                    }).ToList()
                }
            }
        };
    }
}

// ── Work Order ────────────────────────────────────────────────────────────────────────────────
public class WorkOrderLookupHandler : ISakuraLookupHandler
{
    private readonly AppDbContext _db;
    private readonly ViidooService _viidoo;
    public WorkOrderLookupHandler(AppDbContext db, ViidooService viidoo)
    {
        _db = db;
        _viidoo = viidoo;
    }

    public string TypeKey => "WorkOrder";

    // Work Order không có format cố định trong PrintApp (Odoo tự đặt tên WO) — không đoán được
    // qua prefix, để SakuraLookupService fallback thử handler này sau các handler có format rõ.
    public bool CanHandle(string code) => false;

    public async Task<SakuraLookupResponse?> ResolveAsync(string code)
    {
        // ── Odoo/Viidoo — best-effort, lỗi cookie/mạng không được chặn phần dữ liệu local.
        string? odooProductCode = null, odooColor = null, odooEan = null;
        decimal? odooQuantity = null;
        try
        {
            var odoo = await _viidoo.SearchAsync(code);
            if (odoo != null)
            {
                odooProductCode = odoo.ProductCode;
                odooColor = odoo.Color;
                odooQuantity = odoo.Quantity;
                if (odoo.ProductId is int pid)
                    odooEan = await _viidoo.GetProductEanAsync(pid);
            }
        }
        catch
        {
            // Best-effort — xem lý do ở SerialLookupHandler.
        }

        int printedQuantity = await _db.SnLabelPrints.AsNoTracking().CountAsync(x => x.WorkOrder == code);

        var cartonRows = await _db.CartonSnScanLogs.AsNoTracking()
            .Where(x => x.WorkOrder == code)
            .ToListAsync();

        var backPanelStatusCounts = await _db.BackPanelLaserLogs.AsNoTracking()
            .Where(x => x.WorkOrder == code)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, StringComparer.OrdinalIgnoreCase);

        var middleStatusCounts = await _db.MiddleLogs.AsNoTracking()
            .Where(x => x.WorkOrder == code)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, StringComparer.OrdinalIgnoreCase);

        bool foundAnywhere = odooProductCode != null || printedQuantity > 0 || cartonRows.Count > 0
            || backPanelStatusCounts.Count > 0 || middleStatusCounts.Count > 0;
        if (!foundAnywhere) return null;

        static string CountFor(Dictionary<string, int> counts, string status) =>
            counts.TryGetValue(status, out var n) ? n.ToString() : "0";

        int? remainingQuantity = odooQuantity.HasValue ? (int?)Math.Max(0, (int)odooQuantity.Value - printedQuantity) : null;

        var activeCartons = cartonRows.Where(x => !x.IsDeleted).ToList();
        var pallets = cartonRows.GroupBy(r => r.PalletNumber ?? r.PalletId)
            .Where(g => g.Key != null)
            .Select(g => new
            {
                PalletNumber = g.Key,
                BoxCount = g.Count(r => !r.IsDeleted),
                UnitCount = g.Where(r => !r.IsDeleted).Sum(r => r.CountSerial),
                LastScan = g.Max(r => r.ScanDate)
            })
            .OrderBy(p => p.PalletNumber)
            .ToList();

        return new SakuraLookupResponse
        {
            TypeKey = TypeKey,
            TypeLabel = "Work Order",
            ScannedCode = code,
            Fields = new()
            {
                new("Work Order", code),
                new("Product Code (Odoo)", odooProductCode),
                new("Product Color (Odoo)", odooColor),
                new("EAN (Odoo)", odooEan),
                new("WO Quantity (Odoo)", odooQuantity?.ToString()),
                new("Printed Quantity", printedQuantity.ToString()),
                new("Remaining Quantity", remainingQuantity?.ToString()),
                new("Carton Count (active)", activeCartons.Count.ToString()),
                new("Unit Count (active)", activeCartons.Sum(x => x.CountSerial).ToString()),
                new("Pallet Count", pallets.Count.ToString()),
            },
            Sections = new()
            {
                new SakuraLookupSection
                {
                    Title = "Station Pass / Fail Summary",
                    Columns = new() { "Station", "Pass", "Fail", "Pending" },
                    Rows = new()
                    {
                        new List<string?> { "Back Panel (Laser)", CountFor(backPanelStatusCounts, "PASS"), CountFor(backPanelStatusCounts, "FAIL"), CountFor(backPanelStatusCounts, "PENDING") },
                        new List<string?> { "Middle Panel (KQSX)", CountFor(middleStatusCounts, "PASS"), CountFor(middleStatusCounts, "FAIL"), CountFor(middleStatusCounts, "PENDING") },
                    }
                },
                new SakuraLookupSection
                {
                    Title = $"Pallets under this Work Order ({pallets.Count})",
                    Columns = new() { "Pallet Number", "Box Count", "Unit Count", "Last Scan" },
                    Rows = pallets.Select(p => new List<string?>
                    {
                        p.PalletNumber, p.BoxCount.ToString(), p.UnitCount.ToString(), p.LastScan.ToString("yyyy-MM-dd HH:mm")
                    }).ToList()
                }
            }
        };
    }
}

// ── Carton Number ────────────────────────────────────────────────────────────────────────────
public class CartonLookupHandler : ISakuraLookupHandler
{
    private readonly AppDbContext _db;
    public CartonLookupHandler(AppDbContext db) => _db = db;

    public string TypeKey => "Carton";

    // Carton Number là mã ngoài hệ thống (scan từ tem carton có sẵn), không có format cố định
    // trong PrintApp — dùng làm handler catch-all cuối cùng khi Pallet/Serial không khớp.
    public bool CanHandle(string code) => true;

    public async Task<SakuraLookupResponse?> ResolveAsync(string code)
    {
        var rows = await _db.Database
            .SqlQuery<SakuraCartonTraceRow>($"EXEC dbo.sp_Sakura_Trace_Carton @CartonNumber = {code}")
            .ToListAsync();

        var row = rows.FirstOrDefault();
        if (row == null) return null;

        var serials = row.Serial.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var unpackLogs = await _db.CartonUnpackLogs.AsNoTracking()
            .Where(x => x.CartonNumber == row.CartonNumber)
            .OrderByDescending(x => x.UnpackedAt)
            .ToListAsync();

        return new SakuraLookupResponse
        {
            TypeKey = TypeKey,
            TypeLabel = "Carton",
            ScannedCode = code,
            Fields = new()
            {
                new("Carton Number", row.CartonNumber),
                new("Work Order", row.WorkOrder),
                new("Color", row.Color),
                new("Condition", row.Condition),
                new("Serial Count", row.CountSerial.ToString()),
                new("Scan Date", row.ScanDate.ToString("yyyy-MM-dd HH:mm")),
                new("Pallet Number", row.PalletNumber),
                new("Deleted?", row.IsDeleted ? "Yes" : "No"),
                new("Reprinted?", row.IsReprint ? $"Yes ({row.ReprintCount}x, last {row.LastReprintAt:yyyy-MM-dd HH:mm})" : "No"),
                new("Rework Work Order", row.ReworkWorkOrder),
                new("Repack Count", row.RepackCount.ToString()),
                new("Last Repacked At", row.LastRepackedAt?.ToString("yyyy-MM-dd HH:mm")),
            },
            Sections = new()
            {
                new SakuraLookupSection
                {
                    Title = $"Serials in this carton ({serials.Count})",
                    Columns = new() { "Serial Number" },
                    Rows = serials.Select(s => new List<string?> { s }).ToList()
                },
                new SakuraLookupSection
                {
                    Title = "Unpack / Rework history",
                    Columns = new() { "Serial Number", "Status", "Rework WO", "Rework Type", "Unpacked At", "Reworked At", "Repacked At" },
                    Rows = unpackLogs.Select(u => new List<string?>
                    {
                        u.SerialNumber, u.Status, u.ReworkWorkOrder, u.ReworkType,
                        u.UnpackedAt.ToString("yyyy-MM-dd HH:mm"),
                        u.ReworkedAt?.ToString("yyyy-MM-dd HH:mm"),
                        u.RepackedAt?.ToString("yyyy-MM-dd HH:mm")
                    }).ToList()
                }
            }
        };
    }
}

// ── Serial Number ────────────────────────────────────────────────────────────────────────────
public class SerialLookupHandler : ISakuraLookupHandler
{
    private readonly AppDbContext _db;
    private readonly ViidooService _viidoo;
    public SerialLookupHandler(AppDbContext db, ViidooService viidoo)
    {
        _db = db;
        _viidoo = viidoo;
    }

    public string TypeKey => "Serial";

    // Serial luôn bắt đầu bằng SakuraService.Model ("RM15A") — xem SakuraService.BuildSerial.
    public bool CanHandle(string code) => code.StartsWith(SakuraService.Model, StringComparison.OrdinalIgnoreCase);

    public async Task<SakuraLookupResponse?> ResolveAsync(string code)
    {
        var rows = await _db.Database
            .SqlQuery<SakuraSerialTraceRow>($"EXEC dbo.sp_Sakura_Trace_Serial @SerialNumber = {code}")
            .ToListAsync();

        var row = rows.FirstOrDefault();
        if (row == null) return null;

        var scanLogs = await _db.SnLabelScanLogs.AsNoTracking()
            .Where(x => x.SerialNumber == code)
            .OrderBy(x => x.Timeline)
            .ToListAsync();

        var unpackLogs = await _db.CartonUnpackLogs.AsNoTracking()
            .Where(x => x.SerialNumber == code)
            .OrderBy(x => x.UnpackedAt)
            .ToListAsync();

        var backPanelLogs = await _db.BackPanelLaserLogs.AsNoTracking()
            .Where(x => x.SerialNumber == code)
            .OrderBy(x => x.Timeline)
            .ToListAsync();

        var middleLogs = await _db.MiddleLogs.AsNoTracking()
            .Where(x => x.SerialNumber == code)
            .OrderBy(x => x.Timeline)
            .ToListAsync();

        var dimensionResults = await _db.MiddleDimensionCheckResults.AsNoTracking()
            .Where(x => x.UNIT_SN == code)
            .OrderBy(x => x.DATE_TIME)
            .ToListAsync();

        var productionInputLogs = await _db.ProductionInputLogs.AsNoTracking()
            .Where(x => x.SerialCode == code)
            .OrderBy(x => x.DateFinished)
            .ToListAsync();

        string cartonDeletedField = row.CartonNumber == null
            ? "N/A — chưa thuộc carton nào"
            : (row.CartonIsDeleted == true ? "Yes" : "No");

        // ── Enrich với thông tin sản phẩm từ Odoo/Viidoo — Serial không tồn tại trong Odoo,
        // chỉ Work Order mới có, nên phải tra ngược qua PrintWorkOrder. Best-effort: lỗi Odoo
        // (cookie hết hạn, network...) không được làm hỏng cả kết quả lookup, chỉ để trống phần này.
        string? odooProductCode = null, odooColor = null, odooEan = null;
        decimal? odooQuantity = null;
        if (!string.IsNullOrWhiteSpace(row.PrintWorkOrder))
        {
            try
            {
                var odoo = await _viidoo.SearchAsync(row.PrintWorkOrder);
                if (odoo != null)
                {
                    odooProductCode = odoo.ProductCode;
                    odooColor = odoo.Color;
                    odooQuantity = odoo.Quantity;
                    if (odoo.ProductId is int pid)
                        odooEan = await _viidoo.GetProductEanAsync(pid);
                }
            }
            catch
            {
                // Best-effort — Odoo có thể chưa cấu hình cookie hoặc đang lỗi mạng, không chặn
                // phần còn lại của kết quả lookup vì lỗi này.
            }
        }

        // ── Gộp mọi trạm serial này đã đi qua thành 1 timeline duy nhất, sắp theo thời gian —
        // đúng ý "đường đi" (đang ở bước nào, đã qua trạm nào) thay vì tách rời từng bảng.
        var journey = new List<(DateTime Time, string Station, string Status, string? Detail)>();

        foreach (var s in scanLogs)
            journey.Add((s.Timeline, "SN Label Print", s.Status,
                s.FailedStep.HasValue ? $"Failed step {s.FailedStep}" : s.Ean));

        foreach (var b in backPanelLogs)
            journey.Add((b.Timeline, "Back Panel (Laser)", b.Status, b.FailReason ?? (b.FailedStep.HasValue ? $"Failed step {b.FailedStep}" : null)));

        foreach (var m in middleLogs)
            journey.Add((m.Timeline, "Middle Panel (Enter KQSX)", m.Status, m.FailReason ?? (m.FailedStep.HasValue ? $"Failed step {m.FailedStep}" : null)));

        foreach (var d in dimensionResults)
            journey.Add((d.DATE_TIME, "Middle Panel (Magnet Dimension Test)", d.STATUS ?? "", null));

        foreach (var p in productionInputLogs.Where(p => p.DateFinished.HasValue))
            journey.Add((p.DateFinished!.Value, "Production Result Entered (MES)", "ENTERED", $"WO {p.MasterWoCode}, Qty {p.ProductQty}"));

        if (row.CartonNumber != null && row.CartonScanDate.HasValue)
            journey.Add((row.CartonScanDate.Value, "Packed into Carton", "DONE", $"Carton {row.CartonNumber}"));

        foreach (var u in unpackLogs)
        {
            journey.Add((u.UnpackedAt, "Rework — Unpack", "UNPACKED", $"Carton {u.CartonNumber}"));
            if (u.ReworkedAt.HasValue)
                journey.Add((u.ReworkedAt.Value, "Rework — Reprint", "REWORKED", u.ReworkWorkOrder));
            if (u.RepackedAt.HasValue)
                journey.Add((u.RepackedAt.Value, "Rework — Repack", "REPACKED", $"Carton {u.CartonNumber}"));
        }

        var journeyRows = journey.OrderBy(j => j.Time)
            .Select(j => new List<string?> { j.Time.ToString("yyyy-MM-dd HH:mm"), j.Station, j.Status, j.Detail })
            .ToList();

        return new SakuraLookupResponse
        {
            TypeKey = TypeKey,
            TypeLabel = "Serial",
            ScannedCode = code,
            Fields = new()
            {
                new("Serial Number", row.SerialNumber),
                new("Model / Variant", $"{row.Model} {row.Variant}".Trim()),
                new("Color", row.PrintColor),
                new("Production Line", row.ProductionLine),
                new("Production Date", row.ProductionDate?.ToString("yyyy-MM-dd")),
                new("Work Order", row.PrintWorkOrder),
                new("Printed At", row.PrintedAt?.ToString("yyyy-MM-dd HH:mm")),
                new("Printed By", row.PrintedBy),
                new("Status", row.Status),
                new("EAN", row.Ean),
                new("Reprinted?", (row.ReprintCount ?? 0) > 0 ? $"Yes ({row.ReprintCount}x, last {row.LastReprintedAt:yyyy-MM-dd HH:mm})" : "No"),
                new("Rework Work Order", row.ReworkWorkOrder),
                new("Carton Number", row.CartonNumber),
                new("Carton Work Order", row.CartonWorkOrder),
                new("Pallet ID", row.PalletId),
                new("Pallet Number", row.PalletNumber),
                new("Carton Deleted?", cartonDeletedField),
                new("Product Code (Odoo)", odooProductCode),
                new("Product Color (Odoo)", odooColor),
                new("WO Quantity (Odoo)", odooQuantity?.ToString()),
                new("EAN (Odoo)", odooEan),
            },
            Sections = new()
            {
                new SakuraLookupSection
                {
                    Title = $"Production Journey ({journeyRows.Count} events)",
                    Columns = new() { "Time", "Station", "Status", "Detail" },
                    Rows = journeyRows
                }
            }
        };
    }
}
