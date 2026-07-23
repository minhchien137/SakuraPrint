using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;

namespace PrintApp.Services;

public class ToastService
{
    private readonly AppDbContext _context;

    public ToastService(AppDbContext context)
    {
        _context = context;
    }

    // ── Serial validation ──────────────────────────────────────────────────────

    public async Task<bool> CheckSerialExistAsync(string prefix, string serial)
    {
        return await _context.AstroLabelDatas
            .AnyAsync(d => d.PackageID != null
                        && d.PackageID.StartsWith(prefix)
                        && d.Serial != null
                        && d.Serial.Contains(serial)
                        && d.isDeleted == false);
    }

    // ── Label data CRUD ────────────────────────────────────────────────────────

    public async Task<AstroLabelData> CreateLabelDataAsync(AstroLabelDataDto dto)
    {
        var entity = new AstroLabelData
        {
            Date = dto.Date,
            PackageID = dto.PackageID,
            Serial = dto.Serial,
            ScanDate = dto.ScanDate,
            PalletID = dto.PalletID,
            EmployeeID = dto.EmployeeID,
            CountSerial = dto.CountSerial
        };
        _context.AstroLabelDatas.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<int> CountPalletAsync(string palletId)
    {
        return await _context.AstroLabelDatas
            .CountAsync(d => d.PalletID == palletId && d.isDeleted == false);
    }

    public async Task<List<AstroLabelData>> GetPalletAsync(string palletId)
    {
        return await _context.AstroLabelDatas
            .Where(d => d.PalletID == palletId && d.isDeleted == false)
            .ToListAsync();
    }

    public async Task<bool> DeleteBoxAsync(string serial)
    {
        var entity = await _context.AstroLabelDatas
            .FirstOrDefaultAsync(d => d.Serial == serial);
        if (entity == null) return false;
        _context.AstroLabelDatas.Remove(entity);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Printer info ───────────────────────────────────────────────────────────

    public async Task<PrinterInfo?> GetPrinterAsync(string printerId)
    {
        return await _context.PrinterInfos
            .FirstOrDefaultAsync(p => p.ID_Printer == printerId);
    }

    // ── ZPL generation ─────────────────────────────────────────────────────────

    public string GenerateSerialBlock(string? serial, string barcodeCoords, string textCoords)
    {
        if (string.IsNullOrEmpty(serial)) return "";
        return $@"
^BY4,3,102^FT{barcodeCoords}^BCN,,N,N
^FH\^FD>:{serial}^FS
^FT{textCoords}^A@N,33,34,TT0003M_^FH\^CI28^FD{serial}^FS^CI27
";
    }

    public string BuildToastZpl(PrinterInfo printer, ToastLabelRequest req)
    {
        string template = printer.ZPL_Temp ?? "";
        string[] lotParts = (req.LotId ?? "--").Split('-');
        string pn1 = req.SkuNumber?.Length >= 2 ? req.SkuNumber.Substring(0, 2) : "";
        string pn2 = req.SkuNumber?.Length >= 2 ? req.SkuNumber.Substring(2) : req.SkuNumber ?? "";

        (string desc, string descFr, string modelNumber) = ResolveSkuMeta(req.SkuNumber);

        return template
            .Replace("{toastPartNumber}", req.SkuNumber ?? "")
            .Replace("{toastPartNumber1}", pn1)
            .Replace("{toastPartNumber2}", pn2)
            .Replace("{modelNumber}", modelNumber)
            .Replace("{desc}", desc)
            .Replace("{descFr}", descFr)
            .Replace("{quantity}", req.Quantity.ToString())
            .Replace("{serialBlock1}", GenerateSerialBlock(req.SerialNumber1, "264,1043", "485,1077"))
            .Replace("{serialBlock2}", GenerateSerialBlock(req.SerialNumber2, "264,1193", "485,1227"))
            .Replace("{serialBlock3}", GenerateSerialBlock(req.SerialNumber3, "264,1343", "485,1377"))
            .Replace("{serialBlock4}", GenerateSerialBlock(req.SerialNumber4, "264,1493", "485,1527"))
            .Replace("{serialBlock5}", GenerateSerialBlock(req.SerialNumber5, "264,1643", "485,1677"))
            .Replace("{lotId}", req.LotId ?? "")
            .Replace("{poNumber}", req.PoNumber ?? "")
            .Replace("{lotId1}", lotParts.Length > 0 ? lotParts[0] : "")
            .Replace("{lotId2}", lotParts.Length > 1 ? lotParts[1] : "")
            .Replace("{lotId3}", lotParts.Length > 2 ? lotParts[2] : "");
    }

    public async Task<(bool success, string error, string zpl)> BuildPalletZplAsync(ToastPalletRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return (false, "Thiếu Pallet ID.", "");

        var printer = await _context.PrinterInfos
            .FirstOrDefaultAsync(p => p.Name_Printer == "Pallet_Toast");
        if (printer == null)
            return (false, "Không tìm thấy máy in Pallet_Toast trong DB.", "");

        var list = await _context.AstroLabelDatas
            .Where(d => d.PalletID == req.PalletId && d.isDeleted == false)
            .ToListAsync();

        if (list.Count == 0)
            return (false, "Pallet chưa có thùng nào.", "");

        string ser1 = string.Join("", list.Take(10).Select(r => r.Serial)).TrimEnd(',');
        string ser2 = list.Count > 10
            ? string.Join("", list.Skip(10).Take(10).Select(r => r.Serial)).TrimEnd(',')
            : "";
        string ser3 = list.Count > 20
            ? string.Join("", list.Skip(20).Select(r => r.Serial)).TrimEnd(',')
            : "";

        string[] lotParts = (req.LotId ?? "--").Split('-');
        string pn1 = req.SkuNumber?.Length >= 2 ? req.SkuNumber.Substring(0, 2) : "";
        string pn2 = req.SkuNumber?.Length >= 2 ? req.SkuNumber.Substring(2) : req.SkuNumber ?? "";

        string zpl = (printer.ZPL_Temp ?? "")
            .Replace("{ser1}", ser1)
            .Replace("{toastPartNumber}", req.SkuNumber ?? "")
            .Replace("{toastPartNumber1}", pn1)
            .Replace("{toastPartNumber2}", pn2)
            .Replace("{modelNumber}", req.ModelNumber ?? "")
            .Replace("{desc}", req.Desc ?? "")
            .Replace("{descFr}", req.DescFr ?? "")
            .Replace("{poNumber}", req.PoNumber ?? "")
            .Replace("{lotId}", req.LotId ?? "")
            .Replace("{lotId1}", lotParts.Length > 0 ? lotParts[0] : "")
            .Replace("{lotId2}", lotParts.Length > 1 ? lotParts[1] : "")
            .Replace("{lotId3}", lotParts.Length > 2 ? lotParts[2] : "");

        // ser2: nếu rỗng → xóa cả lệnh QR thứ 2
        if (string.IsNullOrEmpty(ser2))
            zpl = zpl.Replace("^FT497,1266^BQN,2,3 ^FH\\^FDLA,{ser2}^FS", "");
        else
            zpl = zpl.Replace("{ser2}", ser2);

        // ser3: nếu rỗng → xóa cả lệnh QR thứ 3
        if (string.IsNullOrEmpty(ser3))
            zpl = zpl.Replace("^FT838,1266^BQN,2,3 ^FH\\^FDLA,{ser3}^FS", "");
        else
            zpl = zpl.Replace("{ser3}", ser3);

        return (true, "", zpl);
    }

    // ── TCP send ───────────────────────────────────────────────────────────────

    public async Task SendZplAsync(string host, int port, string zpl, int copies = 1)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port);
        using var stream = client.GetStream();
        byte[] bytes = Encoding.UTF8.GetBytes(zpl);
        for (int i = 0; i < copies; i++)
        {
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await Task.Delay(50);
        }
    }

    // ── SKU metadata lookup ────────────────────────────────────────────────────

    public static (string desc, string descFr, string modelNumber) ResolveSkuMeta(string? sku)
    {
        return sku switch
        {
            "HW0032" or "HW0172" or "HW0170" =>
                ("Toast Go  3 Charging Dock", "Station de recharge Toast Go  3", "TDC300"),
            _ => ("", "", "")
        };
    }
}