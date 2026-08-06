using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

// Login toàn site Sakura — thay cho modal verify-reprint-login cũ (xem SakuraController,
// [Authorize] gắn ở đó). Tài khoản/mật khẩu vẫn ở SM_UserPermission (PasswordHash — PBKDF2/
// SHA256, xem SimplePasswordHasher), nay có thêm Role để phân quyền Worker/LineLeader/Admin.
public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/account/login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost("/account/login")]
    public async Task<IActionResult> Login([FromForm] LoginRequest req)
    {
        var username = (req.Username ?? "").Trim();
        var user = await _db.UserPermissions.FirstOrDefaultAsync(x => x.Username == username);

        if (user == null || !user.IsActive || !SimplePasswordHasher.Verify(req.Password ?? "", user.PasswordHash))
        {
            ViewBag.ReturnUrl = req.ReturnUrl;
            ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu.";
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("DisplayName", user.DisplayName ?? user.Username),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return RedirectToLocal(req.ReturnUrl);
    }

    [HttpPost("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("~/account/login");
    }

    [HttpGet("/account/denied")]
    public IActionResult Denied() => View();

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect("~/sakura");
    }
}
