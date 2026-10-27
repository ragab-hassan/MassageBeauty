using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MassageBeauty.Models;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public int? SubscriberId { get; set; }
    public Subscriber? Subscriber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Title { get; set; }            // التخصص
    public string? PhotoUrl { get; set; }
    public bool ShowOnPublic { get; set; } = true; // يظهر للعملاء
}

public class Subscriber
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم النشاط مطلوب")] public string BusinessName { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string? Notes { get; set; }
    public string WhatsApp { get; set; } = "";
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int OpenHour { get; set; } = 10;
    public int CloseHour { get; set; } = 22;
    public int BookingCounter { get; set; }
    public string OwnerUserId { get; set; } = "";          // حساب دخول المشترك
    public bool IsActive { get; set; } = true;               // إيقاف الحساب بالكامل
    public bool AllowRenewal { get; set; } = true;           // تحكم المالك في التجديد
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Service> Services { get; set; } = new List<Service>();

    // خصائص محسوبة (غير مخزنة)
    public Subscription? Current => Subscriptions.OrderByDescending(s => s.EndDate).FirstOrDefault();
    public int EmployeesCount => Users.Count(u => u.Id != OwnerUserId);
    public string State => Current == null ? "none" : Current.DaysLeft < 0 ? "expired" : Current.DaysLeft <= 7 ? "soon" : "active";
}

public class Plan
{
    public int Id { get; set; }
    [Required(ErrorMessage = "اسم الباقة مطلوب")] public string Name { get; set; } = "";
    [Range(1, 3650, ErrorMessage = "المدة بالأيام")] public int DurationDays { get; set; } = 30;
    [Range(0, 1000000)] public decimal Price { get; set; }
    [Range(0, 100)] public decimal? DiscountPercent { get; set; }
    public DateTime? OfferStart { get; set; }
    public DateTime? OfferEnd { get; set; }
    public int? MaxEmployees { get; set; }
    public string? Features { get; set; }
    public bool IsActive { get; set; } = true;

    public bool OfferRunning => DiscountPercent is > 0
        && (OfferStart == null || OfferStart.Value.Date <= DateTime.Today)
        && (OfferEnd == null || OfferEnd.Value.Date >= DateTime.Today);
    public decimal EffectivePrice => OfferRunning ? Math.Round(Price * (1 - DiscountPercent!.Value / 100m), 2) : Price;
}

public class Subscription
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber Subscriber { get; set; } = null!;
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal PricePaid { get; set; }
    public string? Notes { get; set; }

    public int PeriodDays => (EndDate.Date - StartDate.Date).Days;
    public int DaysLeft => (EndDate.Date - DateTime.Today).Days;
}

public class CreateSubscriberVm
{
    [Required(ErrorMessage = "مطلوب")] public string BusinessName { get; set; } = "";
    [Required(ErrorMessage = "مطلوب")] public string OwnerName { get; set; } = "";
    [Required(ErrorMessage = "مطلوب")] public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string? Notes { get; set; }
    [Required(ErrorMessage = "مطلوب")] public string UserName { get; set; } = "";
    [Required(ErrorMessage = "مطلوب")] public string Password { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "اختر باقة")] public int PlanId { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public decimal PricePaid { get; set; }
}
