using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 6;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    o.Lockout.MaxFailedAccessAttempts = 8;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Denied";
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
});

builder.Services.AddSingleton<ImageStore>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

string? startupError = null;
try { await Seeder.RunAsync(app.Services, app.Configuration); }
catch (Exception ex)
{
    startupError = ex.ToString();
    app.Logger.LogError(ex, "Database initialization failed");
}
// للتشخيص فقط: فعّل Diagnostics:ShowStartupError في appsettings ثم افتح /startup-error وأغلقه بعد الحل
if (startupError != null && app.Configuration.GetValue<bool>("Diagnostics:ShowStartupError"))
    app.MapGet("/startup-error", () => Results.Text(startupError, "text/plain; charset=utf-8"));

app.Run();
