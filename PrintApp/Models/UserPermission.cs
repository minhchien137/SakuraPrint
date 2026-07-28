using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Tài khoản đăng nhập để mở khoá tab Reprint ở trang SnLabel (/sakura/snlabel) — bắt buộc
// đăng nhập lại MỖI LẦN vào tab này (không "remember me"), kể cả chưa reload trang, chỉ
// chuyển tab đi rồi quay lại. Xem SakuraController.VerifyReprintLogin.
[Table("SM_UserPermission")]
public class UserPermission
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = "";

    // Lưu dạng "{iterations}.{saltBase64}.{hashBase64}" (PBKDF2/SHA256) — xem
    // Services/SimplePasswordHasher.cs. KHÔNG bao giờ lưu mật khẩu dạng plaintext.
    [Required]
    [StringLength(200)]
    public string PasswordHash { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}

public class ReprintLoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
