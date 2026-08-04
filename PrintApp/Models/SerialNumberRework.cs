using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Bảng tích hợp cho hệ thống nhập kết quả sản xuất bên ngoài (team khác phụ trách): mỗi lần
// trạm SN Label reprint 1 tem gift box dưới 1 Rework Work Order, insert 1 dòng MỚI vào đây
// TRƯỚC khi gọi bước nhập kết quả sản xuất — hệ thống bên ngoài tự check bảng này để biết
// serial này là rework và bỏ qua validate trùng serial khi thêm bản ghi kết quả sản xuất mới
// dưới Rework WO (song song với bản ghi gốc dưới WO sản xuất ban đầu).
[Table("SM_SerialNumber_Rework")]
public class SerialNumberRework
{
    [Key]
    public int Id { get; set; }

    // Rework WO.
    [Required]
    [StringLength(50)]
    public string WorkOrder { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string SerialNumber { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}
