using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers.SubscriberArea;

[Area("Subscriber"), Authorize(Roles = "Subscriber")]
public class ServicesController : Controller
{
    private readonly AppDb _db;
    private readonly UserManager<AppUser> _users;
    private readonly ImageStore _img;
    public ServicesController(AppDb db, UserManager<AppUser> users, ImageStore img) { _db = db; _users = users; _img = img; }

    private async Task<int> Sid() => (await _users.GetUserAsync(User))!.SubscriberId!.Value;

    public async Task<IActionResult> Index()
    {
        var sid = await Sid();
        return View(await _db.Services.Where(s => s.SubscriberId == sid).OrderBy(s => s.Kind).ThenBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return View(new Service());
        var sid = await Sid();
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.SubscriberId == sid);
        return s == null ? NotFound() : View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Service model, IFormFile? image, bool removeImage)
    {
        var sid = await Sid();
        ModelState.Remove(nameof(Service.Subscriber));
        if (!ModelState.IsValid) return View(model);

        var (url, err) = await _img.SaveAsync(image, sid);
        if (err != null) { ModelState.AddModelError("", err); return View(model); }

        Service s;
        if (model.Id == 0) { s = new Service { SubscriberId = sid }; _db.Services.Add(s); }
        else
        {
            s = (await _db.Services.FirstOrDefaultAsync(x => x.Id == model.Id && x.SubscriberId == sid))!;
            if (s == null) return NotFound();
        }
        s.Name = model.Name; s.Kind = model.Kind; s.DurationMinutes = model.DurationMinutes; s.Price = model.Price;
        s.OldPrice = model.OldPrice; s.Benefits = model.Benefits; s.Description = model.Description;
        s.OfferEnd = model.OfferEnd; s.IsActive = model.IsActive; s.SortOrder = model.SortOrder;
        if (url != null) { _img.Delete(s.ImageUrl, sid); s.ImageUrl = url; }
        else if (removeImage) { _img.Delete(s.ImageUrl, sid); s.ImageUrl = null; }
        await _db.SaveChangesAsync();
        TempData["Ok"] = "تم حفظ الخدمة";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var sid = await Sid();
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.SubscriberId == sid);
        if (s == null) return NotFound();
        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var sid = await Sid();
        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.SubscriberId == sid);
        if (s == null) return NotFound();
        if (await _db.Bookings.AnyAsync(b => b.ServiceId == id))
        { TempData["Err"] = "الخدمة لها حجوزات سابقة، أوقفها بدلًا من الحذف"; return RedirectToAction(nameof(Index)); }
        _img.Delete(s.ImageUrl, sid);
        _db.Services.Remove(s);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "تم الحذف";
        return RedirectToAction(nameof(Index));
    }
}
