using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

public class ToastController : Controller
{
    private readonly ToastService _toast;
    private readonly AppDbContext _db;

    public ToastController(ToastService toast, AppDbContext db)
    {
        _toast = toast;
        _db = db;
    }

    // ── View ───────────────────────────────────────────────────────────────────

    [HttpGet("/toast")]
    public async Task<IActionResult> Index()
    {
        // Lấy danh sách printer có target = "Toast" để bind vào select
        var printers = await _db.PrinterInfos
            .Where(p => p.target == "Toast")
            .ToListAsync();
        return View(printers);
    }

    // ── API: check serial ──────────────────────────────────────────────────────

    [HttpGet("/api/toast/checkserial/{prefix}/{serial}")]
    public async Task<IActionResult> CheckSerial(string prefix, string serial)
    {
        bool exists = await _toast.CheckSerialExistAsync(prefix, serial);
        return Ok(exists);
    }

    // ── API: save label data ───────────────────────────────────────────────────

    [HttpPost("/api/toast/label")]
    public async Task<IActionResult> CreateLabel([FromBody] AstroLabelDataDto dto)
    {
        var created = await _toast.CreateLabelDataAsync(dto);
        return Ok(created);
    }

    // ── API: print box label ───────────────────────────────────────────────────

    [HttpPost("/api/toast/print")]
    public async Task<IActionResult> PrintLabel([FromBody] ToastLabelRequest req)
    {
        var printer = await _toast.GetPrinterAsync(req.PrinterId ?? "");
        if (printer == null)
            return BadRequest(new { ok = false, error = "Không tìm thấy máy in." });

        try
        {
            string zpl = _toast.BuildToastZpl(printer, req);
            return Ok(new { ok = true, zpl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, error = ex.Message });
        }
    }

    // ── API: count pallet ──────────────────────────────────────────────────────

    [HttpGet("/api/toast/count/{palletId}")]
    public async Task<IActionResult> CountPallet(string palletId)
    {
        int count = await _toast.CountPalletAsync(palletId);
        return Ok(count);
    }

    // ── API: get pallet rows ───────────────────────────────────────────────────

    [HttpGet("/api/toast/pallet/{palletId}")]
    public async Task<IActionResult> GetPallet(string palletId)
    {
        var list = await _toast.GetPalletAsync(palletId);
        return Ok(list);
    }

    // ── API: delete box from pallet ────────────────────────────────────────────

    [HttpGet("/api/toast/delete/{serial}")]
    public async Task<IActionResult> DeleteBox(string serial)
    {
        bool ok = await _toast.DeleteBoxAsync(serial);
        return Ok(ok);
    }

    // ── API: print pallet label ────────────────────────────────────────────────

    [HttpPost("/api/toast/printpallet")]
    public async Task<IActionResult> PrintPallet([FromBody] ToastPalletRequest req)
    {
        var (success, error, zpl) = await _toast.BuildPalletZplAsync(req);
        if (!success) return BadRequest(new { ok = false, error });
        return Ok(new { ok = true, zpl });
    }

    // ── API: list printers ─────────────────────────────────────────────────────

    [HttpGet("/api/toast/printers")]
    public async Task<IActionResult> GetPrinters()
    {
        var list = await _db.PrinterInfos
            .Where(p => p.target == "Toast")
            .Select(p => new { p.ID_Printer, p.Name_Printer, p.IP_Printer, p.Port_Printer, p.ZPL_Temp })
            .ToListAsync();
        return Ok(list);
    }
}