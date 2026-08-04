using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Log MỖI lần thử một bước trong Process ở trạm Rework (1 Check Work Order -> 2 Check Color &
// EAN of The Gift Box -> 3 Print Label) — kể cả các lần FAIL, không bị ghi đè. Cùng vai trò với
// SM_SNLabelScanLog ở trạm SnLabel.
[Table("SM_LabelPvidEANLog")]
public class LabelPvidEanLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string WorkOrder { get; set; } = "";

    // Mã sản phẩm (bracket code từ Odoo, vd "RM15A-1001RW") — dùng để check bước 1 (hậu tố "RW").
    [StringLength(50)]
    public string? Pvid { get; set; }

    [StringLength(20)]
    public string? WoColor { get; set; }

    // EAN thật của sản phẩm Rework — lấy từ Odoo lúc lookup Work Order (đối chiếu bước 1).
    [Column("EanRW")]
    [StringLength(30)]
    public string? EanRw { get; set; }

    // EAN người vận hành quét trên hộp quà thật (bước 2 — Check Color & EAN of The Gift Box).
    [StringLength(30)]
    public string? EanGiftBox { get; set; }

    [StringLength(20)]
    public string? EanColor { get; set; }

    [Required]
    [StringLength(10)]
    public string Status { get; set; } = "";

    // NULL nếu PASS toàn bộ. 1=Check Work Order, 2=Check Color & EAN of The Gift Box, 3=Print Label.
    public int? FailedStep { get; set; }

    public DateTime Timeline { get; set; }
}
