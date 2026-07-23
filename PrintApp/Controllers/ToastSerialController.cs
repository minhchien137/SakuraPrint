using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

[IgnoreAntiforgeryToken]
public class ToastSerialController : Controller
{
    private readonly ToastSerialService _serialSvc;
    private readonly AppDbContext _db;

    public ToastSerialController(ToastSerialService serialSvc, AppDbContext db)
    {
        _serialSvc = serialSvc;
        _db = db;
    }

    // ── Views ──────────────────────────────────────────────────────────────────

    [HttpGet("/toast/fct")]
    public IActionResult Fct() => View("~/Views/Toast/FctScan.cshtml");

    [HttpGet("/toast/fqc")]
    public async Task<IActionResult> Fqc()
    {
        var printer = await _db.PrinterInfos
            .FirstOrDefaultAsync(p => p.Name_Printer == "TEST_TOAST_1SERIAL");
        if (printer == null)
            return NotFound("Không tìm thấy máy in FQC_Toast trong DB.");
        return View("~/Views/Toast/FqcScan.cshtml", printer);
    }

    // ── FCT API ────────────────────────────────────────────────────────────────

    [HttpPost("/FctScanToast/Submit")]
    public async Task<IActionResult> FctSubmit([FromBody] FctSubmitReq req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Serial))
            return BadRequest(new { ok = false, message = "Serial rỗng." });

        var (ok, message, serial, status) = await _serialSvc.SubmitFctAsync(req.Serial, req.Status);

        if (!ok)
            return StatusCode(req.Serial.Trim().Length == 13 ? 409 : 400,
                new { ok = false, message });

        return Ok(new { ok = true, serial, status, message });
    }

    // ── FQC API ────────────────────────────────────────────────────────────────

    [HttpPost("/UpdateFqcStatus")]
    public async Task<IActionResult> UpdateFqcStatus([FromBody] FqcUpdateRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.serialNumber))
            return BadRequest(new { ok = false, message = "Thiếu dữ liệu." });

        var (ok, message) = await _serialSvc.UpdateFqcAsync(req.serialNumber, req.status);

        if (!ok) return BadRequest(new { ok = false, message });

        return Ok(new { ok = true, message });
    }

    // ── Serial info lookup ─────────────────────────────────────────────────────

    [HttpGet("/api/toastserial/{serial}")]
    public async Task<IActionResult> GetSerial(string serial)
    {
        var rec = await _serialSvc.GetBySerialAsync(serial);
        if (rec == null) return NotFound(new { ok = false });
        return Ok(new
        {
            ok = true,
            data = new
            {
                rec.SerialNumber,
                rec.WorkOrder,
                rec.FCTStatus,
                rec.FCTStatusDatetime,
                rec.FQCStatus,
                rec.FQCStatusDatetime
            }
        });
    }
}