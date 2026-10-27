using MassageBeauty.Data;
using MassageBeauty.Models;
using MassageBeauty.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers.EmployeeArea;

[Area("Employee"), Authorize(Roles = "Employee")]
public class HomeController : Controller
{
    private readonly AppDb _db;
    private readonly UserManager<AppUser> _users;
    public HomeController(AppDb db, UserManager<AppUser> users) { _db = db; _users = users; }

    public async Task<IActionResult> Index()
    {
        var me = (await _users.GetUserAsync(User))!;
        var today = Ksa.Today;
        var list = await _db.Bookings.Include(b => b.Service).Include(b => b.Customer)
            .Where(b => b.EmployeeId == me.Id && b.Date >= today && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
            .OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToListAsync();
        return View(list);
    }
}
