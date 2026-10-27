using MassageBeauty.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Data;

public class AppDb : IdentityDbContext<AppUser>
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Plan>().Property(x => x.Price).HasPrecision(10, 2);
        b.Entity<Plan>().Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.Entity<Subscription>().Property(x => x.PricePaid).HasPrecision(10, 2);
        b.Entity<Subscription>().HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Subscription>().HasOne(x => x.Subscriber).WithMany(s => s.Subscriptions).HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Service>().Property(x => x.Price).HasPrecision(10, 2);
        b.Entity<Service>().Property(x => x.OldPrice).HasPrecision(10, 2);
        b.Entity<Service>().HasOne(x => x.Subscriber).WithMany(s => s.Services).HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Booking>().Property(x => x.Price).HasPrecision(10, 2);
        b.Entity<Booking>().HasOne(x => x.Subscriber).WithMany().HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Booking>().HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Booking>().HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Booking>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Booking>().HasIndex(x => new { x.SubscriberId, x.Code }).IsUnique();
        b.Entity<Booking>().HasIndex(x => new { x.EmployeeId, x.Date });
        b.Entity<AppUser>().HasOne(u => u.Subscriber).WithMany(s => s.Users).HasForeignKey(u => u.SubscriberId).OnDelete(DeleteBehavior.Restrict);
    }
}

public static class Seeder
{
    public static async Task RunAsync(IServiceProvider sp, IConfiguration cfg)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // لو وُجدت ملفات Migrations تُطبَّق، وإلا تُنشأ الجداول مباشرة (قاعدة فارغة)
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var r in new[] { "Owner", "Subscriber", "Employee", "Customer" })
            if (!await rm.RoleExistsAsync(r)) await rm.CreateAsync(new IdentityRole(r));

        var uname = cfg["Owner:UserName"] ?? "owner";
        if (await um.FindByNameAsync(uname) == null)
        {
            var u = new AppUser { UserName = uname, FullName = cfg["Owner:FullName"] ?? "المالك", EmailConfirmed = true };
            var res = await um.CreateAsync(u, cfg["Owner:Password"] ?? "ChangeMe@123");
            if (res.Succeeded) await um.AddToRoleAsync(u, "Owner");
        }

        if (!await db.Plans.AnyAsync())
        {
            db.Plans.AddRange(
                new Plan { Name = "تجربة مجانية", DurationDays = 10, Price = 0, MaxEmployees = 3, Features = "تجربة كاملة لمدة 10 أيام" },
                new Plan { Name = "الباقة الشهرية", DurationDays = 30, Price = 150, MaxEmployees = 10, Features = "جميع الميزات" },
                new Plan { Name = "الباقة السنوية", DurationDays = 365, Price = 1000, MaxEmployees = 30, Features = "جميع الميزات + أولوية في الدعم" });
            await db.SaveChangesAsync();
        }
    }
}
