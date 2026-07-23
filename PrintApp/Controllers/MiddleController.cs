using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// Trang xem kết quả kiểm tra kích thước magnet (SVN_MiddleDimensionCheckResult),
// dữ liệu được nạp bởi service ngoài magnet-log-collector (Node.js) đọc file
// Excel định kỳ. Ngoài ra còn trạm Nhập kết quả sản xuất (InputResult) — quét Serial
// trực tiếp, có ghi vào SM_MiddleLog (khác các bảng đọc-only còn lại của controller này).
public class MiddleController : Controller
{
    private readonly AppDbContext _db;
    private readonly ViidooService _viidoo;
    private readonly ProductionResultApiService _productionApi;

    public MiddleController(AppDbContext db, ViidooService viidoo, ProductionResultApiService productionApi)
    {
        _db = db;
        _viidoo = viidoo;
        _productionApi = productionApi;
    }

    // Gói 1 exception thành JSON lỗi có errorCode/errorParams (nếu có) để front-end tự
    // dịch theo EN/ZH đang chọn — cùng quy ước với SakuraController/BackPanelController.BuildError.
    private static object BuildError(Exception ex)
    {
        if (ex is ISakuraCodedException coded)
            return new { ok = false, error = ex.Message, errorCode = coded.Code, errorParams = coded.Params };
        return new { ok = false, error = ex.Message, errorCode = "common.unexpectedError", errorParams = new { message = ex.Message } };
    }

    // Cắt bớt message dài (vd exception stack chi tiết) cho vừa cột FailReason NVARCHAR(500).
    private static string? Truncate(string? s, int maxLength) =>
        string.IsNullOrEmpty(s) || s.Length <= maxLength ? s : s.Substring(0, maxLength);

    [HttpGet("/middle/result")]
    public IActionResult Result() => View("~/Views/Middle/Result.cshtml");

    [HttpGet("/middle/summary")]
    public IActionResult Summary() => View("~/Views/Middle/Summary.cshtml");

    [HttpGet("/middle/inputresult")]
    public IActionResult InputResult() => View("~/Views/Middle/InputResult.cshtml");

    [HttpGet("/middle/inputresult/history")]
    public IActionResult InputResultHistory() => View("~/Views/Middle/HistoryInputResult.cshtml");

    // ── API: danh sách kết quả (search UNIT_SN, filter STATUS + khoảng ngày, phân trang) ──
    [HttpGet("/api/middle/result")]
    public async Task<IActionResult> GetResults(
        [FromQuery] string? unitSn,
        [FromQuery] string? status,
        [FromQuery] string? workOrder,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.MiddleDimensionCheckResults.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(unitSn))
        {
            string sn = unitSn.Trim();
            query = query.Where(x => x.UNIT_SN != null && x.UNIT_SN.Contains(sn));
        }

        if (!string.IsNullOrWhiteSpace(workOrder))
        {
            // Work Order không nằm trên chính bảng này — tra qua SVN_ProductionInputLogs
            // (master_wo_code) rồi lọc UNIT_SN theo tập serial_code khớp.
            string wo = workOrder.Trim();
            var matchingSerials = await _db.ProductionInputLogs.AsNoTracking()
                .Where(x => x.SerialCode != null && x.MasterWoCode != null && x.MasterWoCode.Contains(wo))
                .Select(x => x.SerialCode!)
                .Distinct()
                .ToListAsync();
            query = query.Where(x => x.UNIT_SN != null && matchingSerials.Contains(x.UNIT_SN));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            string st = status.Trim();
            query = query.Where(x => x.STATUS != null && x.STATUS.ToUpper() == st.ToUpper());
        }

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(x => x.DATE_TIME >= from);
        }

        if (dateTo.HasValue)
        {
            // dateTo là ngày (không giờ) từ input type=date — lấy hết cả ngày đó.
            var toExclusive = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.DATE_TIME < toExclusive);
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.DATE_TIME)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(MiddleDimensionCheckResultDto.FromEntity).ToList();
        await FillWorkOrdersAsync(dtos);

        return Ok(new
        {
            ok = true,
            data = new MiddleDimensionCheckResultPageDto
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            }
        });
    }

    // Tra cột Work Order hiển thị trên grid: UNIT_SN (= serial_code bên SVN_ProductionInputLogs)
    // -> master_wo_code. Một serial_code có thể có nhiều dòng log (nhiều state), lấy dòng
    // mới nhất (Id lớn nhất).
    private async Task FillWorkOrdersAsync(List<MiddleDimensionCheckResultDto> dtos)
    {
        var unitSns = dtos.Select(d => d.UnitSn).Where(sn => !string.IsNullOrWhiteSpace(sn)).Distinct().ToList();
        var woBySerial = await GetLatestWorkOrderBySerialAsync(unitSns);

        foreach (var dto in dtos)
        {
            if (woBySerial.TryGetValue(dto.UnitSn, out var wo))
                dto.WorkOrder = wo;
        }
    }

    // Dùng chung cho GetResults (fill cột Work Order) và GetSummary (gom nhóm theo WO):
    // SN -> master_wo_code của dòng log mới nhất (Id lớn nhất) bên SVN_ProductionInputLogs.
    // SN không có bản ghi log nào sẽ không xuất hiện trong dictionary trả về.
    private async Task<Dictionary<string, string?>> GetLatestWorkOrderBySerialAsync(List<string> unitSns)
    {
        var woBySerial = new Dictionary<string, string?>();
        if (unitSns.Count == 0) return woBySerial;

        var rows = await _db.ProductionInputLogs.AsNoTracking()
            .Where(x => x.SerialCode != null && unitSns.Contains(x.SerialCode))
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.SerialCode, x.MasterWoCode })
            .ToListAsync();

        foreach (var row in rows)
        {
            if (!woBySerial.ContainsKey(row.SerialCode!))
                woBySerial[row.SerialCode!] = row.MasterWoCode;
        }

        return woBySerial;
    }

    // ── API: Summary — so sánh, theo từng Work Order, số Unit S/N đã test tại Middle với ──
    // số lượng đã nhập kết quả sản xuất (SVN_ProductionInputLogs). SN test lại nhiều lần
    // (nhiều bản ghi trùng UNIT_SN) chỉ tính 1 lần. SN chưa có bản ghi log tương ứng được
    // gom vào nhóm WorkOrder = null ("chưa nhập kết quả sản xuất").
    [HttpGet("/api/middle/summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? workOrder)
    {
        var unitSns = await BuildMiddleUnitSnQuery(dateFrom, dateTo).ToListAsync();
        if (unitSns.Count == 0)
            return Ok(new { ok = true, data = Array.Empty<MiddleWorkOrderSummaryDto>() });

        var woBySerial = await GetLatestWorkOrderBySerialAsync(unitSns);

        // "" = nhóm chưa có Work Order (chưa nhập kết quả sản xuất cho SN đó).
        var snByWo = new Dictionary<string, List<string>>();
        foreach (var sn in unitSns)
        {
            string key = (woBySerial.TryGetValue(sn, out var wo) ? wo : null) ?? "";
            if (!snByWo.TryGetValue(key, out var list))
                snByWo[key] = list = new List<string>();
            list.Add(sn);
        }

        var woList = snByWo.Keys.Where(k => k.Length > 0).ToList();
        if (!string.IsNullOrWhiteSpace(workOrder))
        {
            string wf = workOrder.Trim();
            woList = woList.Where(w => w.Contains(wf, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Entered Qty phải theo cùng khoảng ngày với bộ lọc (date_finished), không phải luôn
        // luôn tính trên toàn bộ lịch sử WO — nếu không Entered Qty sẽ không đổi theo filter.
        var logQuery = _db.ProductionInputLogs.AsNoTracking()
            .Where(x => x.MasterWoCode != null && woList.Contains(x.MasterWoCode));

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            logQuery = logQuery.Where(x => x.DateFinished != null && x.DateFinished >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.Date.AddDays(1);
            logQuery = logQuery.Where(x => x.DateFinished != null && x.DateFinished < toExclusive);
        }

        var logRows = await logQuery
            .Select(x => new { x.MasterWoCode, x.WoCode, x.SerialCode })
            .ToListAsync();

        // Có lọc ngày: đếm số SERIAL RIÊNG BIỆT đã nhập trong khoảng ngày đó (không lấy MAX
        // suffix vì MAX chỉ đúng khi tính trên toàn bộ lịch sử WO). Phải Distinct theo
        // SerialCode — không đếm thẳng số dòng bản ghi — vì 1 serial có thể có nhiều dòng log
        // (nhập lại/sửa) trong cùng khoảng ngày; nếu không sẽ đếm trùng và Entered Qty (từ đó
        // ra Gap) không còn khớp với danh sách SN Distinct ở GetSummaryDetail bên dưới.
        bool hasDateFilter = dateFrom.HasValue || dateTo.HasValue;
        var enteredQtyByWo = logRows
            .GroupBy(x => x.MasterWoCode!)
            .ToDictionary(
                g => g.Key,
                g => hasDateFilter
                    ? g.Select(x => x.SerialCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    : g.Select(x => ParseWoSuffix(x.WoCode)).DefaultIfEmpty(0).Max());

        var results = new List<MiddleWorkOrderSummaryDto>();

        if (string.IsNullOrWhiteSpace(workOrder) && snByWo.TryGetValue("", out var noWoSns))
        {
            results.Add(new MiddleWorkOrderSummaryDto
            {
                WorkOrder = null,
                MiddleTestedCount = noWoSns.Count,
                EnteredQty = 0
            });
        }

        foreach (var wo in woList)
        {
            enteredQtyByWo.TryGetValue(wo, out var enteredQty);
            results.Add(new MiddleWorkOrderSummaryDto
            {
                WorkOrder = wo,
                MiddleTestedCount = snByWo[wo].Count,
                EnteredQty = enteredQty
            });
        }

        // Nhóm "chưa có WO" luôn cần chú ý nên lên đầu; còn lại xếp theo độ lệch (|Diff|)
        // giảm dần — WO lệch nhiều nhất (thiếu/vượt nhiều nhất) lên trên, WO khớp đủ dạt xuống cuối.
        var sorted = results
            .OrderBy(r => r.WorkOrder == null ? 0 : 1)
            .ThenByDescending(r => Math.Abs(r.Diff))
            .ThenByDescending(r => r.WorkOrder, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new { ok = true, data = sorted });
    }

    // "NM/MO/04025-140" -> 140 (số lượng đã nhập kết quả sản xuất cho WO đó).
    // Cùng logic với BackPanelController.BuildNextSubNameAsync, chỉ khác là chạy ở C# vì
    // ở đây cần đọc wo_code hàng loạt (theo danh sách WO) thay vì tính max cho 1 WO.
    private static int ParseWoSuffix(string? woCode)
    {
        if (string.IsNullOrEmpty(woCode)) return 0;
        int dashIdx = woCode.LastIndexOf('-');
        if (dashIdx < 0 || dashIdx == woCode.Length - 1) return 0;
        return int.TryParse(woCode[(dashIdx + 1)..], out var n) ? n : 0;
    }

    // Danh sách UNIT_SN duy nhất test tại Middle trong khoảng ngày lọc — dùng chung cho
    // GetSummary và GetSummaryDetail.
    private IQueryable<string> BuildMiddleUnitSnQuery(DateTime? dateFrom, DateTime? dateTo)
    {
        var query = _db.MiddleDimensionCheckResults.AsNoTracking()
            .Where(x => x.UNIT_SN != null && x.UNIT_SN != "");

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(x => x.DATE_TIME >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.DATE_TIME < toExclusive);
        }

        return query.Select(x => x.UNIT_SN!).Distinct();
    }

    // ── API: chi tiết SN gây lệch cho 1 dòng Summary — bấm mở rộng 1 WO (hoặc nhóm "chưa
    // có WO" nếu workOrder rỗng) để xem đúng SN nào đang thiếu/dư, thay vì chỉ nhìn con số. ──
    [HttpGet("/api/middle/summary/detail")]
    public async Task<IActionResult> GetSummaryDetail(
        [FromQuery] string? workOrder,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        var unitSns = await BuildMiddleUnitSnQuery(dateFrom, dateTo).ToListAsync();
        var woBySerial = await GetLatestWorkOrderBySerialAsync(unitSns);

        // Nhóm "chưa có WO": liệt kê SN test tại Middle nhưng chưa có bản ghi log nào cả
        // (chưa nhập kết quả sản xuất) — không có gì để so "thiếu" nên missingAtMiddle rỗng.
        if (string.IsNullOrWhiteSpace(workOrder))
        {
            var noWoSerials = unitSns.Where(sn => !woBySerial.ContainsKey(sn))
                .OrderBy(sn => sn, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(new
            {
                ok = true,
                data = new { workOrder = (string?)null, missingAtMiddle = Array.Empty<string>(), extraAtMiddle = noWoSerials }
            });
        }

        string wo = workOrder.Trim();
        var testedSerials = unitSns.Where(sn => woBySerial.TryGetValue(sn, out var w) && w == wo).ToList();

        var logQuery = _db.ProductionInputLogs.AsNoTracking()
            .Where(x => x.MasterWoCode == wo && x.SerialCode != null);

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            logQuery = logQuery.Where(x => x.DateFinished != null && x.DateFinished >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.Date.AddDays(1);
            logQuery = logQuery.Where(x => x.DateFinished != null && x.DateFinished < toExclusive);
        }

        var producedSerials = await logQuery.Select(x => x.SerialCode!).Distinct().ToListAsync();

        // missingAtMiddle: đã nhập kết quả sản xuất nhưng CHƯA test tại Middle (ứng với "Thiếu").
        // extraAtMiddle: test tại Middle nhưng KHÔNG thấy trong sản xuất đã nhập (ứng với "Vượt").
        var missingAtMiddle = producedSerials.Except(testedSerials, StringComparer.OrdinalIgnoreCase)
            .OrderBy(sn => sn, StringComparer.OrdinalIgnoreCase).ToList();
        var extraAtMiddle = testedSerials.Except(producedSerials, StringComparer.OrdinalIgnoreCase)
            .OrderBy(sn => sn, StringComparer.OrdinalIgnoreCase).ToList();

        return Ok(new
        {
            ok = true,
            data = new { workOrder = wo, missingAtMiddle, extraAtMiddle }
        });
    }

    // ── Trạm Nhập kết quả sản xuất (InputResult) — quét Work Order lấy màu từ Odoo, quét ──
    // Serial Number để đối chiếu màu rồi nhập kết quả sản xuất. Cùng logic với trạm Laser
    // (Back Panel) nhưng KHÔNG có bước in vật lý (Print Laser) — verify-serial chạy xong
    // là chốt PASS/FAIL ngay trong 1 request, không cần bước report-print-result riêng.

    [HttpGet("/api/middle/inputresult/workorder-lookup")]
    public async Task<IActionResult> InputResultWorkOrderLookup([FromQuery] string workOrder)
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

    [HttpPost("/api/middle/inputresult/verify-serial")]
    public async Task<IActionResult> InputResultVerifySerial([FromBody] MiddleInputVerifySerialRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.SerialNumber))
            return BadRequest(new { ok = false, error = "Thiếu Serial Number.", errorCode = "serial.missing" });

        if (string.IsNullOrWhiteSpace(req.ExpectedColor))
            return BadRequest(new { ok = false, error = "Thiếu màu Work Order để đối chiếu — vui lòng quét Work Order trước.", errorCode = "serial.missingExpectedColor" });

        string serial = req.SerialNumber.Trim();
        string workOrder = req.WorkOrder?.Trim() ?? "";
        string? serialColor = SakuraService.TryResolveColorFromMiddleSerial(serial);
        if (serialColor == null)
            return BadRequest(new { ok = false, error = $"Serial '{serial}' không đúng định dạng.", errorCode = "serial.invalidFormat", errorParams = new { serial } });

        bool match = string.Equals(serialColor, req.ExpectedColor.Trim(), StringComparison.OrdinalIgnoreCase);

        // Thứ tự bước: 1) Scan Color Check  2) Check Serial Already Entered  3) Enter
        // Production Result. Không có bước in vật lý nên không cần logic "priorSubName" của
        // Laser (dùng để tránh Check báo trùng khi retry sau khi bước in fail) — ở đây mỗi
        // lần gọi verify-serial luôn chạy trọn vẹn cả 3 bước trong 1 request.
        bool? checkResultOk = null;
        string? checkResultMessage = null;
        bool? inputResultOk = null;
        string? inputResultMessage = null;
        string? subName = null;
        string? failReason = null;

        if (match)
        {
            if (req.ProductId is int productId)
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
                        Console.WriteLine($"[Middle] Loi query SVN_ProductionInputLogs de tinh subName (sau {maxAttempts} lan thu): {lastEx.Message}");
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
        string status = failedStep != null ? "FAIL" : "PASS";

        try
        {
            var logEntry = new MiddleLog
            {
                WorkOrder = workOrder,
                SerialNumber = serial,
                Status = status,
                FailedStep = failedStep,
                ProductionResultSubName = subName,
                FailReason = failedStep != null ? Truncate(failReason, 500) : null,
                Timeline = SakuraService.VietnamNow()
            };
            _db.MiddleLogs.Add(logEntry);
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
                color = serialColor,
                expectedColor = req.ExpectedColor,
                match,
                checkResultOk,
                checkResultMessage,
                inputResultOk,
                inputResultMessage,
                subName
            }
        });
    }

    // Lấy số thứ tự WO con lớn nhất HIỆN CÓ (chưa +1) của master_wo_code này từ
    // SVN_ProductionInputLogs — dùng để hiển thị tiến độ (subName hiện tại / tổng số lượng)
    // trên giao diện. Cùng logic với BackPanelController.GetCurrentMaxSubNameSuffixAsync.
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

    // Lấy số thứ tự WO con lớn nhất hiện có của master_wo_code này từ SVN_ProductionInputLogs
    // (nguồn sự thật thực tế), +1, để tính subName tiếp theo ("{masterWoCode}-{max+1:000}").
    // Cùng logic với BackPanelController.BuildNextSubNameAsync — luôn query DB tại đúng thời
    // điểm gọi (không cache), ném exception nếu query fail (caller phải dừng, không tự đoán).
    private async Task<string> BuildNextSubNameAsync(string masterWoCode)
    {
        int maxSuffix = await GetCurrentMaxSubNameSuffixAsync(masterWoCode);
        return $"{masterWoCode}-{(maxSuffix + 1):D3}";
    }

    // ── API: lịch sử quét ở trạm InputResult (filter theo Work Order / Serial Number, phân trang) ──
    [HttpGet("/api/middle/inputresult/history")]
    public async Task<IActionResult> GetInputResultHistory([FromQuery] string? workOrder, [FromQuery] string? serialNumber, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.MiddleLogs.AsNoTracking().AsQueryable();

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
            .Select(x => new MiddleLogItemDto
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
            data = new MiddleLogPageDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            }
        });
    }
}
