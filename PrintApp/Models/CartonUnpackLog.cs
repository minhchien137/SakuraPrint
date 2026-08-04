using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Lịch sử Unpack/Rework/Repack (/sakura/rework): 1 dòng cho MỖI lần 1 Serial được gỡ ra khỏi 1
// Carton Number để rework — insert lúc Unpack (Status=UNPACKED), rồi UPDATE TẠI CHỖ (không insert
// dòng mới) khi serial đó qua bước Rework ở trạm SN Label (Status=REWORKED, gắn ReworkWorkOrder)
// và khi carton được đóng lại/in lại tem (Status=REPACKED) — mỗi dòng theo dõi trọn vòng đời 1
// lần rework của 1 serial, tra được lịch sử đầy đủ theo cả CartonNumber lẫn SerialNumber.
[Table("SM_CartonUnpack_Log")]
public class CartonUnpackLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(30)]
    public string CartonNumber { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string SerialNumber { get; set; } = "";

    // WO gốc của carton lúc unpack (snapshot từ CartonSnScanLog.WorkOrder tại thời điểm gỡ).
    [Required]
    [StringLength(50)]
    public string WorkOrder { get; set; } = "";

    // Gắn lúc serial qua bước Rework (SN Label reprint dưới Rework WO).
    [StringLength(50)]
    public string? ReworkWorkOrder { get; set; }

    // UNPACKED / REWORKED / REPACKED
    [Required]
    [StringLength(10)]
    public string Status { get; set; } = "";

    public DateTime UnpackedAt { get; set; }
    public DateTime? ReworkedAt { get; set; }
    public DateTime? RepackedAt { get; set; }
}

// ── Request / response DTOs ──────────────────────────────────────────────────

public class CartonForUnpackDto
{
    public string CartonNumber { get; set; } = "";
    public string WorkOrder { get; set; } = "";
    public string? Color { get; set; }
    public List<string> Serials { get; set; } = new();
}

public class UnpackCartonRequest
{
    public string CartonNumber { get; set; } = "";
    public List<string> SerialNumbers { get; set; } = new();
}

public class RepackStatusDto
{
    public string CartonNumber { get; set; } = "";
    public string WorkOrder { get; set; } = "";
    public string? Color { get; set; }
    public List<string> RequiredSerials { get; set; } = new();
    public List<string> PendingReworkedSerials { get; set; } = new();
}

public class RepackPrintRequest
{
    public string CartonNumber { get; set; } = "";
    public string ReworkWorkOrder { get; set; } = "";
    public List<string> ScannedSerials { get; set; } = new();
}

public class RepackReportResultRequest
{
    public string CartonNumber { get; set; } = "";
    public string ReworkWorkOrder { get; set; } = "";
}

public class ReworkReprintRequest
{
    public string SerialNumber { get; set; } = "";
    public string ReworkWorkOrder { get; set; } = "";
    public string? Ean { get; set; }
}

public class CartonUnpackHistoryItemDto
{
    public int Id { get; set; }
    public string CartonNumber { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string WorkOrder { get; set; } = "";
    public string? ReworkWorkOrder { get; set; }
    public string Status { get; set; } = "";
    public DateTime UnpackedAt { get; set; }
    public DateTime? ReworkedAt { get; set; }
    public DateTime? RepackedAt { get; set; }
}

public class CartonUnpackHistoryPageDto
{
    public List<CartonUnpackHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
