using Microsoft.AspNetCore.Mvc.Filters;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Filters;

// Ghi 1 dòng vào SM_Sakura_UserActivityLog mỗi lần tài khoản đang đăng nhập vào 1 trang thao
// tác chính của Sakura — gắn [TypeFilter(typeof(LogPageVisitFilter))] lên action tương ứng
// trong SakuraController (Index/SnLabelIndex/CartonSnIndex/ReworkIndex/CartonSnReprint).
// KHÔNG gắn vào action trả JSON (API) hay các trang History — xem SM_Sakura_UserActivityLog_CreateTable.sql.
public class LogPageVisitFilter : IAsyncActionFilter
{
    private readonly AppDbContext _db;

    public LogPageVisitFilter(AppDbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();

        var username = context.HttpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(username) || result.Exception != null)
            return;

        _db.UserActivityLogs.Add(new UserActivityLog
        {
            Username = username,
            Path = context.HttpContext.Request.Path.Value ?? "",
            // Giờ Trung Quốc (UTC+8) — giống các bảng log khác của Sakura (SM_LabelPvidEANLog...).
            Timeline = SakuraService.VietnamNow().AddHours(1)
        });
        await _db.SaveChangesAsync();
    }
}
