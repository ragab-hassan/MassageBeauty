using System.ComponentModel.DataAnnotations;
using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _sign;
    private readonly UserManager<AppUser> _users;
    private readonly AppDb _db;
    public AccountController(SignInManager<AppUser> s, UserManager<AppUser> u, AppDb db) { _sign = s; _users = u; _db = db; }

    private IActionResult Back(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Go", "Home", new { area = "" });

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Back(returnUrl);
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        const string bad = "اسم المستخدم أو كلمة المرور غير صحيحة";
        username = (username ?? "").Trim();
        var user = username == "" ? null : await _users.FindByNameAsync(username);
        if (user == null && username != "") user = await _users.FindByNameAsync(Phones.Normalize(username)); // العميل يدخل برقم جواله
        if (user == null || !user.IsActive) { ViewBag.Error = bad; return View(); }

        var check = await _sign.CheckPasswordSignInAsync(user, password ?? "", lockoutOnFailure: true);
        if (check.IsLockedOut) { ViewBag.Error = "تم إيقاف الدخول مؤقتًا بسبب محاولات كثيرة، حاول لاحقًا"; return View(); }
        if (!check.Succeeded) { ViewBag.Error = bad; return View(); }

        // المالك والعميل لا يخضعان لفحص الاشتراك، أما المشترك والعاملون فيُفحص اشتراك النشاط
        if (!await _users.IsInRoleAsync(user, "Owner") && !await _users.IsInRoleAsync(user, "Customer"))
        {
            var sub = user.SubscriberId == null ? null : await _db.Subscribers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.SubscriberId);
            if (sub == null || !sub.IsActive) { ViewBag.Error = "هذا الحساب موقوف، تواصل مع إدارة المنصة"; return View(); }
            var valid = await _db.Subscriptions.AnyAsync(s => s.SubscriberId == sub.Id && s.EndDate >= DateTime.Today);
            if (!valid) { ViewBag.Error = "انتهى اشتراك النشاط، تواصل مع إدارة المنصة للتجديد"; return View(); }
        }

        await _sign.SignInAsync(user, isPersistent: true);
        return Back(returnUrl);
    }

    // ===== تسجيل حساب عميل =====
    [HttpGet, AllowAnonymous]
    public IActionResult Register(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Back(returnUrl);
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string fullName, string phone, string password, string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        var norm = Phones.Normalize(phone);
        if (string.IsNullOrWhiteSpace(fullName) || norm.Length < 9 || norm.Length > 15)
        { ViewBag.Error = "أدخل الاسم ورقم جوال صحيح"; return View(); }

        var user = new AppUser { UserName = norm, FullName = fullName.Trim(), PhoneNumber = norm, EmailConfirmed = true };
        var res = await _users.CreateAsync(user, password ?? "");
        if (!res.Succeeded)
        {
            ViewBag.Error = res.Errors.Any(e => e.Code == "DuplicateUserName") ? "هذا الرقم مسجل من قبل، سجّل الدخول" : string.Join(" | ", res.Errors.Select(e => e.Description));
            return View();
        }
        await _users.AddToRoleAsync(user, "Customer");
        await _sign.SignInAsync(user, isPersistent: true);
        return Back(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await _sign.SignOutAsync(); return RedirectToAction("Index", "Home", new { area = "" }); }

    [AllowAnonymous] public IActionResult Denied() => View();
}
