using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// API riêng cho HỆ THỐNG NGOÀI gọi vào để in tem Serial Number — tách biệt hoàn toàn
// với luồng SnLabel/Sakura hiện tại (Work Order, verify EAN/Color/Serial, SM_SNLabelPrint,
// SM_SNLabelScanLog...). Không ghi log/lịch sử gì cả, chỉ build ZPL + gửi thẳng tới máy in.
// Dùng chung template ZPL "SnLabel" (SakuraService.GetZplTemplateAsync) và hàm gửi TCP đã
// có sẵn (SakuraService.SendZplAsync) để tránh viết trùng logic build/gửi ZPL.
public class ExternalPrintController : Controller
{
    private readonly AppDbContext _db;
    private readonly SakuraService _sakura;

    public ExternalPrintController(AppDbContext db, SakuraService sakura)
    {
        _db = db;
        _sakura = sakura;
    }

    // POST /api/external/print/serial
    // Body: { "serial": "...", "printerId": "SAKURA_01" (mặc định nếu bỏ trống), "copies": 1 (mặc định) }
    // Server tra IP/Port máy in theo printerId trong bảng SM_Printer_Info rồi gửi ZPL
    // thẳng tới máy in qua TCP — hệ thống ngoài không cần biết ZPL hay bridge cục bộ.
    [HttpPost("/api/external/print/serial")]
    public async Task<IActionResult> PrintSerial([FromBody] ExternalPrintRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Serial))
            return BadRequest(new { ok = false, error = "Thiếu Serial." });

        string serial = req.Serial.Trim();
        string printerId = string.IsNullOrWhiteSpace(req.PrinterId) ? "SAKURA_01" : req.PrinterId.Trim();
        int copies = req.Copies > 0 ? req.Copies : 1;

        var printer = await _db.SmPrinterInfos.AsNoTracking().FirstOrDefaultAsync(p => p.ID_Printer == printerId);
        if (printer == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy máy in '{printerId}' trong SM_Printer_Info." });

        if (!int.TryParse(printer.Port_Printer, out int port))
            return BadRequest(new { ok = false, error = $"Port máy in '{printerId}' không hợp lệ: '{printer.Port_Printer}'." });

        string template = await _sakura.GetZplTemplateAsync("SnLabel");
        string zpl = SakuraService.BuildConcatenatedZpl(template, new[] { serial });

        try
        {
            for (int i = 0; i < copies; i++)
                await _sakura.SendZplAsync(printer.IP_Printer, port, zpl);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, error = $"Gửi lệnh in thất bại: {ex.Message}" });
        }

        return Ok(new { ok = true, serial, printerId, ip = printer.IP_Printer, port, copies });
    }
}
