using MassageBeauty.Data;
using MassageBeauty.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers.OwnerArea;

[Area("Owner"), Authorize(Roles = "Owner")]
public class PlansController : Controller
{
    private readonly AppDb _db;
    public PlansController(AppDb db) => _db = db;

    public async Task<IActionResult> Index() => View(await _db.Plans.OrderBy(p => p.DurationDays).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return View(new Plan());
        var p = await _db.Plans.FindAsync(id);
        return p == null ? NotFound() : View(p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Plan model)
    {
        if (model.OfferStart != null && model.OfferEnd != null && model.OfferEnd < model.OfferStart)
            ModelState.AddModelError(nameof(model.OfferEnd), "نهاية العرض قبل بدايته");
        if (!ModelState.IsValid) return View(model);

        if (model.Id == 0) _db.Plans.Add(model);
        else
        {
            var p = await _db.Plans.FindAsync(model.Id);
            if (p == null) return NotFound();
            p.Name = model.Name; p.DurationDays = model.DurationDays; p.Price = model.Price;
            p.DiscountPercent = model.DiscountPercent; p.OfferStart = model.OfferStart; p.OfferEnd = model.OfferEnd;
            p.MaxEmployees = model.MaxEmployees; p.Features = model.Features; p.IsActive = model.IsActive;
        }
        await _db.SaveChangesAsync();
        TempData["Ok"] = "تم حفظ الباقة";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var p = await _db.Plans.FindAsync(id);
        if (p == null) return NotFound();
        p.IsActive = !p.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Plans.FindAsync(id);
        if (p == null) return NotFound();
        if (await _db.Subscriptions.AnyAsync(s => s.PlanId == id))
        {
            TempData["Err"] = "لا يمكن حذف باقة مستخدمة في اشتراكات، أوقفها بدلًا من ذلك";
            return RedirectToAction(nameof(Index));
        }
        _db.Plans.Remove(p);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "تم حذف الباقة";
        return RedirectToAction(nameof(Index));
    }
}
