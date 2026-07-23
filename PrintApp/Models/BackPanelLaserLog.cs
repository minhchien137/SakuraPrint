using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Log mỗi lần quét Serial Number ở trạm Laser (Back Panel) — đối chiếu màu Serial
// với màu Work Order. Status là "PASS", "FAIL", hoặc "PENDING" (đã qua bước 1-3, đang chờ
// trình duyệt in laser qua bridge cục bộ + báo kết quả qua ReportPrintResult).
[Table("SM_BackPanelLaserLog")]
public class BackPanelLaserLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string WorkOrder { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string SerialNumber { get; set; } = "";

    [Required]
    [StringLength(10)]
    public string Status { get; set; } = "";

    // NULL nếu PASS toàn bộ quy trình; 1-4 = bước quy trình bị FAIL
    // (1=Scan Color Check, 2=Check Serial Already Entered, 3=Enter Production Result, 4=Print Laser).
    public int? FailedStep { get; set; }

    // subName đã dùng khi nhập KQSX (bước 3) thành công — NULL nếu bước 3 chưa pass.
    [StringLength(50)]
    public string? ProductionResultSubName { get; set; }

    // Chi tiết lý do FAIL (message thật từ API hoặc exception khi query DB tính subName) —
    // NULL nếu PASS. Dùng để tra nguyên nhân khi lỗi lặp lại, vì message hiện ra cho vận
    // hành viên (checkResultMessage/inputResultMessage) đôi khi bị thay bằng câu chung chung.
    [StringLength(500)]
    public string? FailReason { get; set; }

    public DateTime Timeline { get; set; }
}
