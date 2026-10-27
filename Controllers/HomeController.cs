using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers;

public class HomeController : Controller
{
    private readonly AppDb _db;
    public HomeController(AppDb db) => _db = db;

    // الصفحة العامة للعميل
    public async Task<IActionResult> Index(string? city, double? lat, double? lng, string? q, int? kind)
    {
        var all = await _db.Subscribers.PublicCenters()
            .Include(s => s.Services.Where(x => x.IsActive))
            .AsNoTracking().ToListAsync();

        var vm = new LandingVm { City = city, Q = q, Kind = kind, HasLocation = lat != null && lng != null };
        vm.Cities = all.Select(s => s.City).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();

        double? Dist(Subscriber s) => lat != null && lng != null && s.Latitude != null && s.Longitude != null
            ? Math.Round(Geo.Km(lat.Value, lng.Value, s.Latitude.Value, s.Longitude.Value), 1) : null;

        var centers = all.Select(s => new CenterCard { S = s, Km = Dist(s) });
        if (!string.IsNullOrWhiteSpace(city)) centers = centers.Where(c => c.S.City == city);
        if (!string.IsNullOrWhiteSpace(q))
            centers = centers.Where(c => c.S.BusinessName.Contains(q, StringComparison.OrdinalIgnoreCase)
                                      || c.S.Services.Any(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)));
        var list = centers.OrderBy(c => c.Km ?? double.MaxValue).ThenBy(c => c.S.BusinessName).ToList();
        vm.Centers = list;

        var cards = list.SelectMany(c => c.S.Services.Where(x => x.IsLive).Select(x => new ServiceCard { Svc = x, Center = c.S, Km = c.Km }));
        if (kind != null) cards = cards.Where(x => (int)x.Svc.Kind == kind);
        if (!string.IsNullOrWhiteSpace(q)) cards = cards.Where(x => x.Svc.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Center.BusinessName.Contains(q, StringComparison.OrdinalIgnoreCase));
        vm.Services = cards.OrderBy(x => x.Km ?? double.MaxValue)
                           .ThenByDescending(x => x.Svc.Kind == ServiceKind.Offer)
                           .ThenBy(x => x.Svc.SortOrder).Take(60).ToList();
        return View(vm);
    }

    // تحويل المستخدم بعد الدخول حسب دوره
    public IActionResult Go()
    {
        if (User.IsInRole("Owner")) return RedirectToAction("Index", "Dashboard", new { area = "Owner" });
        if (User.IsInRole("Subscriber")) return RedirectToAction("Index", "Bookings", new { area = "Subscriber" });
        if (User.IsInRole("Employee")) return RedirectToAction("Index", "Home", new { area = "Employee" });
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Error() => Content("حدث خطأ غير متوقع");
}
