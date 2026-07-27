using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Map bảng SM_Printer_Info (đã tạo sẵn trong DB, riêng biệt với SVN_Printer_Info_New) —
// chỉ phục vụ ExternalPrintController tra IP/Port máy in theo ID_Printer.
[Table("SM_Printer_Info")]
public class SmPrinterInfo
{
    [Key]
    public string ID_Printer { get; set; } = "";
    public string Name_Printer { get; set; } = "";
    public string IP_Printer { get; set; } = "";
    public string Port_Printer { get; set; } = "";
}

// ── Request DTO cho ExternalPrintController ──────────────────────────────────

public class ExternalPrintRequest
{
    public string Serial { get; set; } = "";
    public string PrinterId { get; set; } = "SAKURA_01";
    public int Copies { get; set; } = 1;

    // false (mặc định) = server tự mở TCP thẳng tới máy in (cách cũ).
    // true = server gọi qua PrintService cục bộ (localhost:8021/print) — CHỈ hoạt động nếu
    // PrintApp và PrintService đang chạy chung 1 máy (xem ExternalPrintController.PrintSerial).
    public bool ViaBridge { get; set; } = false;
}

// Body cho POST /api/external/print/queue/{id}/complete — PrintService báo kết quả sau khi
// tự lấy lệnh in từ hàng đợi (GetPendingQueue) rồi tự in xong.
public class CompleteQueueRequest
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
