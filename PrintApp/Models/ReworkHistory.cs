namespace PrintApp.Models;

// DTO cho trang History (/sakura/rework/history) — đọc từ SM_LabelPvidEANLog.
public class ReworkHistoryItemDto
{
    public int Id { get; set; }
    public string WorkOrder { get; set; } = "";
    public string? Pvid { get; set; }
    public string? WoColor { get; set; }
    public string? EanRw { get; set; }
    public string? EanGiftBox { get; set; }
    public string? EanColor { get; set; }
    public string Status { get; set; } = "";
    public int? FailedStep { get; set; }
    public DateTime Timeline { get; set; }
}

public class ReworkHistoryPageDto
{
    public List<ReworkHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
