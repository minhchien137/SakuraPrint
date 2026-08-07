using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Filters;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// Controller chung cho các chức năng thuộc dự án Sakura.
// SN Label Print là chức năng đầu tiên — các chức năng Sakura khác sẽ được thêm vào đây.
//
// [Authorize] ở đây bắt buộc đăng nhập (bất kỳ Role nào — Worker/LineLeader/Admin) cho MỌI
// action trong controller này (xem AccountController/Program.cs — cookie auth). Các action
// dành riêng cho Reprint/Rework (LineLeader trở lên) có thêm [Authorize(Roles = "LineLeader,Admin")]
// đè lên — thay thế hoàn toàn cho modal verify-reprint-login cũ.
[Authorize]
public class SakuraController : Controller
{
    private readonly SakuraService _snLabel;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ViidooService _viidoo;
    private readonly ProductionResultApiService _productionApi;
    private readonly SakuraLookupService _lookup;

    public SakuraController(SakuraService snLabel, IConfiguration config, AppDbContext db, ViidooService viidoo, ProductionResultApiService productionApi, SakuraLookupService lookup)
    {
        _snLabel = snLabel;
        _config = config;
        _db = db;
        _viidoo = viidoo;
        _productionApi = productionApi;
        _lookup = lookup;
    }

    // Gói 1 exception thành JSON lỗi có errorCode/errorParams (nếu có) để front-end tự
    // dịch theo EN/ZH đang chọn (xem sakura-i18n.js -> translateApiError). Exception
    // không có mã (vd lỗi .NET/network chung chung) vẫn được dịch qua mã dùng chung
    // "common.unexpectedError", kèm message gốc làm tham số.
    private static object BuildError(Exception ex)
    {
        if (ex is ISakuraCodedException coded)
            return new { ok = false, error = ex.Message, errorCode = coded.Code, errorParams = coded.Params };
        return new { ok = false, error = ex.Message, errorCode = "common.unexpectedError", errorParams = new { message = ex.Message } };
    }

    // ── View ──────────────────────────────────────────────────────────────────

    // Trang chủ Sakura — tổng hợp các chức năng dưới dạng ô chọn (app tile).
    // Thêm chức năng mới: thêm 1 SakuraAppTile vào danh sách bên dưới.
    [TypeFilter(typeof(LogPageVisitFilter))]
    [HttpGet("/sakura")]
    public IActionResult Index()
    {
        // Title/Subtitle o day la fallback tieng Anh hien khi JS chua chay kip;
        // ban dich day du (EN/ZH) nam trong wwwroot/js/sakura-i18n.js, khoa theo Key.
        var tiles = new List<SakuraAppTile>
        {
            new SakuraAppTile
            {
                Key = "lookup",
                Icon = "🔎",
                Title = "Universal Lookup",
                Subtitle = "Scan to get data",
                Href = Url.Content("~/sakura/lookup"),
                Enabled = true,
                Wide = true
            },
            new SakuraAppTile
            {
                Key = "snlabelgroup",
                Icon = "🏷️",
                Title = "SN label on Gift box",
                Subtitle = "Serial number label printing",
                Enabled = true,
                Items = new List<SakuraAppTile>
                {
                    new SakuraAppTile
                    {
                        Key = "snlabel",
                        Icon = "🖨️",
                        Title = "Print",
                        Subtitle = "Print serial number labels",
                        Href = Url.Content("~/sakura/snlabel"),
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "history",
                        Icon = "🕘",
                        Title = "History",
                        Subtitle = "SN Label print history",
                        Href = Url.Content("~/sakura/snlabel/history"),
                        Enabled = true
                    }
                }
            },
            new SakuraAppTile
            {
                Key = "cartonsngroup",
                Icon = "📦",
                Title = "Carton SN & Pallet Label Print",
                Subtitle = "Carton serial number label printing",
                Enabled = true,
                Items = new List<SakuraAppTile>
                {
                    new SakuraAppTile
                    {
                        Key = "cartonsn",
                        Icon = "🖨️",
                        Title = "Print",
                        Subtitle = "Print Carton SN & Pallet Label",
                        Href = Url.Content("~/sakura/cartonsn"),
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "cartonsnhistory",
                        Icon = "🕘",
                        Title = "History",
                        Subtitle = "Carton SN Label print history",
                        Href = Url.Content("~/sakura/cartonsn/history"),
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "cartonsnreprint",
                        Icon = "↺",
                        Title = "Reprint",
                        Subtitle = "Reprint damaged/misprinted carton or pallet labels",
                        Href = Url.Content("~/sakura/cartonsn/reprint"),
                        Enabled = true
                    }
                }
            },
            new SakuraAppTile
            {
                Key = "laserstation",
                Icon = "🔦",
                Title = "Back Panel Station",
                Subtitle = "Back Panel laser marking check",
                Enabled = true,
                Items = new List<SakuraAppTile>
                {
                    new SakuraAppTile
                    {
                        Key = "laser",
                        Icon = "🔦",
                        Title = "Enter Production Result & Print Laser",
                        Subtitle = "Scan Work Order & Serial Number",
                        Href = Url.Content("~/backpanel/laser"),
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "logLaser",
                        Icon = "📋",
                        Title = "Back Panel Input History",
                        Subtitle = "Laser scan history",
                        Href = Url.Content("~/backpanel/laser/history"),
                        Enabled = true
                    }
                }
            },
            new SakuraAppTile
            {
                Key = "fqcgroup",
                Icon = "✅",
                Title = "FQC Station",
                Subtitle = "Final QC scan stations",
                Enabled = true,
                Items = new List<SakuraAppTile>
                {
                    new SakuraAppTile
                    {
                        Key = "fqc02",
                        Icon = "🔍",
                        Title = "FQC02",
                        Subtitle = "Trạm FQC02",
                        Href = "https://ds.sigmaworldwide.io/ScanCheck/FQC/FQC02",
                        OpenInNewTab = true,
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "fqc04",
                        Icon = "🔍",
                        Title = "FQC04",
                        Subtitle = "Trạm FQC04",
                        Href = "https://ds.sigmaworldwide.io/ScanCheck/FQC/FQC04",
                        OpenInNewTab = true,
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "fqcfg",
                        Icon = "🔍",
                        Title = "FQCFG",
                        Subtitle = "Trạm FQCFG",
                        Href = "https://ds.sigmaworldwide.io/ScanCheck/FQC/FQCBP",
                        OpenInNewTab = true,
                        Enabled = true
                    }
                }
            },
            new SakuraAppTile
            {
                Key = "middlepanelgroup",
                Icon = "🧲",
                Title = "Middle Panel",
                Subtitle = "Middle station tools",
                Enabled = true,
                Items = new List<SakuraAppTile>
                {
                    new SakuraAppTile
                    {
                        Key = "middleinput",
                        Icon = "📝",
                        Title = "Enter Production Result",
                        Subtitle = "Scan Work Order & Serial Number",
                        Href = Url.Content("~/middle/inputresult"),
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "middleinputhistory",
                        Icon = "🕘",
                        Title = "Input Result History — Middle Panel",
                        Subtitle = "Production result scan history",
                        Href = Url.Content("~/middle/inputresult/history"),
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "middletestresult",
                        Icon = "📊",
                        Title = "Middle Panel Test Result",
                        Subtitle = "Dimension check results",
                        Href = Url.Content("~/middle/result"),
                        Enabled = true
                    }
                }
            },
            new SakuraAppTile
            {
                Key = "reworkgroup",
                Icon = "🔁",
                Title = "Rework",
                Subtitle = "Rework station",
                Enabled = true,
                Items = new List<SakuraAppTile>
                {
                    new SakuraAppTile
                    {
                        Key = "rework",
                        Icon = "🔁",
                        Title = "Rework Unbind",
                        Subtitle = "Unbind Carton",
                        Href = Url.Content("~/sakura/rework"),
                        Enabled = true
                    },
                    new SakuraAppTile
                    {
                        Key = "reworkhistory",
                        Icon = "🕘",
                        Title = "Rework History",
                        Subtitle = "Rework scan history",
                        Href = Url.Content("~/sakura/rework/history"),
                        Enabled = true
                    }
                }
            },
            new SakuraAppTile
            {
                Key = "comingsoon",
                Icon = "➕",
                Title = "Coming soon",
                Subtitle = "Next Sakura feature",
                Href = null,
                Enabled = false
            }
        };

        // Chỉ LineLeader/Admin thấy tile này — trỏ tới /sakura/whlabel ([Authorize(Roles=...)] trên
        // chính action đó). Ẩn hẳn với Worker thay vì để bấm vào rồi mới bị 403 (khác cách làm cũ ở
        // Rework/CartonSnReprint — dùng luôn quy ước đúng của tile Admin bên dưới cho tính năng mới này).
        if (User.IsInRole(UserRoles.LineLeader) || User.IsInRole(UserRoles.Admin))
        {
            tiles.Insert(0, new SakuraAppTile
            {
                Key = "whlabel",
                Icon = "🏭",
                Title = "Print Label WH",
                Subtitle = "Print WH picking label by Picking Name",
                Href = Url.Content("~/sakura/whlabel"),
                Enabled = true,
                Wide = true
            });
        }

        // Chỉ Admin thấy tile này — trỏ tới /admin/users (AdminController, [Authorize(Roles="Admin")]).
        if (User.IsInRole(UserRoles.Admin))
        {
            tiles.Insert(0, new SakuraAppTile
            {
                Key = "adminusers",
                Icon = "🛡️",
                Title = "Administrator",
                Subtitle = "Manage Account",
                Href = Url.Content("~/admin/users"),
                Enabled = true,
                Wide = true
            });
        }

        return View("~/Views/Sakura/Index.cshtml", tiles);
    }

    [TypeFilter(typeof(LogPageVisitFilter))]
    [HttpGet("/sakura/snlabel")]
    public IActionResult SnLabelIndex()
    {
        // Tab Reprint + luồng Rework trong trang này chỉ dành cho LineLeader/Admin (xem
        // [Authorize(Roles = ...)] trên các action Reprint/ReworkReprintSnLabel/... bên dưới) —
        // view dùng cờ này để ẩn hẳn tab/luồng đó với Worker thay vì để bấm vào rồi mới bị 403.
        ViewBag.CanReprint = User.IsInRole(UserRoles.LineLeader) || User.IsInRole(UserRoles.Admin);
        return View("~/Views/Sakura/SnLabel.cshtml");
    }

    [HttpGet("/sakura/snlabel/history")]
    public IActionResult SnLabelHistory()
    {
        return View("~/Views/Sakura/History.cshtml");
    }

    [TypeFilter(typeof(LogPageVisitFilter))]
    [HttpGet("/sakura/cartonsn")]
    public IActionResult CartonSnIndex()
    {
        // Pallet Manage/Manage Templates/Repack (Rework WO) chỉ dành cho LineLeader/Admin — xem
        // ghi chú CanReprint ở SnLabelIndex().
        ViewBag.CanReprint = User.IsInRole(UserRoles.LineLeader) || User.IsInRole(UserRoles.Admin);
        return View("~/Views/Sakura/CartonSN.cshtml");
    }

    [HttpGet("/sakura/cartonsn/history")]
    public IActionResult CartonSnHistory()
    {
        return View("~/Views/Sakura/CartonSnHistory.cshtml");
    }

    // ── Universal Lookup — scan 1 mã bất kỳ (Pallet/Carton/Serial), tự nhận diện loại mã ────────

    [TypeFilter(typeof(LogPageVisitFilter))]
    [HttpGet("/sakura/lookup")]
    public IActionResult LookupIndex()
    {
        return View("~/Views/Sakura/Lookup.cshtml");
    }

    [HttpGet("/api/sakura/lookup/resolve")]
    public async Task<IActionResult> LookupResolveApi([FromQuery] string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { ok = false, error = "Thiếu mã cần tra cứu.", errorCode = "common.missingData" });

        var result = await _lookup.ResolveAsync(code);
        if (result == null)
            return Ok(new { ok = true, found = false, data = (object?)null });

        return Ok(new { ok = true, found = true, data = result });
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [TypeFilter(typeof(LogPageVisitFilter))]
    [HttpGet("/sakura/cartonsn/reprint")]
    public IActionResult CartonSnReprint()
    {
        return View("~/Views/Sakura/CartonSnReprint.cshtml");
    }

    // Placeholder — chức năng Rework, chưa có logic, sẽ bổ sung sau.
    [Authorize(Roles = "LineLeader,Admin")]
    [TypeFilter(typeof(LogPageVisitFilter))]
    [HttpGet("/sakura/rework")]
    public IActionResult ReworkIndex()
    {
        return View("~/Views/Sakura/Rework.cshtml");
    }

    [HttpGet("/sakura/rework/history")]
    public IActionResult ReworkHistoryPage()
    {
        return View("~/Views/Sakura/ReworkHistory.cshtml");
    }

    // ── Print Label WH — quét Picking Name (stock.picking, VD "WH/INT/00107"), tra qua Odoo
    // (ViidooService) và in 1 tem WHtoPDLabel cho mỗi dòng stock.move.line (lot) thuộc picking đó ──

    [Authorize(Roles = "LineLeader,Admin")]
    [TypeFilter(typeof(LogPageVisitFilter))]
    [HttpGet("/sakura/whlabel")]
    public IActionResult WhLabelIndex()
    {
        return View("~/Views/Sakura/WhLabel.cshtml");
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/whlabel/lookup")]
    public async Task<IActionResult> WhLabelLookup([FromQuery] string pickingCode)
    {
        if (string.IsNullOrWhiteSpace(pickingCode))
            return BadRequest(new { ok = false, error = "Thiếu Picking Name.", errorCode = "whLabel.missingPickingCode" });

        string code = pickingCode.Trim();
        try
        {
            var moveLines = await _viidoo.GetPickingMoveLinesAsync(code);
            if (moveLines.Count == 0)
                return BadRequest(new { ok = false, error = $"Không tìm thấy Picking '{code}' trên Odoo (hoặc picking không có dòng nào).", errorCode = "whLabel.pickingNotFound", errorParams = new { picking = code } });

            var lines = new List<object>();
            foreach (var l in moveLines)
            {
                string pickingName = l.PickingName ?? code;
                string zpl = await _snLabel.BuildWhToPdLabelZplAsync(l.LotName, pickingName, l.ProductName, l.QtyDone);
                lines.Add(new
                {
                    lotNumber = l.LotName,
                    pickingName,
                    product = l.ProductName,
                    qty = l.QtyDone,
                    locationName = l.LocationName,
                    locationDestName = l.LocationDestName,
                    zpl
                });
            }

            return Ok(new { ok = true, data = new { pickingName = code, lines } });
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: printer list (for other Sakura pages that need it) ────────────────

    [HttpGet("/api/sakura/printers")]
    public async Task<IActionResult> GetPrinters()
    {
        var list = await _db.PrinterInfos
            .Where(p => p.target == "Sakura")
            .Select(p => new { p.ID_Printer, p.Name_Printer, p.IP_Printer, p.Port_Printer })
            .ToListAsync();
        return Ok(list);
    }

    // ── API: status ───────────────────────────────────────────────────────────

    [HttpGet("/api/sakura/snlabel/status")]
    public async Task<IActionResult> Status([FromQuery] DateTime date, [FromQuery] string variant, [FromQuery] string line)
    {
        try
        {
            var result = await _snLabel.GetStatusAsync(date, variant, line);
            return Ok(new { ok = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: Work Order lookup (mode "In qua Work Order") ──────────────────────

    [HttpGet("/api/sakura/snlabel/workorder-lookup")]
    public async Task<IActionResult> WorkOrderLookup([FromQuery] string workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });

        string wo = workOrder.Trim();
        try
        {
            var result = await _viidoo.SearchAsync(wo);
            if (result == null)
                return BadRequest(new { ok = false, error = $"Không tìm thấy Work Order '{wo}' trên Odoo.", errorCode = "workOrder.notFoundOdoo", errorParams = new { wo } });

            if (string.IsNullOrWhiteSpace(result.Color))
                return BadRequest(new { ok = false, error = $"Không xác định được màu cho Work Order '{wo}'.", errorCode = "workOrder.colorUnknown", errorParams = new { wo } });

            if (result.Quantity is not decimal qty || qty < 1)
                return BadRequest(new { ok = false, error = $"Work Order '{wo}' không có số lượng hợp lệ.", errorCode = "workOrder.invalidQuantity", errorParams = new { wo } });

            string? variant = SakuraService.TryResolveVariantFromColor(result.Color);
            if (variant == null)
                return BadRequest(new { ok = false, error = $"Không nhận diện được màu '{result.Color}' trả về từ Odoo.", errorCode = "workOrder.unresolvedColor", errorParams = new { color = result.Color } });

            int totalQuantity = (int)qty;
            int printedQuantity = await _snLabel.GetWorkOrderPrintedCountAsync(wo);
            int remainingQuantity = Math.Max(0, totalQuantity - printedQuantity);

            if (remainingQuantity <= 0)
                return BadRequest(new { ok = false, error = $"Work Order '{wo}' đã in đủ số lượng ({printedQuantity}/{totalQuantity}).", errorCode = "workOrder.exhausted", errorParams = new { wo, printed = printedQuantity, total = totalQuantity } });

            // Nếu WO này đã in dở, ngày sản xuất bị khóa theo lần in đầu tiên — chỉ được
            // chọn ngày tự do khi đây là lần in đầu tiên (chưa có dòng nào trong DB).
            DateTime? lockedDate = printedQuantity > 0
                ? await _snLabel.GetWorkOrderProductionDateAsync(wo)
                : null;

            var response = new WorkOrderLookupResponse
            {
                WorkOrder = wo,
                Variant = variant,
                Color = SakuraService.ResolveColor(variant),
                TotalQuantity = totalQuantity,
                PrintedQuantity = printedQuantity,
                RemainingQuantity = remainingQuantity,
                LockedProductionDate = lockedDate,
                ProductId = result.ProductId,
                ProductCode = result.ProductCode
            };
            return Ok(new { ok = true, data = response });
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: EAN lookup (bước "Check EAN" ở Process) ────────────────────────────
    // Tra lại Work Order để lấy product_id (giống WorkOrderLookup), rồi gọi Odoo
    // product.product/read để lấy mã EAN (x_custcode) — dùng để so khớp với mã EAN
    // người vận hành quét/nhập trước khi cho qua bước Check Serial Number.

    [HttpGet("/api/sakura/snlabel/ean-lookup")]
    public async Task<IActionResult> EanLookup([FromQuery] string workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });

        string wo = workOrder.Trim();
        try
        {
            string? ean = await _viidoo.GetEanByWorkOrderAsync(wo);
            if (string.IsNullOrEmpty(ean))
                return BadRequest(new { ok = false, error = $"Không tìm thấy mã EAN cho Work Order '{wo}'.", errorCode = "ean.notFound", errorParams = new { wo } });

            return Ok(new { ok = true, data = new { workOrder = wo, ean } });
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: Rework — Work Order lookup (Product Code + EAN) ────────────────────
    // Tra Work Order trên Odoo để lấy Mã sản phẩm (bracket code trong product_id[1], vd
    // "[RM15A-1001RW] Sakura - Desert Pink (Rework)" -> "RM15A-1001RW" — xem
    // ViidooService.ExtractCodeFromDescription) và mã EAN (x_custcode) của đúng sản phẩm đó
    // (giống cách EanLookup/GetEanByWorkOrderAsync tra EAN cho SnLabel). EAN có thể null nếu
    // sản phẩm chưa cấu hình x_custcode trên Odoo — không coi đó là lỗi, chỉ hiển thị "—".

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/rework/workorder-lookup")]
    public async Task<IActionResult> ReworkWorkOrderLookup([FromQuery] string workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });

        string wo = workOrder.Trim();
        try
        {
            var result = await _viidoo.SearchAsync(wo);
            if (result == null)
                return BadRequest(new { ok = false, error = $"Không tìm thấy Work Order '{wo}' trên Odoo.", errorCode = "workOrder.notFoundOdoo", errorParams = new { wo } });

            string? ean = result.ProductId is int productId ? await _viidoo.GetProductEanAsync(productId) : null;

            return Ok(new { ok = true, data = new { workOrder = wo, productCode = result.ProductCode, ean, color = result.Color, productId = result.ProductId } });
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: Rework — Check Color & EAN. Tra ngược mã EAN người vận hành quét trên sản phẩm
    // thật (KHÔNG dựa vào product_id của Work Order) -> tìm ra màu của đúng sản phẩm sở hữu EAN
    // đó, để front-end tự so khớp với màu của Work Order đã lookup ở bước 1. Trả lỗi nếu không
    // tìm thấy sản phẩm nào có EAN này, hoặc không suy ra được màu từ tên sản phẩm. ───────────

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/rework/ean-color-lookup")]
    public async Task<IActionResult> ReworkEanColorLookup([FromQuery] string ean)
    {
        if (string.IsNullOrWhiteSpace(ean))
            return BadRequest(new { ok = false, error = "Thiếu mã EAN.", errorCode = "ean.missing" });

        string e = ean.Trim();
        try
        {
            string? color = await _viidoo.GetProductColorByEanAsync(e);
            if (string.IsNullOrEmpty(color))
                return BadRequest(new { ok = false, error = $"Không xác định được màu cho EAN '{e}'.", errorCode = "ean.colorNotFound", errorParams = new { ean = e } });

            return Ok(new { ok = true, data = new { ean = e, color } });
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: Rework — ghi log audit trail (SM_LabelPvidEANLog). Front-end gọi tại mỗi điểm kết
    // thúc 1 lần thử trong Process (1 Check Work Order fail, 2 Check Color & EAN of The Gift Box
    // fail, hoặc 3 Print Label pass/fail) — LUÔN insert dòng MỚI, không tìm/ghi đè dòng cũ, để
    // giữ lại toàn bộ lịch sử mọi lần thử (kể cả FAIL), giống SM_SNLabelScanLog ở trạm SnLabel. ──

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/rework/log-attempt")]
    public async Task<IActionResult> ReworkLogAttempt([FromBody] ReworkLogAttemptRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.WorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });

        var entry = new LabelPvidEanLog
        {
            WorkOrder = req.WorkOrder.Trim(),
            Pvid = req.Pvid?.Trim(),
            WoColor = req.WoColor?.Trim(),
            EanRw = req.EanRw?.Trim(),
            EanGiftBox = req.EanGiftBox?.Trim(),
            EanColor = req.EanColor?.Trim(),
            Status = string.IsNullOrWhiteSpace(req.Status) ? "FAIL" : req.Status.Trim(),
            FailedStep = req.FailedStep,
            // Bảng này lưu giờ Trung Quốc (UTC+8) theo yêu cầu riêng — sớm hơn giờ Việt Nam
            // (UTC+7, VietnamNow()) đúng 1 tiếng. Khác với mọi log khác trong Sakura (SnLabel,
            // Laser, Middle...) đều lưu giờ Việt Nam.
            Timeline = SakuraService.VietnamNow().AddHours(1)
        };

        try
        {
            _db.LabelPvidEanLogs.Add(entry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, BuildError(ex));
        }

        return Ok(new { ok = true, data = new { logId = entry.Id } });
    }

    // ── API: Rework — history (trang /sakura/rework/history), đọc từ SM_CartonUnpack_Log —
    // trạm Unpack/Repack mới (thay cho SM_LabelPvidEANLog của tính năng EAN/PVID Label cũ, tắt
    // đi nhưng KHÔNG xoá — GetReworkHistoryAsync/SM_LabelPvidEANLog vẫn còn nguyên trong service
    // nếu cần bật lại). ─────────────────────────────────────────────────────────────────────

    [HttpGet("/api/sakura/rework/history")]
    public async Task<IActionResult> ReworkHistory([FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? cartonNumber, [FromQuery] string? serialNumber, [FromQuery] string? reworkWorkOrder, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _snLabel.GetUnpackHistoryAsync(dateFrom, dateTo, cartonNumber, serialNumber, reworkWorkOrder, status, page, pageSize);
        return Ok(new { ok = true, data = result });
    }

    // ── API: Rework — Unpack Carton (bước 1). Quét 1 Carton Number đã in để xem N serial hiện
    // có, rồi gỡ k serial ra rework — xem SakuraService.GetCartonForUnpackAsync/UnpackCartonSerialsAsync. ──

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/rework/carton-lookup")]
    public async Task<IActionResult> UnpackCartonLookup([FromQuery] string cartonNumber)
    {
        try
        {
            var result = await _snLabel.GetCartonForUnpackAsync(cartonNumber);
            return Ok(new { ok = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/rework/unpack")]
    public async Task<IActionResult> UnpackCarton([FromBody] UnpackCartonRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.CartonNumber))
            return BadRequest(new { ok = false, error = "Thiếu Carton Number.", errorCode = "unpackCarton.cartonNumberMissing" });

        try
        {
            var result = await _snLabel.UnpackCartonSerialsAsync(req.CartonNumber, req.SerialNumbers ?? new List<string>());
            return Ok(new { ok = true, data = new { count = result.Count, serials = result.Select(x => x.SerialNumber) } });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    // ── API: Rework — Repack Carton (bước 3). Quét lại ĐỦ N serial gốc của carton, in lại tem
    // Carton với Condition=Refurb + SKU/PVID/EAN theo Rework WO — xem
    // SakuraService.GetRepackStatusAsync/BuildRepackCartonZplAsync/RecordRepackResultAsync. ─────

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/rework/repack-status")]
    public async Task<IActionResult> RepackStatus([FromQuery] string cartonNumber)
    {
        try
        {
            var result = await _snLabel.GetRepackStatusAsync(cartonNumber);
            return Ok(new { ok = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/rework/verify-repack-serial")]
    public async Task<IActionResult> VerifyRepackSerial([FromQuery] string cartonNumber, [FromQuery] string serial)
    {
        bool ok = await _snLabel.VerifyRepackSerialAsync(cartonNumber, serial);
        if (!ok)
            return BadRequest(new { ok = false, error = $"Serial '{serial}' không thuộc Carton '{cartonNumber}'.", errorCode = "unpackCarton.serialNotInCarton", errorParams = new { serial, cartonNumber } });
        return Ok(new { ok = true });
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/rework/repack-print")]
    public async Task<IActionResult> RepackPrint([FromBody] RepackPrintRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.CartonNumber) || string.IsNullOrWhiteSpace(req.ReworkWorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu Carton Number hoặc Rework WO.", errorCode = "common.missingData" });

        try
        {
            var wo = await _viidoo.SearchAsync(req.ReworkWorkOrder.Trim());
            if (wo == null)
                return BadRequest(new { ok = false, error = $"Không tìm thấy Work Order '{req.ReworkWorkOrder}' trên Odoo.", errorCode = "workOrder.notFoundOdoo", errorParams = new { wo = req.ReworkWorkOrder } });

            string? ean = wo.ProductId is int productId ? await _viidoo.GetProductEanAsync(productId) : null;

            string zpl = await _snLabel.BuildRepackCartonZplAsync(req.CartonNumber, req.ScannedSerials ?? new List<string>(), wo.ProductCode ?? "", ean);
            return Ok(new { ok = true, zpl, productCode = wo.ProductCode, ean, color = wo.Color });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/rework/repack-report-result")]
    public async Task<IActionResult> RepackReportResult([FromBody] RepackReportResultRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.CartonNumber) || string.IsNullOrWhiteSpace(req.ReworkWorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            await _snLabel.RecordRepackResultAsync(req.CartonNumber, req.ReworkWorkOrder);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: ghi log 1 lần Check EAN bị FAIL ngay ở bước quét EAN — bước này chặn không cho
    // quét sang Serial Number nên KHÔNG đi qua verify-serial, phải ghi log riêng ở đây,
    // nếu không lần fail EAN sẽ mất dấu hoàn toàn trong SM_SNLabelScanLog. ─────────────────

    [HttpPost("/api/sakura/snlabel/log-ean-fail")]
    public async Task<IActionResult> LogEanFail([FromBody] SnLabelEanFailLogRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.WorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });

        var logEntry = new SnLabelScanLog
        {
            WorkOrder = req.WorkOrder.Trim(),
            Ean = req.Ean?.Trim(),
            SerialNumber = "", // chưa quét tới Serial Number ở bước này
            Status = "FAIL",
            FailedStep = 1,
            // Log/audit trail thuần tuý -> lưu giờ Trung Quốc (UTC+8, sớm hơn giờ Việt Nam đúng
            // 1 tiếng) theo yêu cầu riêng. KHÔNG áp dụng cho dữ liệu nghiệp vụ thật (SnLabelPrint,
            // CartonSnScanLog...) — những bảng đó vẫn giữ nguyên giờ Việt Nam.
            Timeline = SakuraService.VietnamNow().AddHours(1)
        };

        try
        {
            _db.SnLabelScanLogs.Add(logEntry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, BuildError(ex));
        }

        return Ok(new { ok = true, data = new { logId = logEntry.Id } });
    }

    // ── API: verify EAN + Color + Serial (Process: Check EAN -> Check Color & Serial
    // Number -> Print Label) — MỖI lần quét đều ghi 1 dòng MỚI vào SM_SNLabelScanLog (audit
    // trail đầy đủ, kể cả các lần FAIL — không ghi đè), trả về ZPL sẵn sàng in cho đúng
    // SerialNumber đã quét nếu pass hết 3 bước đầu. SM_SNLabelPrint (kho nhãn đã in thật +
    // tracking Reprint) chỉ nhận dòng mới ở ReportSnLabelPrintResult, sau khi in thành công. ──

    [HttpPost("/api/sakura/snlabel/verify-serial")]
    public async Task<IActionResult> VerifySnLabelSerial([FromBody] SnLabelVerifyRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.SerialNumber))
            return BadRequest(new { ok = false, error = "Thiếu Serial Number.", errorCode = "serial.missing" });
        if (string.IsNullOrWhiteSpace(req.WorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });
        if (req.ProductId <= 0)
            return BadRequest(new { ok = false, error = "Thiếu Product ID — vui lòng lookup lại Work Order.", errorCode = "serial.missingProductId" });
        if (string.IsNullOrWhiteSpace(req.ExpectedColor))
            return BadRequest(new { ok = false, error = "Thiếu màu Work Order để đối chiếu.", errorCode = "serial.missingExpectedColor" });

        string serial = req.SerialNumber.Trim();
        string workOrder = req.WorkOrder.Trim();
        string ean = req.Ean?.Trim() ?? "";

        // Serial đã in THÀNH CÔNG trước đó (dòng trong SM_SNLabelPrint có Status = PASS, hoặc
        // NULL = dòng cũ từ flow tự sinh serial trước đây) -> chặn in trùng. Muốn in lại thì
        // phải qua tab Reprint (không đi qua endpoint này). Không được chặn theo kiểu "có dòng
        // là chặn" — dữ liệu cũ (trước khi tách SM_SNLabelScanLog) có thể còn sót dòng
        // FAIL/PENDING trong chính bảng này, không phải là "đã in".
        bool alreadyPrinted = await _db.SnLabelPrints.AnyAsync(x => x.SerialNumber == serial && (x.Status == null || x.Status == "PASS"));
        if (alreadyPrinted)
            return Conflict(new { ok = false, error = $"Serial '{serial}' đã được in trước đó.", errorCode = "serial.alreadyPrinted", errorParams = new { serial } });

        if (!SakuraService.TryParseSerialParts(serial, out string variant, out string line, out string runningNumber, out int runningNumberInt))
            return BadRequest(new { ok = false, error = $"Serial '{serial}' không đúng định dạng.", errorCode = "serial.invalidFormat", errorParams = new { serial } });

        int? failedStep = null;
        string? failMessage = null;
        bool eanOk, colorOk = false;

        // Bước 1: Check EAN — đối chiếu lại với Odoo (không tin mã client đã tự so khớp trước đó).
        string? realEan;
        try
        {
            realEan = await _viidoo.GetProductEanAsync(req.ProductId);
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
        eanOk = !string.IsNullOrEmpty(realEan) && string.Equals(realEan!.Trim(), ean, StringComparison.OrdinalIgnoreCase);
        if (!eanOk)
        {
            failedStep = 1;
            failMessage = "Mã EAN không khớp với Work Order.";
        }

        // Bước 2: Check Color — màu embed trong Serial (theo TryResolveColorFromSerial) phải
        // khớp màu của Work Order.
        if (failedStep == null)
        {
            string? serialColor = SakuraService.TryResolveColorFromSerial(serial);
            colorOk = serialColor != null && string.Equals(serialColor, req.ExpectedColor.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!colorOk)
            {
                failedStep = 2;
                failMessage = "Màu của Serial không khớp với Work Order.";
            }
        }

        // failedStep 1/2 dừng ở đây — bước "Enter Production Result" (Process step 3, chia
        // nhỏ 3.1 Check / 3.2 Enter) và Print Label (step 4) đi qua 2 endpoint riêng
        // (EnterProductionResult / report-print-result) để Try Again có thể retry ĐÚNG 1 bước
        // đang fail, không phải chạy lại từ đầu (EAN/Color check ở trên là tất định).
        string status = failedStep != null ? "FAIL" : "PENDING";
        string zpl = "";
        if (failedStep == null)
        {
            string template = await _snLabel.GetZplTemplateAsync("SnLabel");
            zpl = SakuraService.BuildConcatenatedZpl(template, new[] { serial });
        }

        // Luôn insert dòng MỚI — không tìm/ghi đè dòng cũ, để giữ lại toàn bộ lịch sử mọi
        // lần quét (kể cả các lần FAIL trước đó của cùng 1 serial).
        var logEntry = new SnLabelScanLog
        {
            WorkOrder = workOrder,
            Ean = ean,
            SerialNumber = serial,
            Model = SakuraService.Model,
            Variant = variant,
            Color = req.ExpectedColor.Trim(),
            ProductionLine = line,
            RunningNumber = runningNumber,
            RunningNumberInt = runningNumberInt,
            Status = status,
            FailedStep = failedStep,
            // Log/audit trail thuần tuý -> lưu giờ Trung Quốc (UTC+8), xem chú thích ở LogEanFail.
            Timeline = SakuraService.VietnamNow().AddHours(1)
        };

        try
        {
            _db.SnLabelScanLogs.Add(logEntry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, BuildError(ex));
        }

        return Ok(new
        {
            ok = true,
            data = new
            {
                serialNumber = serial,
                status,
                failedStep,
                failMessage,
                eanOk,
                colorOk,
                logId = logEntry.Id,
                zpl
            }
        });
    }

    // Lấy số thứ tự WO con lớn nhất HIỆN CÓ (chưa +1) của master_wo_code này từ
    // SVN_ProductionInputLogs — dùng để tính subName tiếp theo cho bước Enter Production
    // Result. Giống hệt BackPanelController.GetCurrentMaxSubNameSuffixAsync (trạm Laser).
    private async Task<int> GetCurrentMaxSubNameSuffixAsync(string masterWoCode)
    {
        return await _db.Database
            .SqlQuery<int>($@"
                SELECT ISNULL(MAX(
                    CASE WHEN CHARINDEX('-', REVERSE(wo_code)) > 0
                         THEN TRY_CAST(RIGHT(wo_code, CHARINDEX('-', REVERSE(wo_code)) - 1) AS INT)
                         ELSE NULL END
                ), 0) AS Value
                FROM [svn_pentaho].[dbo].[SVN_ProductionInputLogs]
                WHERE master_wo_code = {masterWoCode}")
            .SingleAsync();
    }

    // Lấy số thứ tự WO con lớn nhất hiện có của master_wo_code này, +1, để tính subName tiếp
    // theo ("{masterWoCode}-{max+1:000}"). Ném exception nếu query fail — caller phải dừng lại,
    // không được tự đoán subName khi mất kết nối DB.
    private async Task<string> BuildNextSubNameAsync(string masterWoCode)
    {
        int maxSuffix = await GetCurrentMaxSubNameSuffixAsync(masterWoCode);
        return $"{masterWoCode}-{(maxSuffix + 1):D3}";
    }

    // ── API: Process step 3 "Enter Production Result" — chỉ gọi sau khi verify-serial trả
    // status="PENDING" (đã pass Check EAN + Check Color + Check Serial) với đúng logId vừa
    // tạo. Tách riêng khỏi verify-serial để Try Again khi bước này fail chỉ cần gọi lại ĐÚNG
    // endpoint này (không phải verify lại EAN/Color/Serial — vốn đã pass và tất định). ─────

    [HttpPost("/api/sakura/snlabel/enter-production-result")]
    public async Task<IActionResult> EnterProductionResult([FromBody] SnLabelEnterProductionResultRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });
        if (string.IsNullOrWhiteSpace(req.WorkOrder) || string.IsNullOrWhiteSpace(req.SerialNumber))
            return BadRequest(new { ok = false, error = "Thiếu Work Order hoặc Serial Number.", errorCode = "common.missingData" });

        var entry = await _db.SnLabelScanLogs.FindAsync(req.LogId);
        if (entry == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy log Id={req.LogId}.", errorCode = "common.missingData" });

        string workOrder = req.WorkOrder.Trim();
        string serial = req.SerialNumber.Trim();

        // Bước 3.1: Check đã nhập KQSX (ProductionInputLog) cho serial này chưa — dùng
        // CheckLotSerialFG của trạm Laser, ĐÚNG ý nghĩa gốc của nó (ok=true nghĩa là CHƯA
        // nhập). Khác trạm Laser: ở đây "đã nhập rồi" (ok=false) là FAIL — vì trạm SnLabel tự
        // đảm nhiệm việc nhập, không cần chờ trạm khác — trạng thái vĩnh viễn với serial này
        // (không khoá Try Again, chỉ xóa Serial Number rồi cho quét serial tiếp theo ngay, EAN
        // giữ nguyên — xem $btnSnlTryAgain/restartSerialOnly phía client).
        var checkResult = await _productionApi.CheckLotSerialFgAsync(req.ProductId, serial);
        if (!checkResult.Ok)
        {
            entry.Status = "FAIL";
            entry.FailedStep = 5; // 3.1 — xem chú thích FailedStep ở SnLabelScanLog.
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, BuildError(ex));
            }

            return Ok(new
            {
                ok = true,
                data = new { checkResultOk = false, checkResultMessage = checkResult.Message, failedStep = 5, logId = entry.Id }
            });
        }

        // Bước 3.2: Nhập KQSX — "products" phải kèm 1 phần tử tự tham chiếu chính sản
        // phẩm/serial đang xử lý (yêu cầu riêng của API cho trạm này, khác trạm Laser).
        string? subName = null;
        const int maxAttempts = 3;
        Exception? lastEx = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                subName = await BuildNextSubNameAsync(workOrder);
                lastEx = null;
                break;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (attempt < maxAttempts) await Task.Delay(1000);
            }
        }

        bool inputResultOk;
        string? inputResultMessage = null;
        if (lastEx != null)
        {
            Console.WriteLine($"[Sakura] Loi query SVN_ProductionInputLogs de tinh subName (sau {maxAttempts} lan thu): {lastEx.Message}");
            inputResultOk = false;
            inputResultMessage = "Không lấy được số thứ tự WO từ database.";
        }
        else
        {
            var inputResult = await _productionApi.InputProductionResultLogAsync(
                workOrder, subName!, serial, req.ProductId, req.TotalQuantity ?? 0,
                new[] { new ProductionResultProduct { Product_id = req.ProductId, Has_tracking = "serial", Serial_code = serial } });
            inputResultOk = inputResult.Ok;
            inputResultMessage = inputResult.Message;
            if (!inputResult.Ok) subName = null; // API nhập fail -> không tính là đã dùng subName này.
        }

        entry.ProductionResultSubName = subName;
        entry.Status = inputResultOk ? "PENDING" : "FAIL";
        entry.FailedStep = inputResultOk ? null : 6; // 3.2 — xem chú thích FailedStep ở SnLabelScanLog.

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, BuildError(ex));
        }

        return Ok(new
        {
            ok = true,
            data = new
            {
                checkResultOk = true,
                inputResultOk,
                inputResultMessage,
                subName,
                failedStep = entry.FailedStep,
                logId = entry.Id
            }
        });
    }

    // ── API: trình duyệt báo kết quả in nhãn cục bộ qua bridge — chỉ gọi sau khi
    // verify-serial trả status="PENDING" (đã pass Check EAN + Check Color + Check Serial).
    // Cập nhật dòng log (logId) + nếu in thành công, tạo luôn dòng "đã in thật" trong
    // SM_SNLabelPrint (kho nhãn, dùng cho tính remaining quantity + tracking Reprint). ──────

    [HttpPost("/api/sakura/snlabel/report-print-result")]
    public async Task<IActionResult> ReportSnLabelPrintResult([FromBody] SnLabelReportPrintResultRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        var entry = await _db.SnLabelScanLogs.FindAsync(req.LogId);
        if (entry == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy log Id={req.LogId}.", errorCode = "common.missingData" });

        entry.Status = req.Success ? "PASS" : "FAIL";
        entry.FailedStep = req.Success ? null : 4;

        if (req.Success)
        {
            _db.SnLabelPrints.Add(new SnLabelPrint
            {
                SerialNumber = entry.SerialNumber,
                Model = entry.Model ?? SakuraService.Model,
                Variant = entry.Variant ?? "",
                Color = entry.Color ?? "",
                ProductionLine = entry.ProductionLine ?? "",
                ProductionDate = SakuraService.VietnamNow().AddHours(1).Date,
                RunningNumber = entry.RunningNumber ?? "",
                RunningNumberInt = entry.RunningNumberInt ?? 0,
                PrintedAt = SakuraService.VietnamNow().AddHours(1),
                BatchId = Guid.NewGuid(),
                WorkOrder = entry.WorkOrder,
                Ean = entry.Ean,
                Status = "PASS",
                FailedStep = null
            });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, BuildError(ex));
        }

        return Ok(new { ok = true });
    }

    // ── API: print ────────────────────────────────────────────────────────────

    [HttpPost("/api/sakura/snlabel/print")]
    public async Task<IActionResult> Print([FromBody] SnLabelPrintRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        // Không tin số lượng WO mà client gửi lên — tự tra lại tổng số lượng thật từ
        // Odoo ngay lúc in, để không ai (kể cả gọi thẳng API, bỏ qua UI) có thể né
        // giới hạn "còn lại bao nhiêu" của Work Order.
        int? workOrderTotalQuantity = null;
        if (!string.IsNullOrWhiteSpace(req.WorkOrder))
        {
            try
            {
                var woResult = await _viidoo.SearchAsync(req.WorkOrder.Trim());
                if (woResult == null || woResult.Quantity is not decimal woQty || woQty < 1)
                {
                    string wo = req.WorkOrder.Trim();
                    return BadRequest(new { ok = false, error = $"Không xác định được tổng số lượng của Work Order '{wo}'.", errorCode = "workOrder.totalUnavailable", errorParams = new { wo } });
                }
                workOrderTotalQuantity = (int)woQty;
            }
            catch (Exception ex)
            {
                return BadRequest(BuildError(ex));
            }
        }

        List<SnLabelPrint> rows;
        try
        {
            rows = await _snLabel.GenerateNextSerialsAsync(req.Date, req.Variant, req.Line, req.Quantity, req.PrintedBy, req.WorkOrder, workOrderTotalQuantity);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }

        string template = await _snLabel.GetZplTemplateAsync("SnLabel");
        string zpl = SakuraService.BuildConcatenatedZpl(template, rows.Select(r => r.SerialNumber));
        string printMode = _config["Sakura:SnLabel:PrintMode"] ?? "FileDownload";

        var response = new SnLabelPrintResponse
        {
            Ok = true,
            BatchId = rows[0].BatchId,
            Serials = rows.Select(r => new SnLabelSerialDto
            {
                SerialNumber = r.SerialNumber,
                RunningNumber = r.RunningNumber,
                RunningNumberInt = r.RunningNumberInt
            }).ToList(),
            Zpl = zpl,
            PrintMode = printMode
        };

        if (string.Equals(printMode, "DirectTcp", StringComparison.OrdinalIgnoreCase))
        {
            string printerIp = _config["Sakura:SnLabel:PrinterIp"] ?? "";
            int printerPort = int.TryParse(_config["Sakura:SnLabel:PrinterPort"], out var p) ? p : 9100;

            if (string.IsNullOrWhiteSpace(printerIp))
            {
                response.DirectPrintSent = false;
                response.DirectPrintError = "Chưa cấu hình IP máy in (Sakura:SnLabel:PrinterIp trong appsettings.json).";
            }
            else
            {
                try
                {
                    await _snLabel.SendZplAsync(printerIp, printerPort, zpl);
                    response.DirectPrintSent = true;
                }
                catch (Exception ex)
                {
                    response.DirectPrintSent = false;
                    response.DirectPrintError = ex.Message;
                }
            }
        }

        return Ok(response);
    }

    // ── API: reprint by serial (Manual mode) ────────────────────────────────────

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/snlabel/reprint")]
    public async Task<IActionResult> Reprint([FromQuery] string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return BadRequest(new { ok = false, error = "Thiếu Serial Number.", errorCode = "reprint.missingSerial" });

        // Đánh dấu reprint ngay trên dòng gốc (tăng ReprintCount, ghi lại thời điểm) —
        // không tạo dòng mới nên không đụng tới bộ đếm RunningNumber.
        var row = await _snLabel.MarkReprintedAsync(serialNumber, reprintedBy: null);
        if (row == null)
        {
            string sn = serialNumber.Trim();
            return NotFound(new { ok = false, error = $"Không tìm thấy serial '{sn}'.", errorCode = "reprint.notFound", errorParams = new { serial = sn } });
        }

        string template = await _snLabel.GetZplTemplateAsync("SnLabel");
        string zpl = SakuraService.BuildConcatenatedZpl(template, new[] { row.SerialNumber });

        return Ok(new
        {
            ok = true,
            data = new
            {
                row.SerialNumber,
                row.Variant,
                row.Color,
                row.ProductionLine,
                row.ProductionDate,
                row.WorkOrder,
                row.PrintedAt,
                row.ReprintCount,
                zpl
            }
        });
    }

    // ── API: SN Label — reprint dưới Rework WO (mode "Rework" ở trang /sakura/snlabel). Reprint
    // tem gift box Y NGUYÊN nội dung cho 1 serial đã qua Unpack (đang chờ rework ở
    // /sakura/rework) — gắn ReworkWorkOrder trên dòng SnLabelPrint, insert 1 dòng vào
    // SM_SerialNumber_Rework TRƯỚC khi nhập kết quả sản xuất dưới Rework WO (hệ thống nhập kết
    // quả sản xuất bên ngoài tự check bảng đó để bỏ qua validate trùng serial), rồi chuyển dòng
    // Unpack Log tương ứng UNPACKED -> REWORKED. ───────────────────────────────────────────────

    // Ghi 1 dòng MỚI vào SM_SNLabelScanLog cho lần thử Rework này — cùng bảng/cùng quy ước
    // FailedStep với luồng Work Order bình thường (1=Check EAN, 2=Check Color, 4=Print Label,
    // 6=nhập KQSX lỗi) để trang /sakura/snlabel/history hiển thị đúng nhãn có sẵn, không cần
    // sửa gì thêm ở đó. Trả về entity đã lưu (có Id) để các bước sau (retry KQSX/report kết quả
    // in) cập nhật TẠI CHỖ, không insert dòng mới — đúng 1 dòng/1 lần thử, y hệt verify-serial.
    private async Task<SnLabelScanLog> LogReworkAttemptAsync(string reworkWo, string? ean, string serial, string? color, string status, int? failedStep)
    {
        SakuraService.TryParseSerialParts(serial, out string variant, out string line, out string runningNumber, out int runningNumberInt);
        var entry = new SnLabelScanLog
        {
            WorkOrder = reworkWo,
            Ean = ean,
            SerialNumber = serial,
            Model = SakuraService.Model,
            Variant = variant,
            Color = color,
            ProductionLine = line,
            RunningNumber = runningNumber,
            RunningNumberInt = runningNumberInt,
            Status = status,
            FailedStep = failedStep,
            Timeline = SakuraService.VietnamNow().AddHours(1)
        };
        _db.SnLabelScanLogs.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/snlabel/rework-reprint")]
    public async Task<IActionResult> ReworkReprintSnLabel([FromBody] ReworkReprintRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.SerialNumber) || string.IsNullOrWhiteSpace(req.ReworkWorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu Serial Number hoặc Rework WO.", errorCode = "common.missingData" });

        string serial = req.SerialNumber.Trim();
        string reworkWo = req.ReworkWorkOrder.Trim();
        string scannedEan = (req.Ean ?? "").Trim();

        var pending = await _snLabel.FindPendingUnpackLogAsync(serial);
        if (pending == null)
            return BadRequest(new { ok = false, error = $"Serial '{serial}' chưa được Unpack hoặc đã qua Rework rồi.", errorCode = "unpackCarton.serialNotPending", errorParams = new { serial } });

        var wo = await _viidoo.SearchAsync(reworkWo);
        if (wo == null || wo.ProductId is not int productId)
            return BadRequest(new { ok = false, error = $"Không tìm thấy Work Order '{reworkWo}' trên Odoo.", errorCode = "workOrder.notFoundOdoo", errorParams = new { wo = reworkWo } });

        // Re-check EAN ngay tại server (không tin riêng client đã so khớp) — giống bước Check EAN
        // của tab Work Order (verify-serial), áp dụng cho đúng Rework WO đang dùng.
        string? realEan = await _viidoo.GetProductEanAsync(productId);
        if (string.IsNullOrEmpty(realEan) || !string.Equals(realEan.Trim(), scannedEan, StringComparison.OrdinalIgnoreCase))
        {
            await LogReworkAttemptAsync(reworkWo, scannedEan, serial, wo.Color, "FAIL", 1);
            return BadRequest(new { ok = false, error = $"EAN '{scannedEan}' không khớp với Rework WO '{reworkWo}'.", errorCode = "rework.eanMismatch", errorParams = new { ean = scannedEan, expected = realEan ?? "" } });
        }

        // Check Color — màu embed trong Serial (theo TryResolveColorFromSerial) phải khớp màu của
        // Rework WO, giống hệt Bước 2 "Check Color" của luồng verify-serial thông thường. EAN đúng
        // không đủ đảm bảo — quét nhầm 1 serial khác màu (nhưng gõ đúng EAN theo trí nhớ) vẫn phải
        // bị chặn ở đây.
        string? serialColor = SakuraService.TryResolveColorFromSerial(serial);
        if (serialColor == null || !string.Equals(serialColor, wo.Color, StringComparison.OrdinalIgnoreCase))
        {
            await LogReworkAttemptAsync(reworkWo, scannedEan, serial, wo.Color, "FAIL", 2);
            return BadRequest(new { ok = false, error = $"Màu của Serial '{serial}' ({serialColor ?? "?"}) không khớp với Rework WO '{reworkWo}' ({wo.Color}).", errorCode = "rework.colorMismatch", errorParams = new { serial, actual = serialColor ?? "", expected = wo.Color ?? "" } });
        }

        var printRow = await _snLabel.MarkReworkedReprintAsync(serial, reworkWo, reprintedBy: null);
        if (printRow == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy tem SN Label đã in cho serial '{serial}'.", errorCode = "reprint.notFound", errorParams = new { serial } });

        // Insert TRƯỚC khi gọi bước nhập kết quả sản xuất, đúng yêu cầu.
        _db.SerialNumberReworks.Add(new SerialNumberRework
        {
            WorkOrder = reworkWo,
            SerialNumber = serial,
            CreatedAt = SakuraService.VietnamNow().AddHours(1),
            ReworkType = wo.DropReworkType
        });
        await _db.SaveChangesAsync();

        // EAN + Color đã pass -> ghi 1 dòng PENDING (giống verify-serial thường), các bước sau
        // (retry KQSX / report kết quả in) cập nhật TIẾP trên đúng dòng này, không insert mới.
        var logEntry = await LogReworkAttemptAsync(reworkWo, scannedEan, serial, wo.Color, "PENDING", null);

        // Nhập kết quả sản xuất dưới Rework WO — KHÔNG gọi CheckLotSerialFgAsync (bỏ qua bước
        // Check trùng): serial này chắc chắn đã có bản ghi KQSX từ lần sản xuất gốc (đó là lý do
        // nó tồn tại để rework), Check trùng chắc chắn sẽ báo "đã nhập" và chặn mất Input. Gọi
        // thẳng InputProductionResultLogAsync để thêm 1 bản ghi KQSX MỚI song song, dưới Rework
        // WO (khác WO gốc) — dòng vừa insert vào SM_SerialNumber_Rework ở trên là để hệ thống
        // nhập KQSX bên ngoài (nếu có validate trùng ở phía họ) biết đây là serial rework hợp lệ.
        // Fail ở đây -> giống bước 3.2 của luồng thường: KHOÁ lại (Try Again/Skip phía client),
        // KHÔNG cho in tem cho tới khi KQSX vào được (hoặc Skip bỏ qua serial này).
        bool inputResultOk;
        string? inputResultMessage;
        try
        {
            string subName = await BuildNextSubNameAsync(reworkWo);
            var inputResult = await _productionApi.InputProductionResultLogAsync(
                reworkWo, subName, serial, productId, 0,
                new[] { new ProductionResultProduct { Product_id = productId, Has_tracking = "serial", Serial_code = serial } });
            inputResultOk = inputResult.Ok;
            inputResultMessage = inputResult.Message;
        }
        catch (Exception ex)
        {
            inputResultOk = false;
            inputResultMessage = ex.Message;
        }

        if (!inputResultOk)
        {
            logEntry.Status = "FAIL";
            logEntry.FailedStep = 6;
            await _db.SaveChangesAsync();
        }

        // KHÔNG đánh dấu REWORKED ở đây — trình duyệt còn phải gửi ZPL tới bridge cục bộ mới
        // thật sự "in xong". Chỉ chuyển UNPACKED -> REWORKED sau khi client báo in THÀNH CÔNG
        // qua /api/sakura/snlabel/rework-reprint-report-result (xem ReworkReprintReportResult).
        // In lỗi thì Unpack Log giữ nguyên UNPACKED, operator quét lại đúng serial này để thử
        // lại là an toàn (gọi lại endpoint này không phá dữ liệu gì, chỉ tăng ReprintCount/insert
        // thêm dòng SM_SerialNumber_Rework).
        string reworkTemplate = await _snLabel.GetZplTemplateAsync("SnLabel");
        string reworkZpl = SakuraService.BuildConcatenatedZpl(reworkTemplate, new[] { serial });

        return Ok(new
        {
            ok = true,
            data = new
            {
                serialNumber = serial,
                reworkWorkOrder = reworkWo,
                printRow.Color,
                printRow.ReprintCount,
                inputResultOk,
                inputResultMessage,
                unpackLogId = pending.Id,
                logId = logEntry.Id,
                zpl = reworkZpl
            }
        });
    }

    // ── API: retry riêng bước nhập KQSX (3.2) khi lần đầu fail — KHÔNG re-check EAN/Color (đã
    // pass, tất định), KHÔNG insert lại SM_SerialNumber_Rework (đã có từ lần gọi rework-reprint
    // đầu tiên) — chỉ gọi lại InputProductionResultLogAsync, cập nhật TẠI CHỖ dòng log (logId)
    // đã tạo ở ReworkReprintSnLabel. ─────────────────────────────────────────────────────────────

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/snlabel/rework-reprint-retry-kqsx")]
    public async Task<IActionResult> ReworkRetryKqsx([FromBody] ReworkRetryKqsxRequest req)
    {
        if (req == null || req.LogId <= 0 || string.IsNullOrWhiteSpace(req.SerialNumber) || string.IsNullOrWhiteSpace(req.ReworkWorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        var entry = await _db.SnLabelScanLogs.FindAsync(req.LogId);
        if (entry == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy log Id={req.LogId}.", errorCode = "common.missingData" });

        string serial = req.SerialNumber.Trim();
        string reworkWo = req.ReworkWorkOrder.Trim();

        var wo = await _viidoo.SearchAsync(reworkWo);
        if (wo == null || wo.ProductId is not int productId)
            return BadRequest(new { ok = false, error = $"Không tìm thấy Work Order '{reworkWo}' trên Odoo.", errorCode = "workOrder.notFoundOdoo", errorParams = new { wo = reworkWo } });

        bool inputResultOk;
        string? inputResultMessage;
        try
        {
            string subName = await BuildNextSubNameAsync(reworkWo);
            var inputResult = await _productionApi.InputProductionResultLogAsync(
                reworkWo, subName, serial, productId, 0,
                new[] { new ProductionResultProduct { Product_id = productId, Has_tracking = "serial", Serial_code = serial } });
            inputResultOk = inputResult.Ok;
            inputResultMessage = inputResult.Message;
        }
        catch (Exception ex)
        {
            inputResultOk = false;
            inputResultMessage = ex.Message;
        }

        entry.Status = inputResultOk ? "PENDING" : "FAIL";
        entry.FailedStep = inputResultOk ? null : 6;
        await _db.SaveChangesAsync();

        return Ok(new { ok = true, data = new { inputResultOk, inputResultMessage } });
    }

    // ── API: báo kết quả in thật của rework-reprint (SAU KHI trình duyệt gửi ZPL tới bridge cục
    // bộ) — CHỈ khi Success mới chuyển Unpack Log UNPACKED -> REWORKED + chốt log Status=PASS.
    // In lỗi thì log Status=FAIL/FailedStep=4 (giống "Print Label" của luồng thường) nhưng Unpack
    // Log vẫn giữ UNPACKED — xem chú thích ở ReworkReprintSnLabel. ─────────────────────────────

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/snlabel/rework-reprint-report-result")]
    public async Task<IActionResult> ReworkReprintReportResult([FromBody] ReworkReprintReportResultRequest req)
    {
        if (req == null || req.UnpackLogId <= 0 || string.IsNullOrWhiteSpace(req.ReworkWorkOrder))
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        var entry = await _db.SnLabelScanLogs.FindAsync(req.LogId);
        if (entry != null)
        {
            entry.Status = req.Success ? "PASS" : "FAIL";
            entry.FailedStep = req.Success ? null : 4;
        }

        if (!req.Success)
        {
            await _db.SaveChangesAsync();
            return Ok(new { ok = true, data = new { marked = false } });
        }

        try
        {
            string reworkWo = req.ReworkWorkOrder.Trim();
            var wo = await _viidoo.SearchAsync(reworkWo);
            await _snLabel.MarkSerialReworkedAsync(req.UnpackLogId, reworkWo, wo?.DropReworkType);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true, data = new { marked = true } });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: history ──────────────────────────────────────────────────────────

    [HttpGet("/api/sakura/snlabel/history")]
    public async Task<IActionResult> History([FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? workOrder, [FromQuery] string? serialNumber, [FromQuery] string? ean, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _snLabel.GetHistoryAsync(dateFrom, dateTo, workOrder, serialNumber, ean, page, pageSize);
        return Ok(new { ok = true, data = result });
    }

    // Danh sách Work Order để đổ vào dropdown filter — nếu có ngày thì chỉ lấy WO của
    // đúng ngày đó (vd ngày 9 có 3 WO thì dropdown chỉ hiện đúng 3 WO đó).
    [HttpGet("/api/sakura/snlabel/workorders")]
    public async Task<IActionResult> GetWorkOrders([FromQuery] DateTime? date)
    {
        var list = await _snLabel.GetWorkOrdersAsync(date);
        return Ok(new { ok = true, data = list });
    }

    // ── API: re-download ZPL file for a batch ────────────────────────────────

    [HttpGet("/api/sakura/snlabel/download/{batchId:guid}")]
    public async Task<IActionResult> Download(Guid batchId)
    {
        var rows = await _snLabel.GetByBatchAsync(batchId);
        if (rows.Count == 0)
            return NotFound(new { ok = false, error = "Không tìm thấy batch." });

        string template = await _snLabel.GetZplTemplateAsync("SnLabel");
        string zpl = SakuraService.BuildConcatenatedZpl(template, rows.Select(r => r.SerialNumber));
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(zpl);
        return File(bytes, "text/plain", $"sn-labels-{batchId}.zpl");
    }

    // ── API: Carton SN Label — Work Order lookup (lấy Color + Total Quantity từ Odoo, giống
    // SnLabel — tính thêm Printed/Remaining từ SM_Sakura_CartonLabel_Data + ExpectedQuantity cho
    // carton hiện tại: đủ hộp (CartonPcsPerCarton) hay lẻ hộp (phần dư còn lại)). ─────────────

    [HttpGet("/api/sakura/cartonsn/workorder-lookup")]
    public async Task<IActionResult> CartonWorkOrderLookup([FromQuery] string workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });

        string wo = workOrder.Trim();
        try
        {
            var result = await _viidoo.SearchAsync(wo);
            if (result == null)
                return BadRequest(new { ok = false, error = $"Không tìm thấy Work Order '{wo}' trên Odoo.", errorCode = "workOrder.notFoundOdoo", errorParams = new { wo } });

            if (string.IsNullOrWhiteSpace(result.Color))
                return BadRequest(new { ok = false, error = $"Không xác định được màu cho Work Order '{wo}'.", errorCode = "workOrder.colorUnknown", errorParams = new { wo } });

            if (result.Quantity is not decimal qty || qty < 1)
                return BadRequest(new { ok = false, error = $"Work Order '{wo}' không có số lượng hợp lệ.", errorCode = "workOrder.invalidQuantity", errorParams = new { wo } });

            string? variant = SakuraService.TryResolveVariantFromColor(result.Color);
            if (variant == null)
                return BadRequest(new { ok = false, error = $"Không nhận diện được màu '{result.Color}' trả về từ Odoo.", errorCode = "workOrder.unresolvedColor", errorParams = new { color = result.Color } });

            string color = SakuraService.ResolveColor(variant);
            if (!ZplTemplates.CartonColorMeta.ContainsKey(color))
                return BadRequest(new { ok = false, error = $"Màu '{color}' chưa có cấu hình Carton Label.", errorCode = "cartonLabel.unknownColor", errorParams = new { color } });

            int totalQuantity = (int)qty;
            int printedQuantity = await _snLabel.GetCartonWorkOrderPrintedCountAsync(wo);
            int remainingQuantity = Math.Max(0, totalQuantity - printedQuantity);

            if (remainingQuantity <= 0)
                return BadRequest(new { ok = false, error = $"Work Order '{wo}' đã in đủ số lượng ({printedQuantity}/{totalQuantity}).", errorCode = "workOrder.exhausted", errorParams = new { wo, printed = printedQuantity, total = totalQuantity } });

            int expectedQuantity = Math.Min(remainingQuantity, SakuraService.CartonPcsPerCarton);
            int totalCarton = (int)Math.Ceiling(totalQuantity / (double)SakuraService.CartonPcsPerCarton);
            int remainingCarton = (int)Math.Ceiling(remainingQuantity / (double)SakuraService.CartonPcsPerCarton);

            string? ean = result.ProductId is int cartonWoProductId ? await _viidoo.GetProductEanAsync(cartonWoProductId) : null;

            var response = new CartonWorkOrderLookupResponse
            {
                WorkOrder = wo,
                Color = color,
                TotalQuantity = totalQuantity,
                PrintedQuantity = printedQuantity,
                RemainingQuantity = remainingQuantity,
                ExpectedQuantity = expectedQuantity,
                TotalCarton = totalCarton,
                RemainingCarton = remainingCarton,
                ProductId = result.ProductId,
                ProductCode = result.ProductCode,
                Ean = ean
            };
            return Ok(new { ok = true, data = response });
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: Carton SN Label — verify 1 serial NGAY lúc quét vào 1 ô SN (định dạng + đã
    // quét/in ở carton khác chưa) — check trùng trong lần quét hiện tại do client tự làm bằng
    // string/array, không cần gọi server. ───────────────────────────────────────────────────

    [HttpGet("/api/sakura/cartonsn/verify-serial")]
    public async Task<IActionResult> CartonVerifySerial([FromQuery] string serial, [FromQuery] string? color)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { ok = false, error = "Thiếu Serial Number.", errorCode = "serial.missing" });

        var (ok, errorCode, message) = await _snLabel.ValidateCartonSerialAsync(serial.Trim(), color);
        if (!ok)
        {
            var body = new { ok = false, error = message, errorCode };
            return errorCode == "cartonLabel.serialAlreadyUsed" ? Conflict(body) : BadRequest(body);
        }

        return Ok(new { ok = true });
    }

    // ── API: Carton SN Label — verify Carton Number NGAY lúc nhập/quét (chặn nhập trùng — mỗi
    // carton là 1 định danh vật lý riêng, đã in rồi thì không cho dùng lại). ───────────────────

    [HttpGet("/api/sakura/cartonsn/verify-carton-number")]
    public async Task<IActionResult> CartonVerifyCartonNumber([FromQuery] string cartonNumber)
    {
        if (string.IsNullOrWhiteSpace(cartonNumber))
            return BadRequest(new { ok = false, error = "Thiếu Carton Number.", errorCode = "cartonLabel.cartonNumberMissing" });

        string trimmed = cartonNumber.Trim();
        bool alreadyUsed = await _snLabel.IsCartonNumberAlreadyUsedAsync(trimmed);
        if (alreadyUsed)
            return Conflict(new { ok = false, error = $"Carton Number '{trimmed}' đã được sử dụng trước đó.", errorCode = "cartonLabel.cartonNumberAlreadyUsed", errorParams = new { cartonNumber = trimmed } });

        return Ok(new { ok = true });
    }

    // ── API: Carton SN Label print ───────────────────────────────────────────

    [HttpPost("/api/sakura/cartonsn/print")]
    public async Task<IActionResult> CartonSnPrint([FromBody] CartonLabelPrintRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            string zpl = await _snLabel.BuildCartonLabelZplAsync(req.CartonNumber, req.Color, req.Condition, req.SerialNumbers, req.WorkOrder, req.PalletId);
            return Ok(new { ok = true, zpl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    // ── API: Carton SN Label — báo đã in thành công (SAU KHI trình duyệt gửi ZPL tới bridge
    // cục bộ) — lưu các serial không rỗng vào SM_Sakura_CartonLabel_Data. KHÔNG gọi từ Preview ZPL,
    // chỉ gọi từ luồng in thật, để Preview không làm hao hụt số lượng Work Order. ────────────

    [HttpPost("/api/sakura/cartonsn/report-print-result")]
    public async Task<IActionResult> CartonReportPrintResult([FromBody] CartonReportPrintResultRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.CartonNumber))
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            string? palletAttachWarning = await _snLabel.RecordCartonScanAsync(
                req.WorkOrder?.Trim() ?? "",
                req.CartonNumber.Trim(),
                req.Color ?? "",
                req.Condition ?? "",
                req.SerialNumbers ?? new List<string>(),
                req.PalletId);

            PalletBoxesResponse? pallet = null;
            if (!string.IsNullOrWhiteSpace(req.PalletId))
            {
                string palletId = req.PalletId.Trim();
                pallet = ToPalletBoxesResponse(palletId, await _snLabel.GetPalletBoxesAsync(palletId));
            }

            return Ok(new { ok = true, palletAttachWarning, pallet });
        }
        catch (Exception ex)
        {
            return StatusCode(500, BuildError(ex));
        }
    }

    // ── API: Pallet Info Template — preset Inbound Reference/Warehouse Reference/Delivery
    // Address để chọn nhanh ở vùng Print Pallet (PO Number không nằm trong template). ──────────

    [HttpGet("/api/sakura/cartonsn/pallet-template")]
    public async Task<IActionResult> GetPalletInfoTemplates()
    {
        var list = await _snLabel.GetPalletInfoTemplatesAsync();
        return Ok(new { ok = true, data = list });
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/cartonsn/pallet-template")]
    public async Task<IActionResult> CreatePalletInfoTemplate([FromBody] PalletInfoTemplateUpsertRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            var row = await _snLabel.CreatePalletInfoTemplateAsync(req);
            return Ok(new { ok = true, data = row });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPut("/api/sakura/cartonsn/pallet-template/{id:int}")]
    public async Task<IActionResult> UpdatePalletInfoTemplate(int id, [FromBody] PalletInfoTemplateUpsertRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            var row = await _snLabel.UpdatePalletInfoTemplateAsync(id, req);
            return Ok(new { ok = true, data = row });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpDelete("/api/sakura/cartonsn/pallet-template/{id:int}")]
    public async Task<IActionResult> DeletePalletInfoTemplate(int id)
    {
        try
        {
            await _snLabel.DeletePalletInfoTemplateAsync(id);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: Resolve Pallet ID cho 1 Work Order — 1 WO chỉ có ĐÚNG 1 Pallet ID, tự sinh nếu WO
    // hoàn toàn mới, dùng lại nếu WO đã có (xem SakuraService.ResolvePalletIdForWorkOrderAsync).
    // Gọi ngay sau khi Work Order lookup thành công — người vận hành KHÔNG còn tự nhập Pallet ID nữa.

    [HttpGet("/api/sakura/cartonsn/pallet/resolve")]
    public async Task<IActionResult> ResolvePalletId([FromQuery] string workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { ok = false, error = "Thiếu Work Order.", errorCode = "workOrder.missing" });

        string palletId = await _snLabel.ResolvePalletIdForWorkOrderAsync(workOrder.Trim());
        return Ok(new { ok = true, data = new { palletId } });
    }

    // ── API: Print Pallet — gom carton đã in vào 1 Pallet ID, đếm realtime, sinh Pallet Number ─

    private static PalletBoxesResponse ToPalletBoxesResponse(string palletId, (int BoxCount, int UnitCount, List<CartonSnScanLog> Boxes) result) =>
        new PalletBoxesResponse
        {
            PalletId = palletId,
            BoxCount = result.BoxCount,
            UnitCount = result.UnitCount,
            Boxes = result.Boxes.Select(x => new PalletBoxDto
            {
                Id = x.Id,
                CartonNumber = x.CartonNumber,
                Color = x.Color,
                CountSerial = x.CountSerial,
                PalletId = x.PalletId,
                Serial = x.Serial
            }).ToList()
        };

    [HttpGet("/api/sakura/cartonsn/pallet/boxes")]
    public async Task<IActionResult> PalletBoxes([FromQuery] string palletId)
    {
        var result = await _snLabel.GetPalletBoxesAsync(palletId ?? "");
        return Ok(new { ok = true, data = ToPalletBoxesResponse(palletId ?? "", result) });
    }

    [HttpPost("/api/sakura/cartonsn/pallet/scan-box")]
    public async Task<IActionResult> PalletScanBox([FromBody] PalletScanBoxRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            var result = await _snLabel.ScanCartonIntoPalletAsync(req.PalletId?.Trim() ?? "", req.CartonNumber?.Trim() ?? "", req.Color ?? "");
            return Ok(new { ok = true, data = ToPalletBoxesResponse(req.PalletId?.Trim() ?? "", result) });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/cartonsn/pallet/unscan-box")]
    public async Task<IActionResult> PalletUnscanBox([FromBody] PalletUnscanBoxRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            var result = await _snLabel.UnscanCartonFromPalletAsync(req.PalletId?.Trim() ?? "", req.CartonNumber?.Trim() ?? "");
            return Ok(new { ok = true, data = ToPalletBoxesResponse(req.PalletId?.Trim() ?? "", result) });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    [HttpPost("/api/sakura/cartonsn/pallet/print")]
    public async Task<IActionResult> PalletPrint([FromBody] PalletPrintRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        try
        {
            var result = await _snLabel.BuildPalletLabelZplAsync(
                req.PalletId?.Trim() ?? "", req.PoNumber?.Trim() ?? "", req.InboundReference?.Trim() ?? "",
                req.WarehouseReference?.Trim() ?? "", req.DeliveryAddress?.Trim() ?? "");

            // In kèm tem PDF417 (serial của đúng lô vừa in, cùng Pallet Number vừa sinh) mỗi khi
            // tem Pallet được in — xem SakuraService.BuildPdf417LabelZplsAsync. Có thể trả về
            // nhiều tem nếu lô vượt quá 240 serial (4 mã PDF417 x 60 SN/tem).
            var pdf417Zpls = await _snLabel.BuildPdf417LabelZplsAsync(req.PalletId?.Trim() ?? "", result.PalletNumber);

            return Ok(new
            {
                ok = true,
                zpl = result.Zpl,
                palletNumber = result.PalletNumber,
                quantityCartons = result.QuantityCartons,
                quantityUnits = result.QuantityUnits,
                color = result.Color,
                pdf417Zpls
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    // ── API: Carton SN Label — history (1 dòng = 1 carton đã in) ───────────────

    [HttpGet("/api/sakura/cartonsn/history")]
    public async Task<IActionResult> CartonSnHistoryApi(
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? workOrder, [FromQuery] string? cartonNumber,
        [FromQuery] string? serial, [FromQuery] string? color, [FromQuery] string? palletId, [FromQuery] string? palletNumber,
        [FromQuery] bool? isReprint, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _snLabel.GetCartonHistoryAsync(dateFrom, dateTo, workOrder, cartonNumber, serial, color, palletId, palletNumber, page, pageSize, isReprint);
        return Ok(new { ok = true, data = result });
    }

    // ── API: Reprint (trang /sakura/cartonsn/reprint) ───────────────────────────

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpGet("/api/sakura/cartonsn/reprint/pallets")]
    public async Task<IActionResult> CartonSnPalletReprintListApi(
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? workOrder,
        [FromQuery] string? palletId, [FromQuery] string? palletNumber, [FromQuery] bool? isPalletReprint,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _snLabel.GetPalletReprintListAsync(dateFrom, dateTo, workOrder, palletId, palletNumber, page, pageSize, isPalletReprint);
        return Ok(new { ok = true, data = result });
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/cartonsn/reprint/carton")]
    public async Task<IActionResult> ReprintCartonLabel([FromBody] CartonReprintRequest req)
    {
        if (req == null || req.Id <= 0)
            return BadRequest(new { ok = false, error = "Thiếu Id carton cần in lại.", errorCode = "common.missingData" });

        try
        {
            string? overrideSkuPvId = null;
            string? overrideEan = null;

            string? reworkWo = await _snLabel.GetCartonReworkWorkOrderAsync(req.Id);
            if (!string.IsNullOrWhiteSpace(reworkWo))
            {
                // Carton này đã Repack — tra lại ĐÚNG SKU/PVID/EAN hiện tại từ Rework WO đã gắn
                // (không dùng bảng tĩnh theo màu, sẽ in sai) — xem SakuraService.ReprintCartonLabelAsync.
                var wo = await _viidoo.SearchAsync(reworkWo);
                if (wo == null || wo.ProductId is not int productId)
                    return BadRequest(new { ok = false, error = $"Không tìm thấy Rework Work Order '{reworkWo}' trên Odoo để in lại đúng tem carton này.", errorCode = "workOrder.notFoundOdoo", errorParams = new { wo = reworkWo } });

                overrideSkuPvId = wo.ProductCode ?? "";
                overrideEan = await _viidoo.GetProductEanAsync(productId);
            }

            var result = await _snLabel.ReprintCartonLabelAsync(req.Id, overrideSkuPvId, overrideEan);
            return Ok(new { ok = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    [Authorize(Roles = "LineLeader,Admin")]
    [HttpPost("/api/sakura/cartonsn/reprint/pallet")]
    public async Task<IActionResult> ReprintPalletLabel([FromBody] PalletReprintRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.PalletNumber))
            return BadRequest(new { ok = false, error = "Thiếu Pallet Number cần in lại.", errorCode = "common.missingData" });

        try
        {
            var result = await _snLabel.ReprintPalletLabelAsync(req.PalletNumber);
            return Ok(new { ok = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildError(ex));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(BuildError(ex));
        }
    }

    // ── API: ZPL template CRUD (edit template content without touching code) ──

    [HttpGet("/api/sakura/zpl-template/{key}")]
    public async Task<IActionResult> GetZplTemplate(string key)
    {
        string content = await _snLabel.GetZplTemplateAsync(key);
        return Ok(new { ok = true, data = new { templateKey = key, zplContent = content } });
    }

    [HttpPut("/api/sakura/zpl-template/{key}")]
    public async Task<IActionResult> UpdateZplTemplate(string key, [FromBody] SakuraZplTemplateUpdateRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.ZplContent))
            return BadRequest(new { ok = false, error = "Thiếu nội dung ZPL." });

        var row = await _snLabel.UpsertZplTemplateAsync(key, req.ZplContent, req.UpdatedBy);
        return Ok(new { ok = true, data = new { row.TemplateKey, row.UpdatedAt, row.UpdatedBy } });
    }
}
