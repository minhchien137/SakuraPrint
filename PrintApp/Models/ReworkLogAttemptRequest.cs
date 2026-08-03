namespace PrintApp.Models;

// Body của POST /api/sakura/rework/log-attempt — ghi 1 dòng audit trail vào SM_LabelPvidEANLog
// mỗi khi 1 bước trong Process (Check Work Order / Check Color & EAN of The Gift Box / Print
// Label) ở trạm Rework được thử, kể cả FAIL.
public class ReworkLogAttemptRequest
{
    public string WorkOrder { get; set; } = "";
    public string? Pvid { get; set; }
    public string? WoColor { get; set; }
    public string? EanRw { get; set; }
    public string? EanGiftBox { get; set; }
    public string? EanColor { get; set; }
    public string? Status { get; set; }
    public int? FailedStep { get; set; }
}
