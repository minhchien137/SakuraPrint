using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// API riêng cho HỆ THỐNG NGOÀI gọi vào để in tem LotWip — tách biệt hoàn toàn với luồng
// SnLabel/Sakura hiện tại. Input vẫn chỉ là 1 chuỗi text (Serial trong body = Lot Number),
// server tự tra cứu các giá trị còn lại (Product WO, Product, Lot Qty) rồi build ZPL theo
// template "LotWip" (SM_Sakura_ZplTemplate, xem SM_Sakura_ZplTemplate_Seed_LotWip.sql).
//
// 3 CÁCH GỬI ZPL TỚI MÁY IN:
//   - PrintSerial (viaBridge=false mặc định): server tự mở TCP thẳng tới máy in. Server chạy
//     PrintApp (ds.sigmaworldwide.io) đã xác nhận KHÔNG có route mạng thật tới LAN nhà máy
//     (debugLocalRouteIp trả về IP public) nên cách này KHÔNG hoạt động trong triển khai hiện
//     tại — giữ lại chỉ để debug/tương lai nếu đổi hạ tầng.
//   - PrintSerial (viaBridge=true): gọi qua PrintService cục bộ (localhost:8021/print). Chỉ
//     hoạt động nếu PrintApp và PrintService chạy chung 1 máy — không phải trường hợp hiện tại.
//   - QueuePrintSerial (ĐANG DÙNG THẬT): chỉ lưu lệnh in vào SM_ExternalPrint_Queue; PrintService
//     chạy trên máy trong xưởng (cùng LAN với máy in) tự poll GetPendingQueue rồi tự in, báo kết
//     quả qua CompleteQueueItem — xem SM_ExternalPrint_Queue_CreateTable.sql.
public class ExternalPrintController : Controller
{
    private const string BridgeBaseUrl = "http://localhost:8021";

    private readonly AppDbContext _db;
    private readonly SakuraService _sakura;
    private readonly ViidooService _viidoo;
    private readonly IHttpClientFactory _httpClientFactory;

    public ExternalPrintController(AppDbContext db, SakuraService sakura, ViidooService viidoo, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _sakura = sakura;
        _viidoo = viidoo;
        _httpClientFactory = httpClientFactory;
    }

    private record LotWipData(string Zpl, string ProductWO, string Product, decimal LotQty);

    // Tra cứu 3 giá trị còn thiếu cho tem LotWip từ 1 Lot Number (= serial_code):
    //   1. SVN_ProductionInputLogs — match CHÍNH XÁC theo serial_code = lotNumber (serial_code
    //      dạng "LOT-..." là duy nhất trong bảng, xác nhận qua query trực tiếp DB) -> lấy
    //      master_wo_code (Product WO) + product_qty (Lot Qty) của đúng dòng đó.
    //   2. Viidoo/Odoo (product_id[1] thô, vd "[WSS03-00307] Back Panel- ... - Sakura Green")
    //      tra theo Product WO vừa tìm được -> Product.
    // Ném Exception với message rõ ràng nếu không tìm thấy/không hợp lệ ở bước nào.
    private async Task<LotWipData> BuildLotWipDataAsync(string lotNumber)
    {
        var row = await _db.ProductionInputLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SerialCode == lotNumber);

        if (row == null || string.IsNullOrWhiteSpace(row.MasterWoCode))
            throw new Exception($"Không tìm thấy Lot Number '{lotNumber}' (serial_code) trong SVN_ProductionInputLogs.");

        if (row.ProductQty is not decimal lotQty)
            throw new Exception($"Lot Number '{lotNumber}' không có product_qty trong SVN_ProductionInputLogs.");

        string productWO = row.MasterWoCode;

        string? product = await _viidoo.GetProductNameByWorkOrderAsync(productWO);
        if (string.IsNullOrWhiteSpace(product))
            throw new Exception($"Không tìm thấy sản phẩm trên Odoo cho Work Order '{productWO}'.");

        string template = await _sakura.GetZplTemplateAsync("LotWip");
        string zpl = template
            .Replace("{lotNumber}", lotNumber)
            .Replace("{productWO}", productWO)
            .Replace("{product}", product)
            .Replace("{lotQty}", lotQty.ToString("0.######"));

        return new LotWipData(zpl, productWO, product, lotQty);
    }

    // POST /api/external/print/serial
    // Body: { "serial": "..." (Lot Number), "printerId": "SAKURA_01" (mặc định),
    //         "copies": 1 (mặc định), "viaBridge": false (mặc định) }
    [HttpPost("/api/external/print/serial")]
    public async Task<IActionResult> PrintSerial([FromBody] ExternalPrintRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Serial))
            return BadRequest(new { ok = false, error = "Thiếu Serial." });

        string lotNumber = req.Serial.Trim();
        string printerId = string.IsNullOrWhiteSpace(req.PrinterId) ? "SAKURA_01" : req.PrinterId.Trim();
        int copies = req.Copies > 0 ? req.Copies : 1;
        string method = req.ViaBridge ? "bridge" : "direct";

        var printer = await _db.SmPrinterInfos.AsNoTracking().FirstOrDefaultAsync(p => p.ID_Printer == printerId);
        if (printer == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy máy in '{printerId}' trong SM_Printer_Info." });

        if (!int.TryParse(printer.Port_Printer, out int port))
            return BadRequest(new { ok = false, error = $"Port máy in '{printerId}' không hợp lệ: '{printer.Port_Printer}'." });

        LotWipData lotWip;
        try
        {
            lotWip = await BuildLotWipDataAsync(lotNumber);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }

        try
        {
            for (int i = 0; i < copies; i++)
            {
                if (req.ViaBridge)
                    await SendViaBridgeAsync(lotWip.Zpl, printer.IP_Printer, port);
                else
                    await _sakura.SendZplAsync(printer.IP_Printer, port, lotWip.Zpl);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                method,
                error = $"Gửi lệnh in thất bại: {ex.Message}",
                debugLocalRouteIp = req.ViaBridge ? null : GetLocalRouteIp(printer.IP_Printer)
            });
        }

        return Ok(new
        {
            ok = true,
            method,
            lotNumber,
            productWO = lotWip.ProductWO,
            product = lotWip.Product,
            lotQty = lotWip.LotQty,
            printerId,
            ip = printer.IP_Printer,
            port,
            copies
        });
    }

    // Gửi qua PrintService cục bộ (localhost:8021/print) — cùng contract browser đang dùng
    // (xem PrintService/server.js app.post('/print')). Ném exception với message rõ ràng nếu
    // bridge trả lỗi hoặc không kết nối được (bridge không chạy trên máy này, chẳng hạn).
    private async Task SendViaBridgeAsync(string zpl, string printerIp, int printerPort)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        HttpResponseMessage res;
        try
        {
            res = await client.PostAsJsonAsync($"{BridgeBaseUrl}/print", new { zpl, printerIp, printerPort });
        }
        catch (Exception ex)
        {
            throw new Exception($"Không kết nối được PrintService ({BridgeBaseUrl}) — bridge có đang chạy trên máy này không? ({ex.Message})");
        }

        string body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"PrintService trả lỗi ({(int)res.StatusCode}): {body}");
    }

    // Hỏi thẳng OS: "để tới host này, mày sẽ đi ra bằng IP/card mạng nội bộ nào?" — dùng
    // UdpClient.Connect() (chỉ set route ở tầng kernel, KHÔNG gửi gói tin nào cả, nên không
    // cần cổng đó có mở hay không) rồi đọc LocalEndPoint. Chỉ để CHẨN ĐOÁN khi gửi in thất
    // bại — giúp phân biệt "server không có route thật tới LAN nhà máy" (IP trả về không
    // thuộc dải máy in, VD do VPN chỉ áp dụng cho session user, không áp dụng cho service)
    // với các nguyên nhân khác (firewall chặn theo port cụ thể, máy in tắt...).
    private static string GetLocalRouteIp(string destHost)
    {
        try
        {
            using var udp = new UdpClient();
            udp.Connect(destHost, 9);
            return ((IPEndPoint)udp.Client.LocalEndPoint!).Address.ToString();
        }
        catch (Exception ex)
        {
            return $"(không xác định được: {ex.Message})";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CÁCH IN ĐANG DÙNG THẬT — HÀNG ĐỢI (QUEUE), vì server chạy PrintApp KHÔNG có route
    // mạng thật tới LAN nhà máy (đã xác nhận qua debugLocalRouteIp trả về IP public thay vì
    // dải máy in) — cả "direct" lẫn "bridge" ở trên đều không thể hoạt động trong trường hợp
    // đó. Thay vì server tự gửi ZPL, nó chỉ LƯU lệnh in vào SM_ExternalPrint_Queue; PrintService
    // (server.js) chạy trên máy trong xưởng (cùng LAN với máy in) sẽ tự poll 2 API bên dưới để
    // lấy lệnh in về và tự in — xem thêm SM_ExternalPrint_Queue_CreateTable.sql.
    // ═══════════════════════════════════════════════════════════════════════════

    // POST /api/external/print/serial/queue
    // Body: { "serial": "..." (Lot Number), "printerId": "SAKURA_01" (mặc định), "copies": 1 (mặc định) }
    // Trả về ngay lập tức (không chờ in) — chỉ tra cứu + ghi DB, không phụ thuộc mạng/máy in.
    [HttpPost("/api/external/print/serial/queue")]
    public async Task<IActionResult> QueuePrintSerial([FromBody] ExternalPrintRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Serial))
            return BadRequest(new { ok = false, error = "Thiếu Serial." });

        string lotNumber = req.Serial.Trim();
        string printerId = string.IsNullOrWhiteSpace(req.PrinterId) ? "SAKURA_01" : req.PrinterId.Trim();
        int copies = req.Copies > 0 ? req.Copies : 1;

        var printer = await _db.SmPrinterInfos.AsNoTracking().FirstOrDefaultAsync(p => p.ID_Printer == printerId);
        if (printer == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy máy in '{printerId}' trong SM_Printer_Info." });

        if (!int.TryParse(printer.Port_Printer, out int port))
            return BadRequest(new { ok = false, error = $"Port máy in '{printerId}' không hợp lệ: '{printer.Port_Printer}'." });

        LotWipData lotWip;
        try
        {
            lotWip = await BuildLotWipDataAsync(lotNumber);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }

        var now = SakuraService.VietnamNow();
        var items = Enumerable.Range(0, copies).Select(_ => new ExternalPrintQueueItem
        {
            Serial = lotNumber,
            PrinterId = printerId,
            PrinterIp = printer.IP_Printer,
            PrinterPort = port,
            Zpl = lotWip.Zpl,
            Status = "Pending",
            CreatedAt = now,
            ProductWO = lotWip.ProductWO,
            Product = lotWip.Product,
            LotQty = lotWip.LotQty
        }).ToList();

        _db.ExternalPrintQueueItems.AddRange(items);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            ok = true,
            queued = true,
            ids = items.Select(x => x.Id),
            lotNumber,
            productWO = lotWip.ProductWO,
            product = lotWip.Product,
            lotQty = lotWip.LotQty,
            printerId,
            copies
        });
    }

    // GET /api/external/print/queue/pending?limit=10
    // PrintService (máy trong xưởng) gọi định kỳ để lấy lệnh in đang chờ. Đánh dấu ngay
    // Status="Claimed" trước khi trả về để lần poll tiếp theo không lấy trùng lệnh đang xử lý.
    [HttpGet("/api/external/print/queue/pending")]
    public async Task<IActionResult> GetPendingQueue([FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);

        var items = await _db.ExternalPrintQueueItems
            .Where(x => x.Status == "Pending")
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync();

        if (items.Count == 0)
            return Ok(new { ok = true, items = Array.Empty<object>() });

        var now = SakuraService.VietnamNow();
        foreach (var item in items)
        {
            item.Status = "Claimed";
            item.ClaimedAt = now;
        }
        await _db.SaveChangesAsync();

        return Ok(new
        {
            ok = true,
            items = items.Select(x => new { x.Id, x.Serial, x.PrinterIp, x.PrinterPort, x.Zpl })
        });
    }

    // POST /api/external/print/queue/{id}/complete
    // Body: { "success": true/false, "error": "..." (nếu fail) }
    // PrintService gọi lại sau khi tự in xong (hoặc in lỗi) để chốt trạng thái lệnh in.
    [HttpPost("/api/external/print/queue/{id}/complete")]
    public async Task<IActionResult> CompleteQueueItem(int id, [FromBody] CompleteQueueRequest req)
    {
        var item = await _db.ExternalPrintQueueItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null)
            return NotFound(new { ok = false, error = $"Không tìm thấy lệnh in Id={id}." });

        item.Status = req.Success ? "Printed" : "Failed";
        item.CompletedAt = SakuraService.VietnamNow();
        item.ErrorMessage = req.Success ? null : req.Error;
        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }
}
