using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// Controller chung cho các chức năng thuộc dự án Sakura.
// SN Label Print là chức năng đầu tiên — các chức năng Sakura khác sẽ được thêm vào đây.
public class SakuraController : Controller
{
    private readonly SakuraService _snLabel;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ViidooService _viidoo;
    private readonly ProductionResultApiService _productionApi;

    public SakuraController(SakuraService snLabel, IConfiguration config, AppDbContext db, ViidooService viidoo, ProductionResultApiService productionApi)
    {
        _snLabel = snLabel;
        _config = config;
        _db = db;
        _viidoo = viidoo;
        _productionApi = productionApi;
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
    [HttpGet("/sakura")]
    public IActionResult Index()
    {
        // Title/Subtitle o day la fallback tieng Anh hien khi JS chua chay kip;
        // ban dich day du (EN/ZH) nam trong wwwroot/js/sakura-i18n.js, khoa theo Key.
        var tiles = new List<SakuraAppTile>
        {
            new SakuraAppTile
            {
                Key = "snlabelgroup",
                Icon = "🏷️",
                Title = "SN Label",
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
                Title = "Carton SN Label",
                Subtitle = "Carton serial number label printing",
                Enabled = true,
                Items = new List<SakuraAppTile>
                {
                    new SakuraAppTile
                    {
                        Key = "cartonsn",
                        Icon = "🖨️",
                        Title = "Print",
                        Subtitle = "Print carton SN labels",
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
                Title = "FQC",
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
                Key = "comingsoon",
                Icon = "➕",
                Title = "Coming soon",
                Subtitle = "Next Sakura feature",
                Href = null,
                Enabled = false
            }
        };
        return View("~/Views/Sakura/Index.cshtml", tiles);
    }

    [HttpGet("/sakura/snlabel")]
    public IActionResult SnLabelIndex()
    {
        return View("~/Views/Sakura/SnLabel.cshtml");
    }

    [HttpGet("/sakura/snlabel/history")]
    public IActionResult SnLabelHistory()
    {
        return View("~/Views/Sakura/History.cshtml");
    }

    [HttpGet("/sakura/cartonsn")]
    public IActionResult CartonSnIndex()
    {
        return View("~/Views/Sakura/CartonSN.cshtml");
    }

    [HttpGet("/sakura/cartonsn/history")]
    public IActionResult CartonSnHistory()
    {
        return View("~/Views/Sakura/CartonSnHistory.cshtml");
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
                ProductId = result.ProductId
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
            Timeline = SakuraService.VietnamNow()
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
        bool eanOk, colorOk = false, serialLogOk = false;

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

        // Bước 2 (2.1): Check Color — màu embed trong Serial (theo TryResolveColorFromSerial)
        // phải khớp màu của Work Order.
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

        // Bước 3 (2.2): Check đã nhập kết quả sản xuất cho serial này chưa — dùng lại
        // CheckLotSerialFG của trạm Laser nhưng NGƯỢC ý nghĩa: ok=false (đã nhập trước đó)
        // mới là điều SnLabel cần (label chỉ in SAU khi đã nhập KQSX ở trạm khác).
        if (failedStep == null)
        {
            var checkResult = await _productionApi.CheckLotSerialFgAsync(req.ProductId, serial);
            serialLogOk = checkResult.Ok == false;
            if (!serialLogOk)
            {
                failedStep = 3;
                failMessage = "Serial chưa được nhập kết quả sản xuất.";
            }
        }

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
            Timeline = SakuraService.VietnamNow()
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
                serialLogOk,
                logId = logEntry.Id,
                zpl
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
                ProductionDate = SakuraService.VietnamNow().Date,
                RunningNumber = entry.RunningNumber ?? "",
                RunningNumberInt = entry.RunningNumberInt ?? 0,
                PrintedAt = SakuraService.VietnamNow(),
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

    // ── API: unlock Manual print mode with a shared password ────────────────────

    [HttpPost("/api/sakura/snlabel/verify-manual-password")]
    public IActionResult VerifyManualPassword([FromBody] ManualUnlockRequest req)
    {
        string expected = _config["Sakura:SnLabel:ManualModePassword"] ?? "";
        if (req == null || string.IsNullOrEmpty(expected) || req.Password != expected)
            return Unauthorized(new { ok = false, error = "Sai mật khẩu.", errorCode = "password.incorrect" });

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

    // ── API: history ──────────────────────────────────────────────────────────

    [HttpGet("/api/sakura/snlabel/history")]
    public async Task<IActionResult> History([FromQuery] DateTime? date, [FromQuery] string? workOrder, [FromQuery] string? serialNumber, [FromQuery] string? ean, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _snLabel.GetHistoryAsync(date, workOrder, serialNumber, ean, page, pageSize);
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
                ProductId = result.ProductId
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
    public async Task<IActionResult> CartonVerifySerial([FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { ok = false, error = "Thiếu Serial Number.", errorCode = "serial.missing" });

        var (ok, errorCode, message) = await _snLabel.ValidateCartonSerialAsync(serial.Trim());
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
                PalletId = x.PalletId
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

            return Ok(new
            {
                ok = true,
                zpl = result.Zpl,
                palletNumber = result.PalletNumber,
                quantityCartons = result.QuantityCartons,
                quantityUnits = result.QuantityUnits,
                color = result.Color
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
        [FromQuery] string? serial, [FromQuery] string? color, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _snLabel.GetCartonHistoryAsync(dateFrom, dateTo, workOrder, cartonNumber, serial, color, page, pageSize);
        return Ok(new { ok = true, data = result });
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
