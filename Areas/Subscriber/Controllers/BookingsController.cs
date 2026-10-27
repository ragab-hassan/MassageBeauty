using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers.SubscriberArea;

[Area("Subscriber"), Authorize(Roles = "Subscriber")]
public class BookingsController : Controller
{
    private readonly AppDb _db;
    private readonly UserManager<AppUser> _users;
    public BookingsController(AppDb db, UserManager<AppUser> users) { _db = db; _users = users; }

    private async Task<int> Sid() => (await _users.GetUserAsync(User))!.SubscriberId!.Value;

    public async Task<IActionResult> Index(string? filter)
    {
        var sid = await Sid();
        var today = Ksa.Today;
        var q = _db.Bookings.Include(b => b.Service).Include(b => b.Employee).Include(b => b.Customer).Where(b => b.SubscriberId == sid);
        q = filter switch
        {
            "pending" => q.Where(b => b.Status == BookingStatus.Pending),
            "past" => q.Where(b => b.Date < today || b.Status == BookingStatus.Completed || b.Status == BookingStatus.Cancelled),
            _ => q.Where(b => b.Date >= today && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
        };
        ViewBag.Filter = filter ?? "upcoming";
        var list = filter == "past"
            ? await q.OrderByDescending(b => b.Date).ThenByDescending(b => b.StartTime).Take(100).ToListAsync()
            : await q.OrderBy(b => b.Date).ThenBy(b => b.StartTime).Take(200).ToListAsync();
        return View(list);
    }

    private async Task<IActionResult> SetStatus(int id, BookingStatus st, string msg)
    {
        var sid = await Sid();
        var b = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.SubscriberId == sid);
        if (b == null) return NotFound();
        b.Status = st;
        await _db.SaveChangesAsync();
        TempData["Ok"] = msg;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Confirm(int id) => SetStatus(id, BookingStatus.Confirmed, "تم تأكيد الحجز");
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Complete(int id) => SetStatus(id, BookingStatus.Completed, "تم إنهاء الحجز");
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Cancel(int id) => SetStatus(id, BookingStatus.Cancelled, "تم إلغاء الحجز");
}
