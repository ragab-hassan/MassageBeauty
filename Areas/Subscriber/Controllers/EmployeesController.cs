using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers.SubscriberArea;

[Area("Subscriber"), Authorize(Roles = "Subscriber")]
public class EmployeesController : Controller
{
    private readonly AppDb _db;
    private readonly UserManager<AppUser> _users;
    private readonly ImageStore _img;
    public EmployeesController(AppDb db, UserManager<AppUser> users, ImageStore img) { _db = db; _users = users; _img = img; }

    private async Task<AppUser> Me() => (await _users.GetUserAsync(User))!;

    public async Task<IActionResult> Index()
    {
        var me = await Me();
        var sid = me.SubscriberId;
        ViewBag.Business = await _db.Subscribers.Include(s => s.Subscriptions).ThenInclude(x => x.Plan).FirstAsync(s => s.Id == sid);
        var list = await _db.Users.Where(u => u.SubscriberId == sid && u.Id != me.Id).OrderBy(u => u.FullName).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? id, string fullName, string userName, string? newPassword, string? phone,
                                          string? title, bool showOnPublic, IFormFile? photo)
    {
        var me = await Me();
        var sid = me.SubscriberId!.Value;
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(userName))
        { TempData["Err"] = "الاسم واسم المستخدم مطلوبان"; return RedirectToAction(nameof(Index)); }
        userName = userName.Trim();

        var (photoUrl, photoErr) = await _img.SaveAsync(photo, sid);
        if (photoErr != null) { TempData["Err"] = photoErr; return RedirectToAction(nameof(Index)); }

        if (string.IsNullOrEmpty(id))
        {
            var biz = await _db.Subscribers.Include(s => s.Subscriptions).ThenInclude(x => x.Plan).FirstAsync(s => s.Id == sid);
            var max = biz.Current?.Plan.MaxEmployees;
            var count = await _db.Users.CountAsync(u => u.SubscriberId == sid && u.Id != me.Id);
            if (max != null && count >= max) { _img.Delete(photoUrl, sid); TempData["Err"] = $"وصلت للحد الأقصى للعاملين في باقتك ({max})"; return RedirectToAction(nameof(Index)); }

            var emp = new AppUser { UserName = userName, FullName = fullName, PhoneNumber = phone, SubscriberId = sid, EmailConfirmed = true,
                                    Title = title, ShowOnPublic = showOnPublic, PhotoUrl = photoUrl };
            var r = await _users.CreateAsync(emp, newPassword ?? "");
            if (!r.Succeeded) { _img.Delete(photoUrl, sid); TempData["Err"] = string.Join(" | ", r.Errors.Select(e => e.Description)); return RedirectToAction(nameof(Index)); }
            await _users.AddToRoleAsync(emp, "Employee");
            TempData["Ok"] = "تمت إضافة العامل";
        }
        else
        {
            var emp = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.SubscriberId == sid && u.Id != me.Id);
            if (emp == null) return Forbid();

            emp.FullName = fullName; emp.PhoneNumber = phone; emp.Title = title; emp.ShowOnPublic = showOnPublic;
            if (photoUrl != null) { _img.Delete(emp.PhotoUrl, sid); emp.PhotoUrl = photoUrl; }
            if (!string.Equals(emp.UserName, userName, StringComparison.Ordinal))
            {
                var ur = await _users.SetUserNameAsync(emp, userName);
                if (!ur.Succeeded) { TempData["Err"] = string.Join(" | ", ur.Errors.Select(e => e.Description)); return RedirectToAction(nameof(Index)); }
            }
            await _users.UpdateAsync(emp);

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                var token = await _users.GeneratePasswordResetTokenAsync(emp);
                var pr = await _users.ResetPasswordAsync(emp, token, newPassword);
                if (!pr.Succeeded) { TempData["Err"] = string.Join(" | ", pr.Errors.Select(e => e.Description)); return RedirectToAction(nameof(Index)); }
            }
            TempData["Ok"] = "تم حفظ بيانات العامل";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var me = await Me();
        var emp = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.SubscriberId == me.SubscriberId && u.Id != me.Id);
        if (emp == null) return Forbid();
        emp.IsActive = !emp.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var me = await Me();
        var emp = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.SubscriberId == me.SubscriberId && u.Id != me.Id);
        if (emp == null) return Forbid();
        if (await _db.Bookings.AnyAsync(b => b.EmployeeId == id))
        { TempData["Err"] = "لا يمكن حذف عامل لديه حجوزات، أوقفه بدلًا من ذلك"; return RedirectToAction(nameof(Index)); }
        _img.Delete(emp.PhotoUrl, me.SubscriberId!.Value);
        await _users.DeleteAsync(emp);
        TempData["Ok"] = "تم حذف العامل";
        return RedirectToAction(nameof(Index));
    }
}
