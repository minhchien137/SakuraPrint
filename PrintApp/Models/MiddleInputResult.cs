namespace PrintApp.Models;

// ── Request DTO cho trạm Nhập kết quả sản xuất (Middle) ──────────────────────

public class MiddleInputVerifySerialRequest
{
    public string WorkOrder { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string ExpectedColor { get; set; } = "";

    // product_id + tổng số lượng của Work Order — lấy từ WO context lúc lookup,
    // cần cho bước 2/3 (Check Serial Already Entered / Enter Production Result).
    public int? ProductId { get; set; }
    public decimal? TotalQuantity { get; set; }
}

// ── DTO cho trang History (Middle InputResult) — hiển thị SM_MiddleLog ──────

public class MiddleLogItemDto
{
    public int Id { get; set; }
    public string WorkOrder { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public int? FailedStep { get; set; }
    public string? ProductionResultSubName { get; set; }
    public string? FailReason { get; set; }
    public DateTime Timeline { get; set; }
}

public class MiddleLogPageDto
{
    public List<MiddleLogItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
