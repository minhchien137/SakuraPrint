using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Hàng đợi lệnh in cho ExternalPrintController — xem SM_ExternalPrint_Queue_CreateTable.sql
// để biết lý do (server không có route mạng thật tới LAN nhà máy).
[Table("SM_ExternalPrint_Queue")]
public class ExternalPrintQueueItem
{
    [Key]
    public int Id { get; set; }
    public string Serial { get; set; } = "";
    public string PrinterId { get; set; } = "";
    public string PrinterIp { get; set; } = "";
    public int PrinterPort { get; set; }
    public string Zpl { get; set; } = "";
    public string Status { get; set; } = "Pending"; // Pending | Claimed | Printed | Failed
    public DateTime CreatedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Giá trị đã tra cứu được cho tem LotWip — lưu lại để xem/debug sau này (Zpl ở trên đã
    // build sẵn với các giá trị này rồi, đây chỉ để tham chiếu).
    public string? ProductWO { get; set; }
    public string? Product { get; set; }
    public decimal? LotQty { get; set; }
}
