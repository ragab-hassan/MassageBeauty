using System.ComponentModel.DataAnnotations;
using MassageBeauty.Services;

namespace MassageBeauty.Models;

public enum ServiceKind { Service = 0, Package = 1, Offer = 2 }
public enum BookingStatus { Pending = 0, Confirmed = 1, Completed = 2, Cancelled = 3 }

public class Service
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber? Subscriber { get; set; }
    [Required(ErrorMessage = "الاسم مطلوب")] public string Name { get; set; } = "";
    public ServiceKind Kind { get; set; } = ServiceKind.Service;
    public string? ImageUrl { get; set; }
    [Range(10, 600, ErrorMessage = "المدة بالدقائق (10 - 600)")] public int DurationMinutes { get; set; } = 60;
    [Range(0, 100000)] public decimal Price { get; set; }
    [Range(0, 100000)] public decimal? OldPrice { get; set; }
    public string? Benefits { get; set; }       // فوائد مختصرة للجسم
    public string? Description { get; set; }
    public DateTime? OfferEnd { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public bool IsLive => IsActive && (Kind != ServiceKind.Offer || OfferEnd == null || OfferEnd.Value.Date >= Ksa.Today);
    public string KindLabel => Kind switch { ServiceKind.Package => "باقة", ServiceKind.Offer => "عرض", _ => "خدمة" };
}

public class Booking
{
    public int Id { get; set; }
    public int SubscriberId { get; set; }
    public Subscriber Subscriber { get; set; } = null!;
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public string EmployeeId { get; set; } = "";
    public AppUser Employee { get; set; } = null!;
    public string CustomerId { get; set; } = "";
    public AppUser Customer { get; set; } = null!;
    public string Code { get; set; } = "";               // فريد لكل مركز
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal Price { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string StatusLabel => Status switch
    {
        BookingStatus.Pending => "بانتظار التأكيد عبر واتساب",
        BookingStatus.Confirmed => "مؤكد",
        BookingStatus.Completed => "منتهي",
        _ => "ملغي"
    };
}

public class CenterCard { public Subscriber S { get; set; } = null!; public double? Km { get; set; } }
public class ServiceCard { public Service Svc { get; set; } = null!; public Subscriber Center { get; set; } = null!; public double? Km { get; set; } }
public class LandingVm
{
    public List<CenterCard> Centers { get; set; } = new();
    public List<ServiceCard> Services { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public string? City { get; set; }
    public string? Q { get; set; }
    public int? Kind { get; set; }
    public bool HasLocation { get; set; }
}
