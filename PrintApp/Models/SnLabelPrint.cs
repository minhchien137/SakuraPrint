using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

[Table("SM_SNLabelPrint")]
public class SnLabelPrint
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string SerialNumber { get; set; } = "";

    [Required]
    [StringLength(10)]
    public string Model { get; set; } = "";

    [Required]
    [StringLength(2)]
    public string Variant { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string Color { get; set; } = "";

    [Required]
    [StringLength(1)]
    public string ProductionLine { get; set; } = "";

    [Column(TypeName = "date")]
    public DateTime ProductionDate { get; set; }

    [Required]
    [StringLength(3)]
    public string RunningNumber { get; set; } = "";

    public int RunningNumberInt { get; set; }

    public DateTime PrintedAt { get; set; }

    [StringLength(100)]
    public string? PrintedBy { get; set; }

    public Guid BatchId { get; set; }

    [StringLength(50)]
    public string? WorkOrder { get; set; }

    // Reprint by Serial (Manual mode) cập nhật 3 cột này trên chính dòng gốc —
    // không tạo dòng mới, không đụng tới RunningNumber — chỉ để đánh dấu/kiểm soát
    // tem nào đã bị in lại.
    public int ReprintCount { get; set; }
    public DateTime? LastReprintedAt { get; set; }

    [StringLength(100)]
    public string? LastReprintedBy { get; set; }

    // Mã EAN (x_custcode trên Odoo) đã đối chiếu khớp lúc quét — chỉ có ở các dòng đi
    // qua luồng quét EAN + Serial mới (verify-serial); NULL ở các dòng cũ sinh tự động.
    [StringLength(30)]
    public string? Ean { get; set; }

    // "PASS" / "FAIL" / "PENDING" (đã qua Check EAN + Check Color + Check Serial, đang chờ
    // trình duyệt gửi in qua bridge cục bộ + báo kết quả). NULL = dòng cũ (flow tự sinh serial,
    // không đi qua bước check nào) — luôn coi như đã in thành công.
    [StringLength(10)]
    public string? Status { get; set; }

    // NULL nếu PASS toàn bộ; 1=Check EAN, 2=Check Color, 3=Check Serial (đã nhập KQSX chưa),
    // 4=Print Label — cùng quy ước với SM_BackPanelLaserLog.
    public int? FailedStep { get; set; }
}

// ── Request / response DTOs ──────────────────────────────────────────────────

public class SnLabelPrintRequest
{
    public DateTime Date { get; set; }
    public string Variant { get; set; } = "";
    public string Line { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string? PrintedBy { get; set; }
    public string? WorkOrder { get; set; }
}

public class WorkOrderLookupResponse
{
    public string WorkOrder { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Color { get; set; } = "";
    public int TotalQuantity { get; set; }
    public int PrintedQuantity { get; set; }
    public int RemainingQuantity { get; set; }

    // Ngày sản xuất của lần in ĐẦU TIÊN cho Work Order này (null nếu đây là lần in đầu tiên).
    // Toàn bộ nhãn của cùng 1 WO phải dùng chung 1 ngày này — server sẽ tự khóa theo
    // giá trị này khi in tiếp, bất kể ngày hiện tại trên form là gì.
    public DateTime? LockedProductionDate { get; set; }

    // product_id trên Odoo (product.product) của Work Order này — client giữ lại để gửi kèm
    // lúc verify-serial (Check EAN / Check Color / Check Serial), không phải lookup lại.
    public int? ProductId { get; set; }
}

// Quét EAN + Serial Number ở Process (Check EAN -> Check Color & Serial Number -> Print
// Label) — server verify cả 3 bước, ghi 1 dòng lịch sử SnLabelPrint (Status/FailedStep),
// và nếu pass hết trả về ZPL sẵn sàng in cho đúng SerialNumber được quét (không tự sinh serial).
public class SnLabelVerifyRequest
{
    public string WorkOrder { get; set; } = "";
    public int ProductId { get; set; }
    public string ExpectedColor { get; set; } = "";
    public string Ean { get; set; } = "";
    public string SerialNumber { get; set; } = "";
}

// Trình duyệt tự gửi ZPL tới bridge cục bộ (server không với tới máy in) rồi báo kết quả
// về đây để chốt Status/FailedStep (4 = Print Label) của đúng dòng log (logId) vừa tạo.
public class SnLabelReportPrintResultRequest
{
    public int LogId { get; set; }
    public bool Success { get; set; }
}

// Ghi lại 1 lần Check EAN bị FAIL ngay ở bước quét EAN — trước khi có Serial Number, nên
// KHÔNG đi qua verify-serial (bị chặn tại chỗ để tránh nhảy sang Serial Number). Không có
// bước log riêng này thì mọi lần fail EAN sẽ mất dấu hoàn toàn trong SM_SNLabelScanLog.
public class SnLabelEanFailLogRequest
{
    public string WorkOrder { get; set; } = "";
    public string? Ean { get; set; }
}

public class ManualUnlockRequest
{
    public string Password { get; set; } = "";
}

public class SnLabelSerialDto
{
    public string SerialNumber { get; set; } = "";
    public string RunningNumber { get; set; } = "";
    public int RunningNumberInt { get; set; }
}

public class SnLabelPrintResponse
{
    public bool Ok { get; set; } = true;
    public Guid BatchId { get; set; }
    public List<SnLabelSerialDto> Serials { get; set; } = new();
    public string Zpl { get; set; } = "";
    public string PrintMode { get; set; } = "";
    public bool? DirectPrintSent { get; set; }
    public string? DirectPrintError { get; set; }
}

public class SnLabelColorSummaryDto
{
    public string Variant { get; set; } = "";
    public string Color { get; set; } = "";
    public int Count { get; set; }
    public string? LastSerial { get; set; }
}

public class SnLabelStatusDto
{
    public DateTime Date { get; set; }
    public string Line { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Color { get; set; } = "";
    public string? LastSerial { get; set; }
    public string NextSerial { get; set; } = "";
    public int Count { get; set; }
    public int RemainingCapacity { get; set; }
    public List<SnLabelColorSummaryDto> ColorSummary { get; set; } = new();
}

public class SnLabelHistoryItemDto
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Color { get; set; } = "";
    public string ProductionLine { get; set; } = "";
    public DateTime PrintedAt { get; set; }
    public string? PrintedBy { get; set; }
    public Guid BatchId { get; set; }
    public string? WorkOrder { get; set; }
    public int ReprintCount { get; set; }
    public DateTime? LastReprintedAt { get; set; }
    public string? Ean { get; set; }
    public string? Status { get; set; }
    public int? FailedStep { get; set; }
}

public class SnLabelHistoryPageDto
{
    public List<SnLabelHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
