using MassageBeauty.Data;
using MassageBeauty.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers.OwnerArea;

[Area("Owner"), Authorize(Roles = "Owner")]
public class DashboardController : Controller
{
    private readonly AppDb _db;
    private readonly UserManager<AppUser> _users;
    public DashboardController(AppDb db, UserManager<AppUser> users) { _db = db; _users = users; }

    private IQueryable<Subscriber> Full() => _db.Subscribers
        .Include(s => s.Users)
        .Include(s => s.Subscriptions).ThenInclude(x => x.Plan);

    public async Task<IActionResult> Index() => View(await Full().OrderByDescending(s => s.CreatedAt).ToListAsync());

    public async Task<IActionResult> Details(int id)
    {
        var s = await Full().FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        ViewBag.Plans = await _db.Plans.Where(p => p.IsActive).OrderBy(p => p.DurationDays).ToListAsync();
        return View(s);
    }

    // ===== إضافة مشترك جديد =====
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Plans = await _db.Plans.Where(p => p.IsActive).OrderBy(p => p.DurationDays).ToListAsync();
        return View(new CreateSubscriberVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSubscriberVm vm)
    {
        ViewBag.Plans = await _db.Plans.Where(p => p.IsActive).OrderBy(p => p.DurationDays).ToListAsync();
        var plan = await _db.Plans.FindAsync(vm.PlanId);
        if (plan == null) ModelState.AddModelError(nameof(vm.PlanId), "اختر باقة");
        if (!ModelState.IsValid) return View(vm);

        await using var tx = await _db.Database.BeginTransactionAsync();

        var sub = new Subscriber
        {
            BusinessName = vm.BusinessName, OwnerName = vm.OwnerName, Phone = vm.Phone, WhatsApp = vm.Phone,
            Address = vm.Address ?? "", City = vm.City ?? "", Notes = vm.Notes
        };
        _db.Subscribers.Add(sub);
        await _db.SaveChangesAsync();

        var user = new AppUser { UserName = vm.UserName.Trim(), FullName = vm.OwnerName, PhoneNumber = vm.Phone, SubscriberId = sub.Id, EmailConfirmed = true };
        var res = await _users.CreateAsync(user, vm.Password);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            await tx.RollbackAsync();
            return View(vm);
        }
        await _users.AddToRoleAsync(user, "Subscriber");

        sub.OwnerUserId = user.Id;
        _db.Subscriptions.Add(new Subscription
        {
            SubscriberId = sub.Id, PlanId = plan!.Id,
            StartDate = vm.StartDate.Date, EndDate = vm.StartDate.Date.AddDays(plan.DurationDays),
            PricePaid = vm.PricePaid
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Ok"] = "تم إنشاء حساب المشترك بنجاح";
        return RedirectToAction(nameof(Details), new { id = sub.Id });
    }

    // ===== التحكم في التجديد / الإيقاف =====
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRenewal(int id)
    {
        var s = await _db.Subscribers.FindAsync(id);
        if (s == null) return NotFound();
        s.AllowRenewal = !s.AllowRenewal;
        await _db.SaveChangesAsync();
        TempData["Ok"] = s.AllowRenewal ? "تم السماح بالتجديد لهذا الحساب" : "تم إيقاف التجديد لهذا الحساب";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var s = await _db.Subscribers.FindAsync(id);
        if (s == null) return NotFound();
        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync();
        TempData["Ok"] = s.IsActive ? "تم تفعيل الحساب" : "تم إيقاف الحساب ومنع دخول المشترك وعامليه";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(int id, int planId, decimal pricePaid)
    {
        var s = await Full().FirstOrDefaultAsync(x => x.Id == id);
        var plan = await _db.Plans.FindAsync(planId);
        if (s == null || plan == null) return NotFound();
        if (!s.AllowRenewal) { TempData["Err"] = "التجديد موقوف لهذا الحساب، فعّله أولًا"; return RedirectToAction(nameof(Details), new { id }); }

        var last = s.Current;
        var start = last != null && last.EndDate.Date >= DateTime.Today ? last.EndDate.Date : DateTime.Today;
        _db.Subscriptions.Add(new Subscription
        {
            SubscriberId = id, PlanId = plan.Id, StartDate = start,
            EndDate = start.AddDays(plan.DurationDays), PricePaid = pricePaid
        });
        await _db.SaveChangesAsync();
        TempData["Ok"] = "تم تجديد الاشتراك";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotes(int id, string? notes, string? phone, string? address, string? city)
    {
        var s = await _db.Subscribers.FindAsync(id);
        if (s == null) return NotFound();
        s.Notes = notes; s.Phone = phone ?? s.Phone; s.Address = address ?? ""; s.City = city ?? "";
        await _db.SaveChangesAsync();
        TempData["Ok"] = "تم حفظ البيانات";
        return RedirectToAction(nameof(Details), new { id });
    }
}
