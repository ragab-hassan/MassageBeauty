using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers;

public class CenterController : Controller
{
    private readonly AppDb _db;
    public CenterController(AppDb db) => _db = db;

    public async Task<IActionResult> Index(int id)
    {
        var c = await _db.Subscribers.PublicCenters()
            .Include(s => s.Services.Where(x => x.IsActive)).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        return c == null ? NotFound() : View(c);
    }

    // اختيار العامل بعد اختيار الخدمة (متاح بدون تسجيل دخول)
    public async Task<IActionResult> Employees(int serviceId)
    {
        var svc = await _db.Services.Include(s => s.Subscriber).AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId);
        if (svc == null || !svc.IsLive) return NotFound();
        var ok = await _db.Subscribers.PublicCenters().AnyAsync(s => s.Id == svc.SubscriberId);
        if (!ok) return NotFound();

        ViewBag.Employees = await _db.Users.AsNoTracking()
            .Where(u => u.SubscriberId == svc.SubscriberId && u.IsActive && u.ShowOnPublic && u.Id != svc.Subscriber!.OwnerUserId)
            .OrderBy(u => u.FullName).ToListAsync();
        return View(svc);
    }
}
