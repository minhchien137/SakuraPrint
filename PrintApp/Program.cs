using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;
using PrintApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ZplService>();

// ── Data Protection — bắt buộc phải persist key ra ngoài đĩa khi chạy dưới IIS, nếu không mỗi
// lần App Pool recycle (hoặc chạy nhiều Worker Process/Web Garden) sẽ sinh key mã hoá MỚI, khiến
// cookie đăng nhập vừa set (SignInAsync) không tự giải mã lại được ở request kế tiếp -> âm thầm
// coi là chưa đăng nhập -> văng lại /account/login liên tục (KHÔNG liên quan PathBase). Local chạy
// `dotnet run` không gặp vì key mặc định lưu ở %APPDATA% của user hiện tại, ổn định giữa các lần.
// ⚠️ App Pool Identity cần có quyền ghi vào thư mục này (hoặc bật "Load User Profile" = True
// trong IIS Application Pool > Advanced Settings).
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PrintApp-Sakura", "dp-keys")))
    .SetApplicationName("PrintApp-Sakura");

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
        // ⚠️ CỐ ĐỊNH Path = "/" (KHÔNG phải "/print") — nginx phía trước đã có sẵn:
        //   proxy_cookie_path / /print/;
        // nghĩa là nginx TỰ rewrite path "/" thành "/print/" cho mọi Set-Cookie. Nếu app tự gửi
        // sẵn path="/print" (mặc định ASP.NET Core lấy theo Request.PathBase = "/print"), nginx
        // vẫn cứ thay prefix "/" thành "/print/" → ra "/print/print" (lặp), khiến trình duyệt
        // không bao giờ gửi lại cookie cho /print/sakura (không khớp prefix) — trông như đăng
        // nhập xong nhưng bị đá về lại trang login. Phải gửi path="/" để sau khi qua nginx rewrite
        // mới ra đúng "/print/" (1 lần).
        options.Cookie.Path = "/";
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

// ── Universal Lookup (/sakura/lookup) — thứ tự đăng ký = thứ tự ưu tiên thử khi CanHandle() hoà
// nhau (xem SakuraLookupService.ResolveAsync); CartonLookupHandler luôn CanHandle=true nên phải
// đăng ký SAU CÙNG để làm catch-all.
builder.Services.AddScoped<ISakuraLookupHandler, PalletLookupHandler>();
builder.Services.AddScoped<ISakuraLookupHandler, SerialLookupHandler>();
builder.Services.AddScoped<ISakuraLookupHandler, PoNumberLookupHandler>();
builder.Services.AddScoped<ISakuraLookupHandler, WorkOrderLookupHandler>();
builder.Services.AddScoped<ISakuraLookupHandler, CartonLookupHandler>();
builder.Services.AddScoped<SakuraLookupService>();

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