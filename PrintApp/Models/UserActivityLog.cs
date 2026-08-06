using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Ghi lại mỗi lần 1 tài khoản vào 1 trang thao tác chính của Sakura — xem
// Filters/LogPageVisitFilter.cs. KHÔNG log các trang History (chỉ đọc, ít giá trị audit).
[Table("SM_Sakura_UserActivityLog")]
public class UserActivityLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = "";

    [Required]
    [StringLength(200)]
    public string Path { get; set; } = "";

    // Giờ Trung Quốc (UTC+8) — giống các bảng log khác của Sakura (SM_LabelPvidEANLog...).
    public DateTime Timeline { get; set; }
}
