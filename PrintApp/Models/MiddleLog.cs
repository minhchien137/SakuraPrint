using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Log mỗi lần quét Serial Number ở trạm Nhập kết quả sản xuất (Middle) — đối chiếu màu
// Serial với màu Work Order rồi nhập kết quả sản xuất. Khác BackPanelLaserLog (trạm Laser)
// ở chỗ không có bước in vật lý nào chờ trình duyệt báo kết quả về sau — toàn bộ quy trình
// (Scan Color Check → Check Serial Already Entered → Enter Production Result) chạy đồng bộ
// trong 1 request, nên Status luôn chốt ngay là "PASS" hoặc "FAIL", không có "PENDING".
[Table("SM_MiddleLog")]
public class MiddleLog
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

    // NULL nếu PASS toàn bộ quy trình; 1-3 = bước quy trình bị FAIL
    // (1=Scan Color Check, 2=Check Serial Already Entered, 3=Enter Production Result).
    public int? FailedStep { get; set; }

    // subName đã dùng khi nhập KQSX (bước 3) thành công — NULL nếu bước 3 chưa pass.
    [StringLength(50)]
    public string? ProductionResultSubName { get; set; }

    // Chi tiết lý do FAIL (message thật từ API hoặc exception khi query DB tính subName) —
    // NULL nếu PASS.
    [StringLength(500)]
    public string? FailReason { get; set; }

    public DateTime Timeline { get; set; }
}
