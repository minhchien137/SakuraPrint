using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Tài khoản đăng nhập cho toàn site Sakura (cookie auth, xem AccountController) — phân quyền
// theo Role (Worker/LineLeader/Admin). Trước đây chỉ dùng để mở khoá tab Reprint qua modal
// (VerifyReprintLogin, đã xoá) — nay là gate chung cho mọi trang /sakura/*. Bảng đổi tên từ
// SM_UserPermission -> SM_Sakura_UserPermission, xem SM_UserPermission_RenameToSakuraAndAddRole.sql.
// Role "LineLeader" trước đây tên là "Supervisor" — đổi tên theo yêu cầu, xem
// SM_Sakura_UserPermission_RenameSupervisorToLineLeader.sql.
[Table("SM_Sakura_UserPermission")]
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

    // "Worker" | "LineLeader" | "Admin" — xem CK_SM_Sakura_UserPermission_Role.
    [Required]
    [StringLength(20)]
    public string Role { get; set; } = "Worker";

    public bool IsActive { get; set; } = true;

    [StringLength(100)]
    public string? DisplayName { get; set; }
}

public static class UserRoles
{
    public const string Worker = "Worker";
    public const string LineLeader = "LineLeader";
    public const string Admin = "Admin";

    public static readonly string[] All = { Worker, LineLeader, Admin };
}

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
