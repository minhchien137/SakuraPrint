using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Log MỖI lần quét EAN+Serial ở Process (Check EAN -> Check Color & Serial Number ->
// Print Label) trên trang SnLabel — kể cả các lần FAIL, không bị ghi đè. Khác với
// SnLabelPrint (chỉ giữ đúng 1 dòng cho mỗi serial ĐÃ IN THÀNH CÔNG, dùng làm "kho" nhãn
// đã in + tracking Reprint), bảng này là audit trail đầy đủ, cùng vai trò với
// SM_BackPanelLaserLog ở trạm Laser.
[Table("SM_SNLabelScanLog")]
public class SnLabelScanLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string WorkOrder { get; set; } = "";

    [StringLength(30)]
    public string? Ean { get; set; }

    [Required]
    [StringLength(20)]
    public string SerialNumber { get; set; } = "";

    [StringLength(10)]
    public string? Model { get; set; }

    [StringLength(2)]
    public string? Variant { get; set; }

    [StringLength(20)]
    public string? Color { get; set; }

    [StringLength(1)]
    public string? ProductionLine { get; set; }

    [StringLength(3)]
    public string? RunningNumber { get; set; }

    public int? RunningNumberInt { get; set; }

    [Required]
    [StringLength(10)]
    public string Status { get; set; } = "";

    // NULL nếu PASS toàn bộ. 1=Check EAN, 2=Check Color, 4=Print Label. 3=(LỊCH SỬ CŨ, đã bỏ
    // dùng) Check Serial — check ProductionInputLog nhưng NGƯỢC ý nghĩa, không còn ghi mới
    // nữa, chỉ còn ở các dòng cũ. 5=Check Production Result Entered (3.1 — đã nhập rồi/trùng
    // thì fail). 6=Enter Production Result (3.2 — lỗi gọi API nhập). 5/6 thêm SAU nên đứng sau
    // 4 dù 3.1/3.2 chạy TRƯỚC bước 4 (Print Label) trong quy trình thật, để không đổi ý nghĩa
    // các dòng lịch sử cũ đã dùng 4=Print Label.
    public int? FailedStep { get; set; }

    // subName đã dùng khi trạm SnLabel tự nhập KQSX (bước 3.2 "Enter Production Result")
    // thành công — lưu lại để tra cứu/audit. Cùng vai trò với cột cùng tên ở SM_BackPanelLaserLog.
    [StringLength(50)]
    public string? ProductionResultSubName { get; set; }

    public DateTime Timeline { get; set; }
}
