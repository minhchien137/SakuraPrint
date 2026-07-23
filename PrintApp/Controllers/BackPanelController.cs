using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// Controller chung cho các chức năng thuộc trạm Back Panel (bán thành phẩm Sakura).
// Laser là chức năng đầu tiên: quét Work Order lấy màu từ Odoo, quét Serial Number
// (đã laser) để đối chiếu màu embed trong serial (theo quy tắc của SakuraService) với
// màu của Work Order. Mỗi lần quét Serial được log lại vào SM_BackPanelLaserLog.
public class BackPanelController : Controller
{
    private readonly ViidooService _viidoo;
    private readonly AppDbContext _db;
    private readonly ProductionResultApiService _productionApi;

    public BackPanelController(ViidooService viidoo, AppDbContext db, ProductionResultApiService productionApi)
    {
        _viidoo = viidoo;
        _db = db;
        _productionApi = productionApi;
    }

    // Cắt bớt message dài (vd exception stack chi tiết) cho vừa cột FailReason NVARCHAR(500).
    private static string? Truncate(string? s, int maxLength) =>
        string.IsNullOrEmpty(s) || s.Length <= maxLength ? s : s.Substring(0, maxLength);

    // Gói 1 exception thành JSON lỗi có errorCode/errorParams (nếu có) để front-end tự
    // dịch theo EN/ZH đang chọn — cùng quy ước với SakuraController.BuildError.
    private static object BuildError(Exception ex)
    {
        if (ex is ISakuraCodedException coded)
            return new { ok = false, error = ex.Message, errorCode = coded.Code, errorParams = coded.Params };
        return new { ok = false, error = ex.Message, errorCode = "common.unexpectedError", errorParams = new { message = ex.Message } };
    }

    // ── View ──────────────────────────────────────────────────────────────────

    [HttpGet("/backpanel/laser")]
    public IActionResult Laser() => View("~/Views/BackPanel/Laser.cshtml");

    [HttpGet("/backpanel/laser/history")]
    public IActionResult History() => View("~/Views/BackPanel/HistoryLaser.cshtml");

    // ── API: Work Order lookup (chỉ cần màu, không cần số lượng) ────────────────

    [HttpGet("/api/backpanel/laser/workorder-lookup")]
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

            int currentSubNameSuffix = await GetCurrentMaxSubNameSuffixAsync(wo);

            return Ok(new { ok = true, data = new { workOrder = wo, color = result.Color, productId = result.ProductId, quantity = result.Quantity, currentSubNameSuffix } });
        }
        catch (Exception ex)
        {
            return BadRequest(BuildError(ex));
        }
    }

    // ── API: đối chiếu màu embed trong Serial Number với màu Work Order ─────────

    [HttpPost("/api/backpanel/laser/verify-serial")]
    public async Task<IActionResult> VerifySerial([FromBody] LaserVerifySerialRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.SerialNumber))
            return BadRequest(new { ok = false, error = "Thiếu Serial Number.", errorCode = "serial.missing" });

        if (string.IsNullOrWhiteSpace(req.ExpectedColor))
            return BadRequest(new { ok = false, error = "Thiếu màu Work Order để đối chiếu — vui lòng quét Work Order trước.", errorCode = "serial.missingExpectedColor" });

        string serial = req.SerialNumber.Trim();
        string workOrder = req.WorkOrder?.Trim() ?? "";
        string? serialColor = SakuraService.TryResolveColorFromSerial(serial);
        if (serialColor == null)
            return BadRequest(new { ok = false, error = $"Serial '{serial}' không đúng định dạng.", errorCode = "serial.invalidFormat", errorParams = new { serial } });

        bool match = string.Equals(serialColor, req.ExpectedColor.Trim(), StringComparison.OrdinalIgnoreCase);

        // Thứ tự bước: 1) Scan Color Check  2) Check Serial Already Entered  3) Enter
        // Production Result  4) Print Laser. Server (IIS trung tâm) không với tới ổ đĩa của
        // máy dưới xưởng nên KHÔNG tự ghi SN.txt nữa — bước 4 do trình duyệt (chạy trên máy
        // xưởng) tự gọi bridge cục bộ (localhost) để ghi, rồi báo kết quả qua ReportPrintResult.
        bool? checkResultOk = null;
        string? checkResultMessage = null;
        bool? inputResultOk = null;
        string? inputResultMessage = null;
        string? subName = null;
        string? failReason = null;

        if (match)
        {
            // Nếu serial này đã từng Nhập KQSX THÀNH CÔNG ở 1 lần thử trước đó (nhưng In Laser
            // lúc đó lại fail/chưa báo kết quả) — dùng lại subName cũ, KHÔNG gọi lại Check/Nhập
            // KQSX. Nếu không có bước chặn này, Try Again sau khi In Laser fail sẽ luôn bị Check
            // báo "đã nhập" (vì đúng là đã nhập thật), kẹt vĩnh viễn không bao giờ in được.
            string? priorSubName = await _db.BackPanelLaserLogs
                .Where(x => x.WorkOrder == workOrder && x.SerialNumber == serial
                    && (x.Status == "FAIL" || x.Status == "PENDING") && x.ProductionResultSubName != null)
                .OrderByDescending(x => x.Id)
                .Select(x => x.ProductionResultSubName)
                .FirstOrDefaultAsync();

            if (priorSubName != null)
            {
                checkResultOk = true;
                inputResultOk = true;
                subName = priorSubName;
            }
            else if (req.ProductId is int productId)
            {
                var checkResult = await _productionApi.CheckLotSerialFgAsync(productId, serial);
                checkResultOk = checkResult.Ok;
                checkResultMessage = checkResult.Message;
                if (!checkResult.Ok) failReason = checkResult.Message;

                if (checkResult.Ok)
                {
                    // Query tính subName đôi khi gặp lỗi ngắt quãng (mất kết nối/lock tạm thời
                    // tới SQL Server) — thử lại vài lần trước khi bỏ cuộc, thay vì báo FAIL ngay
                    // từ lần lỗi đầu tiên. KHÔNG được tự đoán subName khi hết lượt thử vẫn lỗi.
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

                    if (lastEx != null)
                    {
                        Console.WriteLine($"[BackPanel] Loi query SVN_ProductionInputLogs de tinh subName (sau {maxAttempts} lan thu): {lastEx.Message}");
                        inputResultOk = false;
                        inputResultMessage = "Không lấy được số thứ tự WO từ database.";
                        failReason = $"BuildNextSubNameAsync: {lastEx.Message}";
                    }

                    if (subName != null)
                    {
                        var inputResult = await _productionApi.InputProductionResultLogAsync(
                            workOrder, subName, serial, productId, req.TotalQuantity ?? 0);
                        inputResultOk = inputResult.Ok;
                        inputResultMessage = inputResult.Message;
                        if (!inputResult.Ok)
                        {
                            subName = null; // API 2 fail -> không tính là đã dùng subName này.
                            failReason = inputResult.Message;
                        }
                    }
                }
            }
            else
            {
                checkResultOk = false;
                checkResultMessage = "Thiếu Product ID của Work Order — vui lòng quét lại Work Order.";
                failReason = checkResultMessage;
            }
        }

        int? failedStep = !match ? 1
            : (checkResultOk == false ? 2
            : (inputResultOk == false ? 3 : (int?)null));
        // "PENDING" = check + nhập KQSX đã pass, đang chờ trình duyệt in laser + báo kết quả
        // qua ReportPrintResult. Chỉ "FAIL" khi fail hẳn ở bước 1-3 (bước 4 chưa biết kết quả).
        string status = failedStep != null ? "FAIL" : "PENDING";

        int logId;
        try
        {
            var logEntry = new BackPanelLaserLog
            {
                WorkOrder = workOrder,
                SerialNumber = serial,
                Status = status,
                FailedStep = failedStep,
                ProductionResultSubName = subName,
                FailReason = failedStep != null ? Truncate(failReason, 500) : null,
                Timeline = SakuraService.VietnamNow()
            };
            _db.BackPanelLaserLogs.Add(logEntry);
            await _db.SaveChangesAsync();
            logId = logEntry.Id;
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
                color = serialColor,
                expectedColor = req.ExpectedColor,
                match,
                checkResultOk,
                checkResultMessage,
                inputResultOk,
                inputResultMessage,
                subName,
                logId
            }
        });
    }

    // ── API: trình duyệt (chạy trên máy xưởng) báo kết quả in laser cục bộ qua bridge ──
    // Chỉ gọi sau khi verify-serial trả checkResultOk=true && inputResultOk=true (logId của
    // dòng log "PENDING" tương ứng).
    [HttpPost("/api/backpanel/laser/report-print-result")]
    public async Task<IActionResult> ReportPrintResult([FromBody] LaserReportPrintResultRequest req)
    {
        if (req == null)
            return BadRequest(new { ok = false, error = "Thiếu dữ liệu.", errorCode = "common.missingData" });

        var entry = await _db.BackPanelLaserLogs.FindAsync(req.LogId);
        if (entry == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy log Id={req.LogId}.", errorCode = "common.missingData" });

        entry.Status = req.Success ? "PASS" : "FAIL";
        entry.FailedStep = req.Success ? null : 4;
        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    // Lấy số thứ tự WO con lớn nhất HIỆN CÓ (chưa +1) của master_wo_code này từ
    // SVN_ProductionInputLogs — dùng để hiển thị tiến độ (subName hiện tại / tổng số lượng)
    // trên giao diện. Xem BuildNextSubNameAsync bên dưới cho phần tính subName tiếp theo.
    private async Task<int> GetCurrentMaxSubNameSuffixAsync(string masterWoCode)
    {
        // CASE WHEN chặn trước trường hợp wo_code không có dấu '-' (RIGHT sẽ lỗi length âm
        // nếu thiếu bọc này — TRY_CAST một mình không chặn được lỗi đó).
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

    // Lấy số thứ tự WO con lớn nhất hiện có của master_wo_code này, +1, để tính subName
    // tiếp theo ("{masterWoCode}-{max+1:000}"). Luôn query DB tại đúng thời điểm gọi (không
    // cache theo phiên) để lấy số mới nhất. Ném exception nếu query fail — caller phải dừng
    // lại, không được tự đoán subName khi mất kết nối DB.
    private async Task<string> BuildNextSubNameAsync(string masterWoCode)
    {
        int maxSuffix = await GetCurrentMaxSubNameSuffixAsync(masterWoCode);
        return $"{masterWoCode}-{(maxSuffix + 1):D3}";
    }

    // ── API: lịch sử quét (filter theo Work Order / Serial Number, phân trang) ──

    [HttpGet("/api/backpanel/laser/history")]
    public async Task<IActionResult> GetHistory([FromQuery] string? workOrder, [FromQuery] string? serialNumber, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.BackPanelLaserLogs.AsNoTracking().AsQueryable();

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

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.Timeline)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BackPanelLaserLogItemDto
            {
                Id = x.Id,
                WorkOrder = x.WorkOrder,
                SerialNumber = x.SerialNumber,
                Status = x.Status,
                FailedStep = x.FailedStep,
                ProductionResultSubName = x.ProductionResultSubName,
                FailReason = x.FailReason,
                Timeline = x.Timeline
            })
            .ToListAsync();

        return Ok(new
        {
            ok = true,
            data = new BackPanelLaserLogPageDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            }
        });
    }
}
