using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// Trang quản trị tài khoản Sakura (SM_Sakura_UserPermission) — chỉ Admin vào được. Tạo/sửa
// role/khoá/mở tài khoản/đổi mật khẩu qua UI thay vì phải tự viết INSERT SQL như trước.
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Users()
    {
        var users = await _db.UserPermissions.OrderBy(x => x.Username).ToListAsync();
        ViewBag.MsgCode = TempData["MsgCode"];
        ViewBag.MsgParam = TempData["MsgParam"];
        ViewBag.ErrCode = TempData["ErrCode"];
        ViewBag.ErrParam = TempData["ErrParam"];
        return View(users);
    }

    // Thông báo thành công/lỗi lưu dưới dạng mã (key trong sakura-i18n.js, xem admin.msg*) +
    // tham số ({value}) thay vì chuỗi tiếng Việt cứng — Users.cshtml tự dịch sang EN/ZH theo
    // ngôn ngữ đang chọn (giống cơ chế errorCode/errorParams của các API JSON khác trong Sakura).
    [HttpPost("/admin/users/create")]
    public async Task<IActionResult> CreateUser([FromForm] string username, [FromForm] string password, [FromForm] string role, [FromForm] string? displayName)
    {
        username = (username ?? "").Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            TempData["ErrCode"] = "admin.msgMissingCredentials";
            return RedirectToAction(nameof(Users));
        }
        if (!UserRoles.All.Contains(role))
        {
            TempData["ErrCode"] = "admin.msgInvalidRole";
            return RedirectToAction(nameof(Users));
        }
        if (await _db.UserPermissions.AnyAsync(x => x.Username == username))
        {
            TempData["ErrCode"] = "admin.msgUserExists";
            TempData["ErrParam"] = username;
            return RedirectToAction(nameof(Users));
        }

        _db.UserPermissions.Add(new UserPermission
        {
            Username = username,
            PasswordHash = SimplePasswordHasher.Hash(password),
            CreatedAt = SakuraService.VietnamNow(),
            Role = role,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            IsActive = true
        });
        await _db.SaveChangesAsync();

        TempData["MsgCode"] = "admin.msgCreated";
        TempData["MsgParam"] = username;
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/admin/users/{id:int}/update")]
    public async Task<IActionResult> UpdateUser(int id, [FromForm] string role, [FromForm] string? displayName, [FromForm] bool isActive)
    {
        var user = await _db.UserPermissions.FindAsync(id);
        if (user == null)
        {
            TempData["ErrCode"] = "admin.msgUserNotFound";
            return RedirectToAction(nameof(Users));
        }
        if (!UserRoles.All.Contains(role))
        {
            TempData["ErrCode"] = "admin.msgInvalidRole";
            return RedirectToAction(nameof(Users));
        }

        // Chặn tự đổi quyền/tự khoá chính tài khoản đang đăng nhập — tránh tự khoá mình ra
        // khỏi hệ thống khi không còn Admin nào khác đang online để mở lại.
        bool isSelf = string.Equals(user.Username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);
        if (isSelf && (role != UserRoles.Admin || !isActive))
        {
            TempData["ErrCode"] = "admin.msgCannotSelfDemote";
            return RedirectToAction(nameof(Users));
        }

        user.Role = role;
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        user.IsActive = isActive;
        await _db.SaveChangesAsync();

        TempData["MsgCode"] = "admin.msgUpdated";
        TempData["MsgParam"] = user.Username;
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/admin/users/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromForm] string newPassword)
    {
        var user = await _db.UserPermissions.FindAsync(id);
        if (user == null)
        {
            TempData["ErrCode"] = "admin.msgUserNotFound";
            return RedirectToAction(nameof(Users));
        }
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            TempData["ErrCode"] = "admin.msgPasswordTooShort";
            return RedirectToAction(nameof(Users));
        }

        user.PasswordHash = SimplePasswordHasher.Hash(newPassword);
        await _db.SaveChangesAsync();

        TempData["MsgCode"] = "admin.msgPasswordReset";
        TempData["MsgParam"] = user.Username;
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/admin/users/{id:int}/delete")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.UserPermissions.FindAsync(id);
        if (user == null)
        {
            TempData["ErrCode"] = "admin.msgUserNotFound";
            return RedirectToAction(nameof(Users));
        }

        // Chặn tự xoá chính tài khoản đang đăng nhập — cùng lý do với chặn tự đổi quyền ở
        // UpdateUser (tránh tự khoá mình ra khỏi hệ thống).
        bool isSelf = string.Equals(user.Username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);
        if (isSelf)
        {
            TempData["ErrCode"] = "admin.msgCannotSelfDelete";
            return RedirectToAction(nameof(Users));
        }

        _db.UserPermissions.Remove(user);
        await _db.SaveChangesAsync();

        TempData["MsgCode"] = "admin.msgDeleted";
        TempData["MsgParam"] = user.Username;
        return RedirectToAction(nameof(Users));
    }

    // Xem lại nhật ký ai vào trang nào lúc mấy giờ (SM_Sakura_UserActivityLog, ghi bởi
    // Filters/LogPageVisitFilter — chỉ log các trang thao tác chính, không log History).
    [HttpGet("/admin/activity-log")]
    public async Task<IActionResult> ActivityLog([FromQuery] string? username, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] int page = 1)
    {
        const int pageSize = 30;
        page = page < 1 ? 1 : page;

        var query = _db.UserActivityLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(x => x.Username.Contains(username.Trim()));
        if (dateFrom.HasValue)
            query = query.Where(x => x.Timeline >= dateFrom.Value.Date);
        if (dateTo.HasValue)
            query = query.Where(x => x.Timeline < dateTo.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query.OrderByDescending(x => x.Timeline)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        ViewBag.FilterUsername = username;
        ViewBag.FilterDateFrom = dateFrom?.ToString("yyyy-MM-dd");
        ViewBag.FilterDateTo = dateTo?.ToString("yyyy-MM-dd");
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Total = total;
        return View(items);
    }
}
