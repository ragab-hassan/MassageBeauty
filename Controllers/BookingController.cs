using System.Data;
using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers;

[Authorize(Roles = "Customer")]
public class BookingController : Controller
{
    private readonly AppDb _db;
    private readonly UserManager<AppUser> _users;
    public BookingController(AppDb db, UserManager<AppUser> users) { _db = db; _users = users; }

    private const int MaxDaysAhead = 30;

    // يتحقق من الخدمة والمركز والعامل ويعيدها
    private async Task<(Service? svc, AppUser? emp)> Load(int serviceId, string employeeId)
    {
        var svc = await _db.Services.Include(s => s.Subscriber).FirstOrDefaultAsync(s => s.Id == serviceId);
        if (svc == null || !svc.IsLive) return (null, null);
        if (!await _db.Subscribers.PublicCenters().AnyAsync(s => s.Id == svc.SubscriberId)) return (null, null);
        var emp = await _db.Users.FirstOrDefaultAsync(u => u.Id == employeeId && u.SubscriberId == svc.SubscriberId
                    && u.IsActive && u.ShowOnPublic && u.Id != svc.Subscriber!.OwnerUserId);
        return (svc, emp);
    }

    private async Task<List<TimeSpan>> Slots(Subscriber c, Service s, string empId, DateTime date)
    {
        var cutoff = DateTime.UtcNow.AddHours(-3);   // الحجز المعلّق أكثر من 3 ساعات لا يحجز الموعد
        var busy = (await _db.Bookings.Where(b => b.EmployeeId == empId && b.Date == date.Date &&
                        (b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Pending && b.CreatedAt >= cutoff)))
                    .Select(b => new { b.StartTime, b.EndTime }).ToListAsync());

        var list = new List<TimeSpan>();
        var dur = TimeSpan.FromMinutes(s.DurationMinutes);
        var open = TimeSpan.FromHours(c.OpenHour);
        var close = TimeSpan.FromHours(c.CloseHour);
        var now = Ksa.Now;
        for (var t = open; t + dur <= close && t + dur < TimeSpan.FromHours(24); t += TimeSpan.FromMinutes(30))
        {
            if (date.Date == now.Date && t < now.TimeOfDay.Add(TimeSpan.FromMinutes(30))) continue;
            var end = t + dur;
            if (busy.Any(b => t < b.EndTime && end > b.StartTime)) continue;
            list.Add(t);
        }
        return list;
    }

    private static DateTime ClampDate(DateTime? d)
    {
        var today = Ksa.Today;
        var v = (d ?? today).Date;
        return v < today ? today : v > today.AddDays(MaxDaysAhead) ? today.AddDays(MaxDaysAhead) : v;
    }

    [HttpGet]
    public async Task<IActionResult> Schedule(int serviceId, string employeeId, DateTime? date)
    {
        var (svc, emp) = await Load(serviceId, employeeId);
        if (svc == null || emp == null) return NotFound();
        var d = ClampDate(date);
        ViewBag.Employee = emp;
        ViewBag.Date = d;
        ViewBag.MinDate = Ksa.Today;
        ViewBag.MaxDate = Ksa.Today.AddDays(MaxDaysAhead);
        ViewBag.Slots = await Slots(svc.Subscriber!, svc, emp.Id, d);
        return View(svc);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int serviceId, string employeeId, DateTime date, string time)
    {
        var (svc, emp) = await Load(serviceId, employeeId);
        if (svc == null || emp == null) return NotFound();
        var me = (await _users.GetUserAsync(User))!;
        var d = ClampDate(date);
        if (!TimeSpan.TryParse(time, out var t))
        { TempData["Err"] = "الوقت غير صحيح"; return RedirectToAction(nameof(Schedule), new { serviceId, employeeId, date = d }); }

        // حد أقصى للحجوزات المعلقة لمنع الإزعاج
        var cutoff = DateTime.UtcNow.AddHours(-3);
        if (await _db.Bookings.CountAsync(b => b.CustomerId == me.Id && b.Status == BookingStatus.Pending && b.CreatedAt >= cutoff) >= 5)
        { TempData["Err"] = "لديك عدة حجوزات بانتظار التأكيد، أكملها عبر واتساب أولًا"; return RedirectToAction(nameof(Mine)); }

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var center = await _db.Subscribers.FirstAsync(s => s.Id == svc.SubscriberId);
        var slots = await Slots(center, svc, emp.Id, d);
        if (!slots.Contains(t))
        { await tx.RollbackAsync(); TempData["Err"] = "هذا الموعد لم يعد متاحًا، اختر موعدًا آخر"; return RedirectToAction(nameof(Schedule), new { serviceId, employeeId, date = d }); }

        center.BookingCounter++;
        var b = new Booking
        {
            SubscriberId = center.Id, ServiceId = svc.Id, EmployeeId = emp.Id, CustomerId = me.Id,
            Code = $"B{center.Id}-{center.BookingCounter:D4}",
            Date = d, StartTime = t, EndTime = t + TimeSpan.FromMinutes(svc.DurationMinutes), Price = svc.Price
        };
        _db.Bookings.Add(b);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return RedirectToAction(nameof(Sent), new { id = b.Id });
    }

    private static string WhatsAppUrl(Booking b, Subscriber c, Service s, AppUser emp, AppUser cust)
    {
        var time = b.StartTime.ToString(@"hh\:mm");
        var msg = $"السلام عليكم، أرغب بتأكيد حجزي:\n" +
                  $"كود الحجز: {b.Code}\n" +
                  $"الخدمة: {s.Name}\n" +
                  $"الأخصائي: {emp.FullName}\n" +
                  $"التاريخ: {b.Date:yyyy-MM-dd}\n" +
                  $"الوقت: {time}\n" +
                  $"المدة: {s.DurationMinutes} دقيقة\n" +
                  $"السعر: {b.Price:0.##} ر.س\n" +
                  $"الاسم: {cust.FullName}\n" +
                  $"الجوال: {cust.PhoneNumber}";
        return $"https://wa.me/{Phones.Normalize(c.WhatsApp)}?text={Uri.EscapeDataString(msg)}";
    }

    public async Task<IActionResult> Sent(int id)
    {
        var me = (await _users.GetUserAsync(User))!;
        var b = await _db.Bookings.Include(x => x.Subscriber).Include(x => x.Service).Include(x => x.Employee)
                    .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == me.Id);
        if (b == null) return NotFound();
        ViewBag.Wa = WhatsAppUrl(b, b.Subscriber, b.Service, b.Employee, me);
        return View(b);
    }

    public async Task<IActionResult> Mine()
    {
        var me = (await _users.GetUserAsync(User))!;
        var list = await _db.Bookings.Include(x => x.Subscriber).Include(x => x.Service).Include(x => x.Employee)
                    .Where(x => x.CustomerId == me.Id).OrderByDescending(x => x.Date).ThenByDescending(x => x.StartTime).Take(50).ToListAsync();
        ViewBag.Me = me;
        ViewBag.Wa = list.ToDictionary(b => b.Id, b => WhatsAppUrl(b, b.Subscriber, b.Service, b.Employee, me));
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var me = (await _users.GetUserAsync(User))!;
        var b = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == me.Id);
        if (b == null) return NotFound();
        if (b.Status is BookingStatus.Pending or BookingStatus.Confirmed)
        {
            b.Status = BookingStatus.Cancelled;
            await _db.SaveChangesAsync();
            TempData["Ok"] = "تم إلغاء الحجز";
        }
        return RedirectToAction(nameof(Mine));
    }
}
