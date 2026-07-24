using System.Data;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;

namespace PrintApp.Services;

// Service chung cho các chức năng thuộc dự án Sakura.
// SN Label Print là chức năng đầu tiên — các chức năng Sakura khác sẽ được thêm vào đây.
public class SakuraService
{
    private readonly AppDbContext _context;

    // Model hiện tại luôn là RM15A — để hằng số cho dễ đổi sau này.
    public const string Model = "RM15A";

    // Giới hạn số lượng in mỗi lần. Nhập thủ công giữ mức thấp để tránh gõ nhầm;
    // in theo Work Order tin tưởng số liệu từ Odoo nên cho phép cao hơn.
    public const int ManualMaxQuantity = 500;
    public const int WorkOrderMaxQuantity = 5000;

    // Số sản phẩm/carton theo nghiệp vụ (khác với ZplTemplates.CartonSnPlaceholderCount — số ô
    // SN vật lý trên tem — dù hiện tại 2 số này trùng nhau = 10). Dùng để tính carton hiện tại
    // là đủ hộp (đúng bằng số này) hay lẻ hộp (phần dư còn lại của Work Order < số này).
    public const int CartonPcsPerCarton = 10;

    private const string Base34Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // không có O, I
    private const int Base34 = 34;
    public const int MaxRunningNumberInt = Base34 * Base34 * Base34 - 1; // "ZZZ" = 39303

    public static readonly (string Variant, string Color)[] Variants =
    {
        ("00", "Blue"),
        ("01", "Pink"),
        ("02", "Green"),
    };

    private static readonly Dictionary<string, string> VariantColorMap =
        Variants.ToDictionary(v => v.Variant, v => v.Color);

    public SakuraService(AppDbContext context)
    {
        _context = context;
    }

    // ── Base34 helpers ────────────────────────────────────────────────────────

    public static string Base34Encode(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Giá trị phải >= 0.");

        if (value == 0) return "0";

        var sb = new StringBuilder();
        while (value > 0)
        {
            sb.Insert(0, Base34Alphabet[value % Base34]);
            value /= Base34;
        }
        return sb.ToString();
    }

    public static int Base34Decode(string s)
    {
        if (string.IsNullOrEmpty(s))
            throw new ArgumentException("Chuỗi base34 rỗng.", nameof(s));

        int result = 0;
        foreach (char raw in s)
        {
            char c = char.ToUpperInvariant(raw);
            int idx = Base34Alphabet.IndexOf(c);
            if (idx < 0)
                throw new ArgumentException($"Ký tự không hợp lệ trong chuỗi base34: '{raw}'.", nameof(s));
            result = result * Base34 + idx;
        }
        return result;
    }

    public static string ResolveColor(string variant) =>
        VariantColorMap.TryGetValue(variant, out var color) ? color
            : throw new SakuraValidationException("common.invalidVariant", $"Variant không hợp lệ: {variant}", new { variant });

    // Chấp nhận cả tên màu (không phân biệt hoa/thường) lẫn mã variant ("00"/"01"/"02")
    // vì chưa biết API Work Order thật sẽ trả về dạng nào.
    public static string? TryResolveVariantFromColor(string colorOrVariant)
    {
        if (string.IsNullOrWhiteSpace(colorOrVariant)) return null;
        string s = colorOrVariant.Trim();

        var byVariant = Variants.FirstOrDefault(v => string.Equals(v.Variant, s, StringComparison.OrdinalIgnoreCase));
        if (byVariant.Variant != null) return byVariant.Variant;

        var byColor = Variants.FirstOrDefault(v => string.Equals(v.Color, s, StringComparison.OrdinalIgnoreCase));
        return byColor.Variant;
    }

    // Trích màu từ mã variant (2 ký tự ngay sau model "RM15A") của 1 serial đã in/laser.
    // Dùng ở trạm Laser (Back Panel) để đối chiếu màu SN vừa quét với màu Work Order.
    // Trả về null nếu serial không đúng định dạng (sai model, quá ngắn, variant lạ).
    public static string? TryResolveColorFromSerial(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        string s = serial.Trim().ToUpperInvariant();
        // Serial luôn đúng 15 ký tự: Model(5) + variant(2) + year(1) + day(3) + line(1) + running(3).
        if (s.Length != 15 || !s.StartsWith(Model)) return null;

        string variant = s.Substring(Model.Length, 2);
        return VariantColorMap.TryGetValue(variant, out var color) ? color : null;
    }

    // SN quét vào trạm Middle bị cắt mất 4 ký tự đầu "RM15" so với serial đầy đủ ở trạm
    // Laser/Back Panel — chỉ còn "A" + variant(2) + year(1) + day(3) + line(1) + running(3)
    // = 11 ký tự. Vị trí variant (ngay sau "A") không đổi, chỉ khác độ dài/model prefix.
    private const string MiddleModel = "A";

    public static string? TryResolveColorFromMiddleSerial(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        string s = serial.Trim().ToUpperInvariant();
        if (s.Length != 11 || !s.StartsWith(MiddleModel)) return null;

        string variant = s.Substring(MiddleModel.Length, 2);
        return VariantColorMap.TryGetValue(variant, out var color) ? color : null;
    }

    // Tách cấu trúc 1 serial SnLabel (Model+variant+year+day+line+running) mà KHÔNG đòi hỏi
    // variant phải là màu hợp lệ (khác TryResolveColorFromSerial) — dùng ở bước verify-serial
    // (Check EAN -> Check Color & Serial Number) để vẫn lấy được Variant/Line/RunningNumber
    // ghi vào lịch sử SM_SNLabelPrint ngay cả khi bước Check Color bị FAIL (màu không khớp).
    // Trả về false nếu serial sai độ dài/model — khi đó không có gì để ghi lại.
    public static bool TryParseSerialParts(string serial, out string variant, out string line, out string runningNumber, out int runningNumberInt)
    {
        variant = ""; line = ""; runningNumber = ""; runningNumberInt = 0;
        if (string.IsNullOrWhiteSpace(serial)) return false;

        string s = serial.Trim().ToUpperInvariant();
        if (s.Length != 15 || !s.StartsWith(Model)) return false;

        variant = s.Substring(5, 2);
        line = s.Substring(11, 1);
        runningNumber = s.Substring(12, 3);
        try
        {
            runningNumberInt = Base34Decode(runningNumber);
        }
        catch (ArgumentException)
        {
            return false;
        }
        return true;
    }

    // ── Serial number formatting ─────────────────────────────────────────────

    public static string BuildSerial(string variant, DateTime productionDate, string line, string runningNumber)
    {
        char yearChar = (char)('0' + (productionDate.Year % 10));
        string day = productionDate.DayOfYear.ToString("D3");
        return $"{Model}{variant}{yearChar}{day}{line}{runningNumber}";
    }

    private static string FormatRunning(int runningInt) => Base34Encode(runningInt).PadLeft(3, '0');

    // ── SN Label generation (concurrency-safe) ───────────────────────────────

    public async Task<List<SnLabelPrint>> GenerateNextSerialsAsync(
        DateTime date, string variant, string line, int quantity, string? printedBy,
        string? workOrder = null, int? workOrderTotalQuantity = null)
    {
        int maxQuantity = string.IsNullOrWhiteSpace(workOrder) ? ManualMaxQuantity : WorkOrderMaxQuantity;
        if (quantity < 1 || quantity > maxQuantity)
            throw new SakuraValidationException("print.invalidQuantity", $"Số lượng phải từ 1 đến {maxQuantity}.", new { max = maxQuantity });
        if (line != "0" && line != "1")
            throw new SakuraValidationException("print.invalidLine", "Line không hợp lệ (chỉ 0 hoặc 1).");

        string color = ResolveColor(variant); // throws if invalid
        DateTime prodDate = date.Date;

        const int maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Khóa ngày sản xuất theo lần in ĐẦU TIÊN của Work Order này — bất kể ngày
                // hiện tại trên form là gì, để toàn bộ nhãn cùng 1 WO luôn dùng chung 1 ngày
                // (vd in 2000/3000 ngày 8/7, hôm sau in tiếp 1000 còn lại vẫn phải ra ngày 8/7).
                if (!string.IsNullOrWhiteSpace(workOrder))
                {
                    var existingDate = await _context.SnLabelPrints
                        .Where(x => x.WorkOrder == workOrder)
                        .OrderBy(x => x.PrintedAt)
                        .Select(x => (DateTime?)x.ProductionDate)
                        .FirstOrDefaultAsync();
                    if (existingDate is DateTime lockedDate)
                        prodDate = lockedDate;
                }

                // Re-check "còn lại bao nhiêu" ngay trong transaction — phòng trường hợp
                // người khác đã in thêm cho cùng Work Order này sau khi lookup nhưng
                // trước khi bấm PRINT (vd 2 máy cùng thao tác 1 WO).
                if (!string.IsNullOrWhiteSpace(workOrder) && workOrderTotalQuantity is int totalQty)
                {
                    int alreadyPrinted = await _context.SnLabelPrints
                        .Where(x => x.WorkOrder == workOrder && (x.Status == null || x.Status == "PASS"))
                        .CountAsync();
                    int woRemaining = totalQty - alreadyPrinted;
                    if (quantity > woRemaining)
                    {
                        int clampedRemaining = Math.Max(0, woRemaining);
                        throw new SakuraConflictException(
                            "print.workOrderQuantityExceeded",
                            $"Work Order '{workOrder}' chỉ còn {clampedRemaining} trên tổng {totalQty} có thể in (đã in {alreadyPrinted}).",
                            new { wo = workOrder, remaining = clampedRemaining, total = totalQty, printed = alreadyPrinted });
                    }
                }

                int lastRunning = await _context.SnLabelPrints
                    .Where(x => x.ProductionDate == prodDate && x.ProductionLine == line && x.Variant == variant)
                    .Select(x => (int?)x.RunningNumberInt)
                    .MaxAsync() ?? -1;

                int startRunning = lastRunning + 1;
                if (startRunning + quantity - 1 > MaxRunningNumberInt)
                {
                    int remaining = Math.Max(0, MaxRunningNumberInt - startRunning + 1);
                    throw new SakuraConflictException(
                        "print.serialCapacityExceeded",
                        $"Không thể sinh serial: số thứ tự sẽ vượt quá ZZZ ({MaxRunningNumberInt}). " +
                        $"Chỉ còn {remaining} serial khả dụng cho {color} / Line {line} / {prodDate:yyyy-MM-dd}.",
                        new { max = MaxRunningNumberInt, remaining, color, line, date = prodDate.ToString("yyyy-MM-dd") });
                }

                var batchId = Guid.NewGuid();
                var printedAt = VietnamNow();
                var rows = new List<SnLabelPrint>(quantity);

                for (int i = 0; i < quantity; i++)
                {
                    int runningInt = startRunning + i;
                    string runningStr = FormatRunning(runningInt);
                    string serial = BuildSerial(variant, prodDate, line, runningStr);

                    rows.Add(new SnLabelPrint
                    {
                        SerialNumber = serial,
                        Model = Model,
                        Variant = variant,
                        Color = color,
                        ProductionLine = line,
                        ProductionDate = prodDate,
                        RunningNumber = runningStr,
                        RunningNumberInt = runningInt,
                        PrintedAt = printedAt,
                        PrintedBy = printedBy,
                        BatchId = batchId,
                        WorkOrder = workOrder
                    });
                }

                _context.SnLabelPrints.AddRange(rows);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return rows;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync();
                // Va chạm với 1 request khác đang sinh cùng lúc — thử lại với MAX mới.
                continue;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        throw new SakuraConflictException("print.concurrencyFailed", "Không thể sinh serial do tranh chấp đồng thời quá nhiều lần, vui lòng thử lại.");
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);

    // Tổng số label đã in THÀNH CÔNG (Status = PASS, hoặc NULL = dòng cũ từ flow tự sinh
    // serial — luôn coi là đã in xong) cho 1 Work Order — không phân biệt ngày/line, cộng dồn
    // qua nhiều lượt in khác nhau. Dòng FAIL/PENDING (chưa qua hết Check EAN -> Check Color &
    // Serial Number -> Print Label) KHÔNG được tính, vì chưa thực sự in ra được cái nào.
    // Dùng để tính "còn lại bao nhiêu" khi lookup.
    public async Task<int> GetWorkOrderPrintedCountAsync(string workOrder)
    {
        return await _context.SnLabelPrints
            .AsNoTracking()
            .CountAsync(x => x.WorkOrder == workOrder && (x.Status == null || x.Status == "PASS"));
    }

    // ── Carton SN Label — Work Order tracking (SM_Sakura_CartonLabel_Data) ──────────

    // Tổng số serial đã in thành công cho Work Order này (cộng dồn qua mọi carton) — dùng để
    // tính remaining + carton hiện tại đủ hộp hay lẻ hộp (xem CartonWorkOrderLookupResponse).
    // 1 dòng = 1 carton (không phải 1 dòng/serial nữa) nên phải SUM CountSerial, không đếm dòng.
    public async Task<int> GetCartonWorkOrderPrintedCountAsync(string workOrder)
    {
        return await _context.CartonSnScanLogs
            .AsNoTracking()
            .Where(x => x.WorkOrder == workOrder)
            .SumAsync(x => (int?)x.CountSerial) ?? 0;
    }

    // Serial giờ lưu dạng CSV nhiều serial/carton trong 1 dòng (VD "SN1,SN2,SN3") — so khớp theo
    // TOKEN đầy đủ (không phải substring) bằng cách bọc dấu phẩy 2 đầu cả cột lẫn giá trị cần
    // tìm, để "RM15A...01" không bị match nhầm bởi "RM15A...010".
    public async Task<bool> IsCartonSerialAlreadyUsedAsync(string serial)
    {
        string needle = "," + serial + ",";
        return await _context.CartonSnScanLogs
            .AsNoTracking()
            .AnyAsync(x => ("," + x.Serial + ",").Contains(needle));
    }

    // Carton Number không được trùng — mỗi carton (thùng) là 1 định danh vật lý riêng, đã in
    // rồi thì không cho nhập lại. Check toàn bộ bảng (không giới hạn theo Work Order).
    public async Task<bool> IsCartonNumberAlreadyUsedAsync(string cartonNumber)
    {
        return await _context.CartonSnScanLogs.AsNoTracking().AnyAsync(x => x.CartonNumber == cartonNumber);
    }

    // 3 điều kiện check khi quét 1 SN vào carton: (1) định dạng — dùng lại TryParseSerialParts
    // (cùng logic serial RM15A đang dùng ở SnLabel), (2) trùng trong lần quét hiện tại — client tự
    // check bằng string/array, không cần tới server, (3) đã được quét/in ở carton khác chưa —
    // check tại đây (đã in thành công không thể chọn lại, kể cả khác Work Order).
    public async Task<(bool Ok, string? ErrorCode, string? ErrorMessage)> ValidateCartonSerialAsync(string serial)
    {
        if (!TryParseSerialParts(serial, out _, out _, out _, out _))
            return (false, "cartonLabel.invalidSerialFormat", $"Serial '{serial}' không đúng định dạng.");

        if (await IsCartonSerialAlreadyUsedAsync(serial))
            return (false, "cartonLabel.serialAlreadyUsed", $"Serial '{serial}' đã được quét/in trước đó.");

        return (true, null, null);
    }

    // Gọi SAU KHI ZPL coi như đã "in xong" (bridge thành công, hoặc Preview theo yêu cầu test
    // hiện tại) — lưu 1 DÒNG DUY NHẤT cho cả carton: Serial là toàn bộ serial không rỗng nối
    // bằng dấu phẩy, CountSerial là số lượng serial trong chuỗi đó (10, hoặc phần dư nếu lẻ hộp).
    //
    // Nếu palletId được truyền vào (Pallet ID đang nhập trên UI lúc in carton này), gom thẳng
    // carton vào pallet đó luôn — không cần thao tác "Manage" thủ công như ScanCartonIntoPalletAsync.
    // Carton LUÔN được lưu kèm Pallet Number ngay lúc này (không đợi tới lúc bấm "Print Pallet
    // Label"): nếu pallet đã có số rồi (đã từng "chốt"/in tem trước đó) thì dùng lại đúng số đó;
    // nếu đây là carton ĐẦU TIÊN của 1 Pallet ID hoàn toàn mới thì sinh số mới luôn (dùng chung
    // GenerateAndAssignPalletNumberAsync — concurrency-safe, giống lúc "chốt" pallet). "Print
    // Pallet Label" sau này chỉ còn việc build ZPL từ số đã có sẵn.
    // Carton đã in thật rồi nên KHÔNG được throw ở đây: nếu pallet đang gom màu khác thì bỏ qua
    // việc gán PalletId/PalletNumber (vẫn lưu carton bình thường) và trả về cảnh báo cho caller.
    public async Task<string?> RecordCartonScanAsync(string workOrder, string cartonNumber, string color, string condition, IReadOnlyList<string> orderedSlots, string? palletId)
    {
        var nonEmpty = orderedSlots.Select(s => (s ?? "").Trim()).Where(s => s.Length > 0).ToList();
        if (nonEmpty.Count == 0) return null;

        string? trimmedPalletId = string.IsNullOrWhiteSpace(palletId) ? null : palletId.Trim();
        string? attachWarning = null;
        if (trimmedPalletId != null)
        {
            // 1 Pallet ID chỉ được chứa carton của ĐÚNG 1 Work Order (Work Order đi liền với PO
            // Number — gộp 2 Work Order vào chung 1 pallet sẽ làm sai lệch PO Number trên tem
            // Pallet Label) — kiểm tra y hệt cách đang chặn khác màu bên dưới.
            var existing = await _context.CartonSnScanLogs
                .Where(x => x.PalletId == trimmedPalletId)
                .Select(x => new { x.Color, x.WorkOrder })
                .FirstOrDefaultAsync();
            if (existing != null && !string.Equals(existing.WorkOrder, workOrder, StringComparison.OrdinalIgnoreCase))
            {
                attachWarning = $"Carton '{cartonNumber}' đã lưu nhưng KHÔNG được gom vào Pallet '{trimmedPalletId}' vì khác Work Order ({workOrder} so với {existing.WorkOrder}).";
                trimmedPalletId = null;
            }
            else if (existing != null && !string.Equals(existing.Color, color, StringComparison.OrdinalIgnoreCase))
            {
                attachWarning = $"Carton '{cartonNumber}' đã lưu nhưng KHÔNG được gom vào Pallet '{trimmedPalletId}' vì khác màu ({color} so với {existing.Color}).";
                trimmedPalletId = null;
            }
        }

        var scanDate = VietnamNow();
        var row = new CartonSnScanLog
        {
            Serial = string.Join(",", nonEmpty),
            ScanDate = scanDate,
            CountSerial = nonEmpty.Count,
            WorkOrder = workOrder,
            CartonNumber = cartonNumber,
            Color = color,
            Condition = condition,
            Date = int.Parse(scanDate.ToString("yyyyMMdd")),
            PalletId = trimmedPalletId
        };
        _context.CartonSnScanLogs.Add(row);

        if (trimmedPalletId == null)
        {
            await _context.SaveChangesAsync();
            return attachWarning;
        }

        string? existingNumber = await _context.CartonSnScanLogs
            .Where(x => x.PalletId == trimmedPalletId && x.PalletNumber != null)
            .Select(x => x.PalletNumber)
            .FirstOrDefaultAsync();

        if (existingNumber != null)
        {
            row.PalletNumber = existingNumber;
            await _context.SaveChangesAsync();
        }
        else
        {
            // Carton đầu tiên của Pallet ID này -> sinh Pallet Number ngay, cùng transaction với
            // việc insert carton (GenerateAndAssignPalletNumberAsync tự SaveChanges + commit).
            await GenerateAndAssignPalletNumberAsync(new List<CartonSnScanLog> { row });
        }

        return attachWarning;
    }

    // ── Print Pallet (Main Pallet Label) — gom nhiều carton đã in (SM_Sakura_CartonLabel_Data)
    // vào 1 Pallet ID do người vận hành tự đặt (vd "PALLET-001"), đếm số thùng/unit realtime,
    // sinh Pallet Number tự động lúc "chốt"/in tem. ──────────────────────────────────────────

    // Danh sách carton hiện đang thuộc 1 Pallet ID + tổng số thùng/unit — dùng cho cả badge
    // realtime lẫn bảng trong modal "Quản lý Pallet".
    public async Task<(int BoxCount, int UnitCount, List<CartonSnScanLog> Boxes)> GetPalletBoxesAsync(string palletId)
    {
        if (string.IsNullOrWhiteSpace(palletId))
            return (0, 0, new List<CartonSnScanLog>());

        var boxes = await _context.CartonSnScanLogs
            .AsNoTracking()
            .Where(x => x.PalletId == palletId)
            .OrderBy(x => x.ScanDate)
            .ToListAsync();

        return (boxes.Count, boxes.Sum(x => x.CountSerial), boxes);
    }

    // Quét/thêm 1 Carton Number (đã in trước đó) vào 1 Pallet ID. Tự chặn: carton không tồn
    // tại, carton đã thuộc pallet khác, carton khác màu HOẶC khác Work Order với pallet đang gom
    // (1 Pallet ID chỉ được chứa carton của đúng 1 Work Order — Work Order đi liền với PO Number).
    // Idempotent nếu carton đã thuộc đúng pallet này rồi (bấm/quét lại không báo lỗi).
    public async Task<(int BoxCount, int UnitCount, List<CartonSnScanLog> Boxes)> ScanCartonIntoPalletAsync(string palletId, string cartonNumber, string color)
    {
        if (string.IsNullOrWhiteSpace(palletId))
            throw new SakuraValidationException("cartonLabel.pallet.palletIdMissing", "Thiếu Pallet ID.");
        if (string.IsNullOrWhiteSpace(cartonNumber))
            throw new SakuraValidationException("cartonLabel.cartonNumberMissing", "Thiếu Carton Number.");

        string trimmedCarton = cartonNumber.Trim();
        var row = await _context.CartonSnScanLogs.FirstOrDefaultAsync(x => x.CartonNumber == trimmedCarton);
        if (row == null)
            throw new SakuraValidationException("cartonLabel.pallet.cartonNotFound", $"Không tìm thấy Carton Number '{trimmedCarton}' đã được in trước đó.", new { cartonNumber = trimmedCarton });

        if (!string.IsNullOrEmpty(row.PalletId) && row.PalletId != palletId)
            throw new SakuraConflictException("cartonLabel.pallet.cartonInAnotherPallet", $"Carton Number '{trimmedCarton}' đã thuộc Pallet ID '{row.PalletId}'.", new { cartonNumber = trimmedCarton, palletId = row.PalletId });

        if (!string.Equals(row.Color ?? "", color ?? "", StringComparison.OrdinalIgnoreCase))
            throw new SakuraValidationException("cartonLabel.pallet.colorMismatch", $"Carton Number '{trimmedCarton}' khác màu ({row.Color}) với Pallet đang gom ({color}).", new { cartonNumber = trimmedCarton, cartonColor = row.Color, palletColor = color });

        string? existingWorkOrder = await _context.CartonSnScanLogs
            .Where(x => x.PalletId == palletId && x.CartonNumber != trimmedCarton)
            .Select(x => x.WorkOrder)
            .FirstOrDefaultAsync();
        if (existingWorkOrder != null && !string.Equals(existingWorkOrder, row.WorkOrder, StringComparison.OrdinalIgnoreCase))
            throw new SakuraValidationException("cartonLabel.pallet.workOrderMismatch", $"Carton Number '{trimmedCarton}' thuộc Work Order '{row.WorkOrder}', khác với Work Order '{existingWorkOrder}' đang gom vào Pallet này.", new { cartonNumber = trimmedCarton, cartonWorkOrder = row.WorkOrder, palletWorkOrder = existingWorkOrder });

        if (row.PalletId != palletId)
        {
            row.PalletId = palletId;

            // Giữ đúng bất biến "carton thuộc Pallet ID nào thì luôn có sẵn Pallet Number của
            // pallet đó" — giống RecordCartonScanAsync: dùng lại số đã có, hoặc sinh mới nếu đây
            // là carton đầu tiên của Pallet ID này.
            string? existingNumber = await _context.CartonSnScanLogs
                .Where(x => x.PalletId == palletId && x.PalletNumber != null)
                .Select(x => x.PalletNumber)
                .FirstOrDefaultAsync();

            if (existingNumber != null)
            {
                row.PalletNumber = existingNumber;
                await _context.SaveChangesAsync();
            }
            else
            {
                await GenerateAndAssignPalletNumberAsync(new List<CartonSnScanLog> { row });
            }
        }

        return await GetPalletBoxesAsync(palletId);
    }

    // Gỡ 1 carton khỏi pallet (xoá 1 dòng trong bảng "Quản lý Pallet") — chỉ gỡ nếu carton đó
    // thực sự đang thuộc đúng Pallet ID này. Đánh dấu thêm IsDeleted = true để biết dòng nào đã
    // bị gỡ qua nút Delete này (CHỈ để tra cứu/audit — không đổi logic đếm số lượng/chặn trùng
    // Carton Number-Serial/History ở nơi khác, những chỗ đó vẫn không lọc theo cột này).
    public async Task<(int BoxCount, int UnitCount, List<CartonSnScanLog> Boxes)> UnscanCartonFromPalletAsync(string palletId, string cartonNumber)
    {
        if (string.IsNullOrWhiteSpace(palletId))
            throw new SakuraValidationException("cartonLabel.pallet.palletIdMissing", "Thiếu Pallet ID.");
        if (string.IsNullOrWhiteSpace(cartonNumber))
            throw new SakuraValidationException("cartonLabel.cartonNumberMissing", "Thiếu Carton Number.");

        string trimmedCarton = cartonNumber.Trim();
        var row = await _context.CartonSnScanLogs.FirstOrDefaultAsync(x => x.CartonNumber == trimmedCarton && x.PalletId == palletId);
        if (row == null)
            throw new SakuraValidationException("cartonLabel.pallet.cartonNotFound", $"Carton Number '{trimmedCarton}' không thuộc Pallet ID '{palletId}'.", new { cartonNumber = trimmedCarton, palletId });

        row.PalletId = null;
        row.IsDeleted = true;
        await _context.SaveChangesAsync();

        return await GetPalletBoxesAsync(palletId);
    }

    // Sinh Pallet Number kế tiếp dạng "P-RM15A-XXXXX" (RM15A cố định theo Model hiện tại, XXXXX
    // chạy 00001-99999, KHÔNG phân biệt màu). Cùng pattern concurrency-safe (transaction
    // Serializable + retry khi va chạm unique) với GenerateNextSerialsAsync ở trên.
    private const int MaxPalletRunningNumber = 99999;

    // Sinh Pallet Number + gán ngay vào toàn bộ carton của pallet TRONG CÙNG 1 transaction —
    // để unique violation (2 request cùng sinh trùng số) bị bắt và retry được ở đúng chỗ, giống
    // GenerateNextSerialsAsync (khác với generate rồi save riêng ở ngoài, sẽ không retry được
    // khi va chạm).
    private async Task<string> GenerateAndAssignPalletNumberAsync(List<CartonSnScanLog> rows)
    {
        string prefix = $"P-{Model}-";

        const int maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var existing = await _context.CartonSnScanLogs
                    .Where(x => x.PalletNumber != null && x.PalletNumber.StartsWith(prefix))
                    .Select(x => x.PalletNumber!)
                    .Distinct()
                    .ToListAsync();

                int max = -1;
                foreach (string pn in existing)
                {
                    if (int.TryParse(pn.Substring(prefix.Length), out int n) && n > max)
                        max = n;
                }

                int next = max + 1;
                if (next > MaxPalletRunningNumber)
                    throw new SakuraConflictException("cartonLabel.pallet.numberCapacityExceeded", $"Không thể sinh Pallet Number: đã vượt quá {MaxPalletRunningNumber}.", new { max = MaxPalletRunningNumber });

                string candidate = prefix + next.ToString("D5");

                foreach (var row in rows)
                    row.PalletNumber = candidate;
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return candidate;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await tx.RollbackAsync();
                continue;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        throw new SakuraConflictException("cartonLabel.pallet.numberCapacityExceeded", "Không thể sinh Pallet Number do tranh chấp đồng thời quá nhiều lần, vui lòng thử lại.");
    }

    // "Chốt" pallet + build ZPL tem Pallet: lấy toàn bộ carton đang thuộc palletId (không giới
    // hạn đủ 64 thùng — chốt được ở bất kỳ số lượng nào), tính tổng thùng/unit, sinh (hoặc dùng
    // lại nếu đã từng in) Pallet Number, gán vào toàn bộ carton đó, rồi thay token vào template
    // "PalletLabel" (nội dung ZPL thật do người dùng tự cập nhật sau trong SM_Sakura_ZplTemplate).
    public async Task<(string Zpl, string PalletNumber, int QuantityCartons, int QuantityUnits, string Color)> BuildPalletLabelZplAsync(
        string palletId, string poNumber, string inboundReference, string warehouseReference, string deliveryAddress)
    {
        if (string.IsNullOrWhiteSpace(palletId))
            throw new SakuraValidationException("cartonLabel.pallet.palletIdMissing", "Thiếu Pallet ID.");

        var rows = await _context.CartonSnScanLogs
            .Where(x => x.PalletId == palletId)
            .ToListAsync();

        if (rows.Count == 0)
            throw new SakuraValidationException("cartonLabel.pallet.noBoxesScanned", $"Pallet ID '{palletId}' chưa có thùng nào được quét vào.", new { palletId });

        string color = rows[0].Color ?? "";
        if (!ZplTemplates.CartonColorMeta.TryGetValue(color, out var meta))
            throw new SakuraValidationException("cartonLabel.unknownColor", $"Không nhận diện được màu '{color}'.", new { color });

        int quantityCartons = rows.Count;
        int quantityUnits = rows.Sum(x => x.CountSerial);

        string? palletNumber = rows.Select(x => x.PalletNumber).FirstOrDefault(pn => !string.IsNullOrEmpty(pn));
        if (palletNumber == null)
            palletNumber = await GenerateAndAssignPalletNumberAsync(rows);

        // Snapshot PO Number/Inbound Reference/Warehouse Reference/Delivery Address vào MỌI carton
        // của pallet này (ghi lại mỗi lần build tem, kể cả in lần đầu) — không có bảng Pallet riêng
        // để lưu, và cần dữ liệu này sau này cho ReprintPalletLabelAsync build lại đúng tem cũ.
        foreach (var row in rows)
        {
            row.PoNumber = poNumber?.Trim() ?? "";
            row.InboundReference = inboundReference?.Trim() ?? "";
            row.WarehouseReference = warehouseReference?.Trim() ?? "";
            row.DeliveryAddress = deliveryAddress?.Trim() ?? "";
        }
        await _context.SaveChangesAsync();

        string template = await GetZplTemplateAsync("PalletLabel");
        string zpl = template
            .Replace("{skuPvId}", meta.SkuPvId)
            .Replace("{ean}", meta.Ean)
            .Replace("{cartonQty}", quantityCartons.ToString())
            .Replace("{unitQty}", quantityUnits.ToString())
            .Replace("{poNumber}", poNumber ?? "")
            .Replace("{inboundReference}", inboundReference ?? "")
            .Replace("{warehouseReference}", warehouseReference ?? "")
            .Replace("{vendorCode}", "CN50")
            .Replace("{palletNumber}", palletNumber)
            .Replace("{deliveryTo}", FormatZplDeliveryAddress(deliveryAddress ?? ""));

        return (zpl, palletNumber, quantityCartons, quantityUnits, color);
    }

    // ── Pallet Info Template — preset Inbound Reference/Warehouse Reference/Delivery Address
    // để chọn nhanh ở vùng Print Pallet, khỏi gõ tay mỗi lần in (PO Number không nằm trong
    // template, luôn nhập tay riêng vì đổi theo từng pallet/lô hàng). ─────────────────────────

    public async Task<List<PalletInfoTemplate>> GetPalletInfoTemplatesAsync() =>
        await _context.PalletInfoTemplates.AsNoTracking().OrderBy(x => x.TemplateName).ToListAsync();

    public async Task<PalletInfoTemplate> CreatePalletInfoTemplateAsync(PalletInfoTemplateUpsertRequest req)
    {
        string name = req.TemplateName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
            throw new SakuraValidationException("palletTemplate.nameMissing", "Thiếu tên Template.");

        if (await _context.PalletInfoTemplates.AnyAsync(x => x.TemplateName == name))
            throw new SakuraConflictException("palletTemplate.nameAlreadyExists", $"Template '{name}' đã tồn tại.", new { name });

        var row = new PalletInfoTemplate
        {
            TemplateName = name,
            PoNumber = req.PoNumber?.Trim() ?? "",
            InboundReference = req.InboundReference?.Trim() ?? "",
            WarehouseReference = req.WarehouseReference?.Trim() ?? "",
            DeliveryAddress = req.DeliveryAddress?.Trim() ?? "",
            UpdatedAt = VietnamNow()
        };
        _context.PalletInfoTemplates.Add(row);
        await _context.SaveChangesAsync();
        return row;
    }

    public async Task<PalletInfoTemplate> UpdatePalletInfoTemplateAsync(int id, PalletInfoTemplateUpsertRequest req)
    {
        var row = await _context.PalletInfoTemplates.FindAsync(id);
        if (row == null)
            throw new SakuraValidationException("palletTemplate.notFound", $"Không tìm thấy Template Id={id}.");

        string name = req.TemplateName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
            throw new SakuraValidationException("palletTemplate.nameMissing", "Thiếu tên Template.");

        if (await _context.PalletInfoTemplates.AnyAsync(x => x.Id != id && x.TemplateName == name))
            throw new SakuraConflictException("palletTemplate.nameAlreadyExists", $"Template '{name}' đã tồn tại.", new { name });

        row.TemplateName = name;
        row.PoNumber = req.PoNumber?.Trim() ?? "";
        row.InboundReference = req.InboundReference?.Trim() ?? "";
        row.WarehouseReference = req.WarehouseReference?.Trim() ?? "";
        row.DeliveryAddress = req.DeliveryAddress?.Trim() ?? "";
        row.UpdatedAt = VietnamNow();
        await _context.SaveChangesAsync();
        return row;
    }

    public async Task DeletePalletInfoTemplateAsync(int id)
    {
        var row = await _context.PalletInfoTemplates.FindAsync(id);
        if (row == null)
            throw new SakuraValidationException("palletTemplate.notFound", $"Không tìm thấy Template Id={id}.");

        _context.PalletInfoTemplates.Remove(row);
        await _context.SaveChangesAsync();
    }

    // Chuẩn bị {deliveryTo} cho field ^FH\...^FD...^FS trong template PalletLabel — field này
    // dùng "\" làm hex indicator (^FH\), nên: ký tự đặc biệt ZPL ^ và ~ (nếu người dùng gõ vào
    // địa chỉ) phải escape thành \5E/\7E (mã hex ASCII) để không bị hiểu nhầm thành lệnh ZPL;
    // xuống dòng (\r\n hoặc \n) phải đổi thành "\&" — cú pháp ép xuống dòng của ^FB khi hex
    // indicator là "\". Escape ký tự đặc biệt TRƯỚC khi đổi xuống dòng vì "\&" không chứa ^/~.
    private static string FormatZplDeliveryAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) return "";

        string escaped = address.Replace("^", "\\5E").Replace("~", "\\7E");
        string normalized = escaped.Replace("\r\n", "\n").Replace("\r", "\n");
        return normalized.Replace("\n", "\\&");
    }

    // Trang History của Carton SN Label — 1 dòng lịch sử = 1 carton đã in (khớp 1-1 với
    // SM_Sakura_CartonLabel_Data, không phải 1 dòng/serial). Filter theo ngày (ScanDate), Work
    // Order/Carton Number/Serial (tìm theo substring, Serial search luôn trong cả chuỗi CSV),
    // và Color (khớp chính xác).
    public async Task<CartonSnHistoryPageDto> GetCartonHistoryAsync(DateTime? dateFrom, DateTime? dateTo, string? workOrder, string? cartonNumber, string? serial, string? color, string? palletId, string? palletNumber, int page, int pageSize, bool? isReprint = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.CartonSnScanLogs.AsNoTracking().AsQueryable();

        if (dateFrom.HasValue)
            query = query.Where(x => x.ScanDate >= dateFrom.Value.Date);

        if (dateTo.HasValue)
        {
            // dateTo bao gồm hết ngày đó (< ngày hôm sau), không phải chỉ tới 00:00.
            DateTime dateToEnd = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.ScanDate < dateToEnd);
        }

        if (!string.IsNullOrWhiteSpace(workOrder))
        {
            string wo = workOrder.Trim();
            query = query.Where(x => x.WorkOrder.Contains(wo));
        }

        if (!string.IsNullOrWhiteSpace(cartonNumber))
        {
            string cn = cartonNumber.Trim();
            query = query.Where(x => x.CartonNumber.Contains(cn));
        }

        if (!string.IsNullOrWhiteSpace(serial))
        {
            string s = serial.Trim();
            query = query.Where(x => x.Serial.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(color))
        {
            string c = color.Trim();
            query = query.Where(x => x.Color == c);
        }

        if (!string.IsNullOrWhiteSpace(palletId))
        {
            string pid = palletId.Trim();
            query = query.Where(x => x.PalletId != null && x.PalletId.Contains(pid));
        }

        if (!string.IsNullOrWhiteSpace(palletNumber))
        {
            string pn = palletNumber.Trim();
            query = query.Where(x => x.PalletNumber != null && x.PalletNumber.Contains(pn));
        }

        if (isReprint.HasValue)
            query = query.Where(x => x.IsReprint == isReprint.Value);

        int totalCount = await query.CountAsync();

        var rows = await query
            .OrderByDescending(x => x.ScanDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CartonSnHistoryPageDto
        {
            Items = rows.Select(x => new CartonSnHistoryItemDto
            {
                Id = x.Id,
                CartonNumber = x.CartonNumber,
                WorkOrder = x.WorkOrder,
                Color = x.Color ?? "",
                Condition = x.Condition ?? "",
                CountSerial = x.CountSerial,
                Serial = x.Serial,
                ScanDate = x.ScanDate,
                PalletId = x.PalletId,
                PalletNumber = x.PalletNumber,
                IsReprint = x.IsReprint
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // ── Reprint (trang /sakura/cartonsn/reprint) ────────────────────────────────────────────
    // Danh sách Pallet đã "chốt"/in tem (nhóm theo PalletNumber, mỗi PalletNumber gán chung cho
    // nhiều carton — xem RecordCartonScanAsync/BuildPalletLabelZplAsync). Group + filter theo
    // IsPalletReprint (aggregate) trong memory — số dòng sau khi lọc theo ngày/WO/PalletId/Number
    // đủ nhỏ để làm vậy an toàn hơn là ép EF dịch GROUP BY + HAVING trên aggregate boolean.
    public async Task<CartonSnPalletReprintPageDto> GetPalletReprintListAsync(
        DateTime? dateFrom, DateTime? dateTo, string? workOrder, string? palletId, string? palletNumber, int page, int pageSize, bool? isPalletReprint = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.CartonSnScanLogs.AsNoTracking().Where(x => x.PalletNumber != null).AsQueryable();

        if (dateFrom.HasValue)
            query = query.Where(x => x.ScanDate >= dateFrom.Value.Date);

        if (dateTo.HasValue)
        {
            DateTime dateToEnd = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.ScanDate < dateToEnd);
        }

        if (!string.IsNullOrWhiteSpace(workOrder))
        {
            string wo = workOrder.Trim();
            query = query.Where(x => x.WorkOrder.Contains(wo));
        }

        if (!string.IsNullOrWhiteSpace(palletId))
        {
            string pid = palletId.Trim();
            query = query.Where(x => x.PalletId != null && x.PalletId.Contains(pid));
        }

        if (!string.IsNullOrWhiteSpace(palletNumber))
        {
            string pn = palletNumber.Trim();
            query = query.Where(x => x.PalletNumber!.Contains(pn));
        }

        var rows = await query.ToListAsync();

        var grouped = rows
            .GroupBy(x => x.PalletNumber)
            .Select(g => new CartonSnPalletReprintItemDto
            {
                PalletNumber = g.Key!,
                PalletId = g.Select(x => x.PalletId).FirstOrDefault() ?? "",
                WorkOrder = g.Select(x => x.WorkOrder).FirstOrDefault() ?? "",
                Color = g.Select(x => x.Color).FirstOrDefault() ?? "",
                CartonCount = g.Count(),
                UnitCount = g.Sum(x => x.CountSerial),
                IsPalletReprint = g.Any(x => x.IsPalletReprint),
                LastScanDate = g.Max(x => x.ScanDate)
            });

        if (isPalletReprint.HasValue)
            grouped = grouped.Where(x => x.IsPalletReprint == isPalletReprint.Value);

        var ordered = grouped.OrderByDescending(x => x.LastScanDate).ToList();

        int totalCount = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CartonSnPalletReprintPageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // In lại ĐÚNG dữ liệu cũ đã lưu (Work Order/Carton Number/Serial/Color/Condition) cho 1 carton
    // đã in trước đó — KHÔNG check "đã in trước đó" (chắc chắn đã in — đó là lý do reprint),
    // KHÔNG tính lại số lượng WO, KHÔNG cho sửa serial. Đánh dấu IsReprint = true để trang Reprint/
    // History biết carton nào đã từng in lại.
    public async Task<CartonReprintZplResponse> ReprintCartonLabelAsync(int id)
    {
        var row = await _context.CartonSnScanLogs.FirstOrDefaultAsync(x => x.Id == id);
        if (row == null)
            throw new SakuraValidationException("cartonLabel.reprint.notFound", $"Không tìm thấy Carton Id={id}.", new { id });

        if (!ZplTemplates.CartonColorMeta.TryGetValue(row.Color ?? "", out var meta))
            throw new SakuraValidationException("cartonLabel.unknownColor", $"Không nhận diện được màu '{row.Color}'.", new { color = row.Color });

        var slots = (row.Serial ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (slots.Count == 0)
            throw new SakuraValidationException("cartonLabel.reprint.noSerials", $"Carton '{row.CartonNumber}' không có serial để in lại.", new { cartonNumber = row.CartonNumber });

        string zpl = await RenderCartonZplAsync(row.CartonNumber, meta, row.Condition ?? "New", slots, slots);

        row.IsReprint = true;
        await _context.SaveChangesAsync();

        return new CartonReprintZplResponse { Zpl = zpl, CartonNumber = row.CartonNumber };
    }

    // In lại tem Pallet ĐÚNG dữ liệu cũ (PO Number/Inbound/Warehouse/Delivery Address snapshot
    // lúc build tem gần nhất — xem BuildPalletLabelZplAsync) cho 1 Pallet Number đã "chốt"/in tem
    // trước đó. Đánh dấu IsPalletReprint = true trên MỌI carton của pallet này.
    public async Task<PalletReprintZplResponse> ReprintPalletLabelAsync(string palletNumber)
    {
        if (string.IsNullOrWhiteSpace(palletNumber))
            throw new SakuraValidationException("cartonLabel.pallet.palletNumberMissing", "Thiếu Pallet Number.");

        string trimmed = palletNumber.Trim();
        var rows = await _context.CartonSnScanLogs.Where(x => x.PalletNumber == trimmed).ToListAsync();
        if (rows.Count == 0)
            throw new SakuraValidationException("cartonLabel.pallet.notFound", $"Không tìm thấy Pallet Number '{trimmed}'.", new { palletNumber = trimmed });

        string color = rows[0].Color ?? "";
        if (!ZplTemplates.CartonColorMeta.TryGetValue(color, out var meta))
            throw new SakuraValidationException("cartonLabel.unknownColor", $"Không nhận diện được màu '{color}'.", new { color });

        int quantityCartons = rows.Count;
        int quantityUnits = rows.Sum(x => x.CountSerial);

        string template = await GetZplTemplateAsync("PalletLabel");
        string zpl = template
            .Replace("{skuPvId}", meta.SkuPvId)
            .Replace("{ean}", meta.Ean)
            .Replace("{cartonQty}", quantityCartons.ToString())
            .Replace("{unitQty}", quantityUnits.ToString())
            .Replace("{poNumber}", rows[0].PoNumber ?? "")
            .Replace("{inboundReference}", rows[0].InboundReference ?? "")
            .Replace("{warehouseReference}", rows[0].WarehouseReference ?? "")
            .Replace("{vendorCode}", "CN50")
            .Replace("{palletNumber}", trimmed)
            .Replace("{deliveryTo}", FormatZplDeliveryAddress(rows[0].DeliveryAddress ?? ""));

        foreach (var row in rows)
            row.IsPalletReprint = true;
        await _context.SaveChangesAsync();

        return new PalletReprintZplResponse { Zpl = zpl, PalletNumber = trimmed, QuantityCartons = quantityCartons, QuantityUnits = quantityUnits };
    }

    // Ngày sản xuất của lần in đầu tiên cho Work Order này (null nếu chưa in lần nào).
    // Dùng để hiển thị/khóa ô ngày trên form khi lookup lại 1 WO đã in dở.
    public async Task<DateTime?> GetWorkOrderProductionDateAsync(string workOrder)
    {
        return await _context.SnLabelPrints
            .AsNoTracking()
            .Where(x => x.WorkOrder == workOrder)
            .OrderBy(x => x.PrintedAt)
            .Select(x => (DateTime?)x.ProductionDate)
            .FirstOrDefaultAsync();
    }

    // ── Status / summary ──────────────────────────────────────────────────────

    public async Task<SnLabelStatusDto> GetStatusAsync(DateTime date, string variant, string line)
    {
        string color = ResolveColor(variant);
        DateTime prodDate = date.Date;

        var dayLineRows = await _context.SnLabelPrints
            .AsNoTracking()
            .Where(x => x.ProductionDate == prodDate && x.ProductionLine == line)
            .Select(x => new { x.Variant, x.SerialNumber, x.RunningNumberInt, x.Color })
            .ToListAsync();

        var forVariant = dayLineRows.Where(x => x.Variant == variant).ToList();
        var last = forVariant.OrderByDescending(x => x.RunningNumberInt).FirstOrDefault();
        int nextRunning = (last?.RunningNumberInt ?? -1) + 1;

        string nextSerial = nextRunning > MaxRunningNumberInt
            ? ""
            : BuildSerial(variant, prodDate, line, FormatRunning(nextRunning));

        var summary = Variants.Select(v =>
        {
            var rows = dayLineRows.Where(x => x.Variant == v.Variant).ToList();
            var lastForColor = rows.OrderByDescending(x => x.RunningNumberInt).FirstOrDefault();
            return new SnLabelColorSummaryDto
            {
                Variant = v.Variant,
                Color = v.Color,
                Count = rows.Count,
                LastSerial = lastForColor?.SerialNumber
            };
        }).ToList();

        return new SnLabelStatusDto
        {
            Date = prodDate,
            Line = line,
            Variant = variant,
            Color = color,
            LastSerial = last?.SerialNumber,
            NextSerial = nextSerial,
            Count = forVariant.Count,
            RemainingCapacity = Math.Max(0, MaxRunningNumberInt - nextRunning + 1),
            ColorSummary = summary
        };
    }

    // ── History ───────────────────────────────────────────────────────────────

    // Đọc từ SM_SNLabelScanLog — audit trail ĐẦY ĐỦ mọi lần quét EAN+Serial (kể cả các lần
    // FAIL, mỗi lần 1 dòng riêng, không bị ghi đè). SM_SNLabelPrint (chỉ chứa serial đã in
    // THÀNH CÔNG) chỉ được join thêm vào để lấy ReprintCount/LastReprintedAt cho badge Reprint.
    public async Task<SnLabelHistoryPageDto> GetHistoryAsync(DateTime? date, string? workOrder, string? serialNumber, string? ean, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.SnLabelScanLogs.AsNoTracking().AsQueryable();

        if (date.HasValue)
        {
            DateTime dayStart = date.Value.Date;
            DateTime dayEnd = dayStart.AddDays(1);
            query = query.Where(x => x.Timeline >= dayStart && x.Timeline < dayEnd);
        }

        if (!string.IsNullOrWhiteSpace(workOrder))
        {
            string wo = workOrder.Trim();
            query = query.Where(x => x.WorkOrder.Contains(wo));
        }

        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            string sn = serialNumber.Trim();
            query = query.Where(x => x.SerialNumber.Contains(sn));
        }

        if (!string.IsNullOrWhiteSpace(ean))
        {
            string eanFilter = ean.Trim();
            query = query.Where(x => x.Ean != null && x.Ean.Contains(eanFilter));
        }

        int totalCount = await query.CountAsync();

        var logRows = await query
            .OrderByDescending(x => x.Timeline)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Reprint chỉ có ý nghĩa với serial đã in thật (nằm trong SM_SNLabelPrint) — join tay
        // trong bộ nhớ (sau khi đã phân trang) cho gọn, thay vì viết LEFT JOIN trong LINQ-to-SQL.
        var serialsInPage = logRows.Select(x => x.SerialNumber).Distinct().ToList();
        var printInfoBySerial = await _context.SnLabelPrints
            .AsNoTracking()
            .Where(x => serialsInPage.Contains(x.SerialNumber))
            .Select(x => new { x.SerialNumber, x.ReprintCount, x.LastReprintedAt })
            .ToDictionaryAsync(x => x.SerialNumber);

        var items = logRows.Select(x =>
        {
            printInfoBySerial.TryGetValue(x.SerialNumber, out var printInfo);
            return new SnLabelHistoryItemDto
            {
                Id = x.Id,
                SerialNumber = x.SerialNumber,
                Variant = x.Variant ?? "",
                Color = x.Color ?? "",
                ProductionLine = x.ProductionLine ?? "",
                PrintedAt = x.Timeline,
                WorkOrder = x.WorkOrder,
                ReprintCount = printInfo?.ReprintCount ?? 0,
                LastReprintedAt = printInfo?.LastReprintedAt,
                Ean = x.Ean,
                Status = x.Status,
                FailedStep = x.FailedStep
            };
        }).ToList();

        return new SnLabelHistoryPageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // Danh sách Work Order khác nhau đã in — nếu có ngày thì chỉ lấy WO của ngày đó
    // (dùng để đổ vào dropdown filter Work Order trên trang History).
    public async Task<List<string>> GetWorkOrdersAsync(DateTime? date)
    {
        var query = _context.SnLabelPrints
            .AsNoTracking()
            .Where(x => x.WorkOrder != null && x.WorkOrder != "");

        if (date.HasValue)
        {
            DateTime prodDate = date.Value.Date;
            query = query.Where(x => x.ProductionDate == prodDate);
        }

        return await query
            .Select(x => x.WorkOrder!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    public async Task<List<SnLabelPrint>> GetByBatchAsync(Guid batchId)
    {
        return await _context.SnLabelPrints
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.RunningNumberInt)
            .ToListAsync();
    }

    // ── Reprint by Serial (Manual mode) — re-emit ZPL for an already-printed serial ──
    // Đánh dấu reprint ngay trên dòng gốc (không tạo dòng mới, không đụng RunningNumber) —
    // để trang History biết serial nào đã bị in lại, in lại mấy lần, lần gần nhất khi nào.
    public async Task<SnLabelPrint?> MarkReprintedAsync(string serialNumber, string? reprintedBy)
    {
        var row = await _context.SnLabelPrints.FirstOrDefaultAsync(x => x.SerialNumber == serialNumber.Trim());
        if (row == null) return null;

        row.ReprintCount += 1;
        row.LastReprintedAt = VietnamNow();
        row.LastReprintedBy = reprintedBy;
        await _context.SaveChangesAsync();
        return row;
    }

    // ── ZPL templates (stored in DB — SM_Sakura_ZplTemplate) ─────────────────

    // Trả về nội dung template đang active cho 1 key (vd "SnLabel").
    // Nếu chưa có trong DB (chưa seed), trả về fallback từ ZplTemplates để không vỡ luồng in.
    public async Task<string> GetZplTemplateAsync(string templateKey)
    {
        var row = await _context.SakuraZplTemplates
            .AsNoTracking()
            .Where(x => x.TemplateKey == templateKey && x.IsActive)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();

        if (row != null) return row.ZplContent;

        return templateKey switch
        {
            "SnLabel" => ZplTemplates.DefaultSnLabel,
            "CartonLabel" => ZplTemplates.DefaultCartonLabel,
            _ => ""
        };
    }

    // Thêm mới hoặc cập nhật template theo key — dùng để sửa ZPL trực tiếp trong DB
    // (qua API) thay vì phải sửa code.
    public async Task<SakuraZplTemplate> UpsertZplTemplateAsync(string templateKey, string zplContent, string? updatedBy, string? name = null)
    {
        var row = await _context.SakuraZplTemplates
            .FirstOrDefaultAsync(x => x.TemplateKey == templateKey);

        var now = VietnamNow();
        if (row == null)
        {
            row = new SakuraZplTemplate
            {
                TemplateKey = templateKey,
                Name = name ?? templateKey,
                IsActive = true
            };
            _context.SakuraZplTemplates.Add(row);
        }

        row.ZplContent = zplContent;
        row.UpdatedAt = now;
        row.UpdatedBy = updatedBy;
        if (name != null) row.Name = name;

        await _context.SaveChangesAsync();
        return row;
    }

    public static string BuildConcatenatedZpl(string templateContent, IEnumerable<string> serials) =>
        string.Concat(serials.Select(s => templateContent.Replace("{serialNumber}", s)));

    // Carton SN Label — 1 label chứa nhiều placeholder khác nhau (khác với SnLabel chỉ có
    // {serialNumber}): tra màu → SKU/PV ID + mô tả, rồi thay từng {sn1}..{sn10} theo ĐÚNG VỊ
    // TRÍ ô SN tương ứng trên form. Tọa độ/cỡ chữ/barcode của từng ô đã có sẵn trong template
    // (DB) — code không tự tính toạ độ nữa, chỉ truyền giá trị vào đúng placeholder.
    public async Task<string> BuildCartonLabelZplAsync(string cartonNumber, string color, string condition, IReadOnlyList<string> serialNumbers, string? workOrder = null, string? palletId = null)
    {
        if (string.IsNullOrWhiteSpace(cartonNumber))
            throw new SakuraValidationException("cartonLabel.cartonNumberMissing", "Thiếu Carton Number.");

        string trimmedCartonNumber = cartonNumber.Trim();
        if (await IsCartonNumberAlreadyUsedAsync(trimmedCartonNumber))
            throw new SakuraConflictException("cartonLabel.cartonNumberAlreadyUsed", $"Carton Number '{trimmedCartonNumber}' đã được sử dụng trước đó.", new { cartonNumber = trimmedCartonNumber });

        if (color == null || !ZplTemplates.CartonColorMeta.TryGetValue(color, out var meta))
            throw new SakuraValidationException("cartonLabel.unknownColor", $"Không nhận diện được màu '{color}'.", new { color });

        if (condition != "New" && condition != "Refurb")
            throw new SakuraValidationException("cartonLabel.invalidCondition", $"Condition '{condition}' không hợp lệ (chỉ New hoặc Refurb).", new { condition });

        // 1 Pallet ID chỉ được chứa carton của ĐÚNG 1 Work Order — chặn NGAY TỪ LÚC IN (trước khi
        // build ZPL/gửi máy in), không đợi tới lúc lưu kết quả in mới phát hiện (RecordCartonScanAsync)
        // để tránh in ra tem carton rồi mới báo lỗi không gộp được vào pallet.
        if (!string.IsNullOrWhiteSpace(palletId))
        {
            string trimmedPalletId = palletId.Trim();
            string? existingWorkOrder = await _context.CartonSnScanLogs
                .Where(x => x.PalletId == trimmedPalletId)
                .Select(x => x.WorkOrder)
                .FirstOrDefaultAsync();
            if (existingWorkOrder != null && !string.Equals(existingWorkOrder, workOrder ?? "", StringComparison.OrdinalIgnoreCase))
                throw new SakuraConflictException("cartonLabel.pallet.workOrderMismatch", $"Pallet ID '{trimmedPalletId}' đang thuộc Work Order '{existingWorkOrder}', khác với Work Order '{workOrder}' hiện tại. Đổi Pallet ID hoặc Work Order trước khi in.", new { cartonNumber = trimmedCartonNumber, cartonWorkOrder = workOrder, palletWorkOrder = existingWorkOrder });
        }

        // QUAN TRỌNG: giữ nguyên vị trí — slots[i] LUÔN ứng với ô SN(i+1) trên form, kể cả khi
        // ô đó đang bỏ trống (chuỗi rỗng). KHÔNG được lọc bỏ ô trống rồi dồn mảng lại, nếu
        // không serial ở ô sau sẽ bị dồn lên nhầm vị trí {snN} (vd ô SN3 bị in nhầm vào {sn2}
        // nếu SN2 đang trống mà mảng bị lọc/dồn trước khi tới đây).
        var slots = (serialNumbers ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .ToList();

        if (slots.Count > ZplTemplates.CartonSnPlaceholderCount)
            throw new SakuraValidationException("cartonLabel.invalidQuantity", $"Số lượng serial phải từ 1 đến {ZplTemplates.CartonSnPlaceholderCount}.", new { max = ZplTemplates.CartonSnPlaceholderCount });

        var nonEmptySerials = slots.Where(s => s.Length > 0).ToList();
        if (nonEmptySerials.Count == 0)
            throw new SakuraValidationException("cartonLabel.invalidQuantity", $"Số lượng serial phải từ 1 đến {ZplTemplates.CartonSnPlaceholderCount}.", new { max = ZplTemplates.CartonSnPlaceholderCount });

        if (nonEmptySerials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != nonEmptySerials.Count)
            throw new SakuraValidationException("cartonLabel.duplicateSerial", "Danh sách serial có giá trị trùng lặp.");

        // Re-check định dạng + đã quét/in trước đó chưa NGAY TẠI ĐÂY (không tin riêng client đã
        // check qua verify-serial lúc quét từng ô) — áp dụng cho cả Preview lẫn Print thật vì cả
        // 2 đều đi qua đúng hàm build này.
        foreach (string s in nonEmptySerials)
        {
            if (!TryParseSerialParts(s, out _, out _, out _, out _))
                throw new SakuraValidationException("cartonLabel.invalidSerialFormat", $"Serial '{s}' không đúng định dạng.", new { serial = s });
        }

        // Serial giờ lưu dạng CSV nhiều serial/carton trong 1 dòng nên không thể query 1 phát
        // bằng Contains(nonEmptySerials) như trước — check từng serial một qua LIKE (xem
        // IsCartonSerialAlreadyUsedAsync). Danh sách tối đa 10 serial/carton nên chấp nhận được.
        var alreadyUsed = new List<string>();
        foreach (string s in nonEmptySerials)
        {
            if (await IsCartonSerialAlreadyUsedAsync(s))
                alreadyUsed.Add(s);
        }
        if (alreadyUsed.Count > 0)
        {
            // Dùng chung param "serial" (không phải "serials") với ValidateCartonSerialAsync để
            // front-end chỉ cần 1 bản dịch "error.cartonLabel.serialAlreadyUsed" duy nhất cho cả
            // 2 nơi ném lỗi này (verify-serial từng ô lẫn re-check lúc build ZPL).
            string joined = string.Join(", ", alreadyUsed);
            throw new SakuraConflictException("cartonLabel.serialAlreadyUsed", $"Serial đã được quét/in trước đó: {joined}.", new { serial = joined });
        }

        return await RenderCartonZplAsync(trimmedCartonNumber, meta, condition, slots, nonEmptySerials);
    }

    // Phần render ZPL thuần (đọc template + thay placeholder) — tách riêng khỏi phần validate/
    // check DB ở BuildCartonLabelZplAsync cho dễ đọc.
    private async Task<string> RenderCartonZplAsync(string cartonNumber, (string SkuPvId, string Description, string Ean) meta, string condition, List<string> slots, List<string> nonEmptySerials)
    {
        string template = await GetZplTemplateAsync("CartonLabel");

        string zpl = template
            .Replace("{cartonNumber}", cartonNumber)
            .Replace("{skuPvId}", meta.SkuPvId)
            .Replace("{description}", meta.Description)
            .Replace("{quantity}", nonEmptySerials.Count.ToString())
            .Replace("{condition}", condition)
            .Replace("{pdf417Data}", string.Join(",", nonEmptySerials));

        // CHỈ thay {snN} cho ô CÓ serial — ô trống (carton lẻ hộp, index > N) để nguyên
        // placeholder "{snN}", KHÔNG thay bằng chuỗi rỗng. Text field rỗng ("^FD^FS") thì không
        // in ra gì, nhưng barcode Code128 ("^BC") với dữ liệu rỗng vẫn khiến máy in vẽ ra 1 vạch
        // vô nghĩa/lởm chởm (start+stop+checksum không nội dung) — nên phải xóa HẲN CẢ 2 dòng
        // (text lẫn barcode) của ô trống ở bước sau, không chỉ riêng dòng barcode.
        for (int i = 0; i < ZplTemplates.CartonSnPlaceholderCount; i++)
        {
            if (i < slots.Count && slots[i].Length > 0)
                zpl = zpl.Replace($"{{sn{i + 1}}}", slots[i]);
        }

        // Xóa mọi dòng còn chứa "{sn" — đây chính xác là các dòng của ô KHÔNG có serial (chưa bị
        // thay ở trên vì không khớp điều kiện), gồm cả dòng text lẫn dòng barcode. Xóa theo cách
        // này chắc chắn không sót placeholder nào và không đụng tới bất kỳ field nào khác.
        var lines = zpl.Replace("\r\n", "\n").Split('\n');
        zpl = string.Join("\n", lines.Where(line => !line.Contains("{sn")));

        return zpl;
    }

    // ── Direct TCP send (raw port 9100) ──────────────────────────────────────

    public async Task SendZplAsync(string host, int port, string zpl)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port);
        using var stream = client.GetStream();
        byte[] bytes = Encoding.UTF8.GetBytes(zpl);
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Public vì các controller/service khác (vd BackPanelController khi ghi log)
    // cũng cần giờ Việt Nam nhất quán với giờ in nhãn SN.
    public static DateTime VietnamNow()
    {
        var tzId = System.Runtime.InteropServices.RuntimeInformation
            .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "SE Asia Standard Time" : "Asia/Bangkok";
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(tzId));
    }
}
