using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ZplService>();

// ── Auth — login toàn site Sakura (SakuraController), phân quyền Worker/LineLeader/Admin.
// Xem AccountController + Models/UserPermission.cs. Session giữ tới khi bấm Đăng xuất
// (persistent cookie, sliding — mỗi lần dùng lại gia hạn tiếp), không tự hết hạn theo giờ.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(60);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SakuraAuth";
        // Gọi API (/api/...) qua fetch() mong 401 JSON để tự xử lý, không phải bị redirect
        // sang trang login (fetch sẽ coi đó là response 200 chứa HTML login, gây lỗi khó hiểu).
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

// ── EF Core — cùng SQL Server với SVN_Tools ───────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProdConnectionString")));

// ── Toast services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ToastSerialService>();

// ── Sakura services ────────────────────────────────────────────────────────────
builder.Services.AddScoped<SakuraService>();
builder.Services.AddScoped<ViidooService>();

// ── Back Panel services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<ProductionResultApiService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseForwardedHeaders();
app.UsePathBase("/print");
app.Use((context, next) =>
{
    context.Request.PathBase = "/print";
    return next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Print}/{action=Index}/{id?}");

app.MapControllers();

app.Run();