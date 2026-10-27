using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers.SubscriberArea;

[Area("Subscriber"), Authorize(Roles = "Subscriber")]
public class ProfileController : Controller
{
    private readonly AppDb _db;
    private readonly UserManager<AppUser> _users;
    private readonly ImageStore _img;
    public ProfileController(AppDb db, UserManager<AppUser> users, ImageStore img) { _db = db; _users = users; _img = img; }

    private async Task<Subscriber> Center()
    {
        var me = (await _users.GetUserAsync(User))!;
        return await _db.Subscribers.FirstAsync(s => s.Id == me.SubscriberId);
    }

    public async Task<IActionResult> Index() => View(await Center());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string businessName, string? description, string whatsApp, string phone, string? city, string? address,
                                           double? latitude, double? longitude, int openHour, int closeHour, IFormFile? logo, IFormFile? cover)
    {
        var c = await Center();
        var wa = Phones.Normalize(whatsApp);
        if (string.IsNullOrWhiteSpace(businessName) || wa.Length < 9) { TempData["Err"] = "اسم المركز ورقم واتساب صحيح مطلوبان"; return RedirectToAction(nameof(Index)); }
        if (openHour < 0 || openHour > 23 || closeHour < 1 || closeHour > 24 || closeHour <= openHour) { TempData["Err"] = "ساعات العمل غير صحيحة"; return RedirectToAction(nameof(Index)); }
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) { TempData["Err"] = "الإحداثيات غير صحيحة"; return RedirectToAction(nameof(Index)); }

        var (logoUrl, e1) = await _img.SaveAsync(logo, c.Id);
        var (coverUrl, e2) = await _img.SaveAsync(cover, c.Id);
        if (e1 != null || e2 != null) { TempData["Err"] = e1 ?? e2; return RedirectToAction(nameof(Index)); }

        c.BusinessName = businessName.Trim(); c.Description = description; c.WhatsApp = wa; c.Phone = phone ?? c.Phone;
        c.City = city?.Trim() ?? ""; c.Address = address ?? ""; c.Latitude = latitude; c.Longitude = longitude;
        c.OpenHour = openHour; c.CloseHour = closeHour;
        if (logoUrl != null) { _img.Delete(c.LogoUrl, c.Id); c.LogoUrl = logoUrl; }
        if (coverUrl != null) { _img.Delete(c.CoverUrl, c.Id); c.CoverUrl = coverUrl; }
        await _db.SaveChangesAsync();
        TempData["Ok"] = "تم حفظ بيانات المركز";
        return RedirectToAction(nameof(Index));
    }
}
