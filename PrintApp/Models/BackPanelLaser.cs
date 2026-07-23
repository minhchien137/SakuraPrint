namespace PrintApp.Models;

// ── Request / response DTOs cho trạm Laser (Back Panel) ─────────────────────

public class LaserVerifySerialRequest
{
    public string WorkOrder { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string ExpectedColor { get; set; } = "";

    // product_id + tổng số lượng của Work Order — lấy từ WO context lúc lookup,
    // cần cho bước 3 (Enter Production Result) khi gọi CheckLotSerialFG/InputProductionResultLog.
    public int? ProductId { get; set; }
    public decimal? TotalQuantity { get; set; }
}

public class LaserReportPrintResultRequest
{
    public int LogId { get; set; }
    public bool Success { get; set; }
}

public class BackPanelLaserLogItemDto
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

public class BackPanelLaserLogPageDto
{
    public List<BackPanelLaserLogItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
