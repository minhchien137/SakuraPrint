using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Lưu các ZPL template dùng cho dự án Sakura trong DB — sửa template không cần build lại code.
[Table("SM_Sakura_ZplTemplate")]
public class SakuraZplTemplate
{
    [Key]
    public int Id { get; set; }

    // Khóa định danh template, vd "SnLabel" — mỗi chức năng Sakura có 1 (hoặc nhiều) key riêng.
    [Required]
    [StringLength(50)]
    public string TemplateKey { get; set; } = "";

    [StringLength(100)]
    public string? Name { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string ZplContent { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; }

    [StringLength(100)]
    public string? UpdatedBy { get; set; }
}

public class SakuraZplTemplateUpdateRequest
{
    public string ZplContent { get; set; } = "";
    public string? UpdatedBy { get; set; }
}
