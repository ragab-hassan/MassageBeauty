using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using MassageBeauty.Data;
using MassageBeauty.Models;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Services;

public static class Ksa
{
    private static readonly TimeZoneInfo Tz = Find();
    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Arab Standard Time", "Asia/Riyadh" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("KSA", TimeSpan.FromHours(3), "KSA", "KSA");
    }
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);
    public static DateTime Today => Now.Date;
}

public static class Phones
{
    // يحوّل 05xxxxxxxx أو 5xxxxxxxx أو 00966... إلى 9665xxxxxxxx (صيغة واتساب)
    public static string Normalize(string? p)
    {
        var d = string.Concat((p ?? "").Where(char.IsDigit).Select(c => ((int)char.GetNumericValue(c)).ToString()));
        if (d.StartsWith("00")) d = d[2..];
        if (d.StartsWith("05") && d.Length == 10) d = "966" + d[1..];
        else if (d.StartsWith("5") && d.Length == 9) d = "966" + d;
        return d;
    }
}

public static class Geo
{
    public static double Km(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        double dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public static class Queries
{
    // المراكز الظاهرة للعملاء: حساب مفعّل + اشتراك ساري + رقم واتساب
    public static IQueryable<Subscriber> PublicCenters(this IQueryable<Subscriber> q)
    {
        var today = Ksa.Today;
        return q.Where(s => s.IsActive && s.WhatsApp != "" && s.Subscriptions.Any(x => x.EndDate >= today));
    }
}

public static class HtmlExt
{
    public static IHtmlContent Thumb(this IHtmlHelper h, string? url, string cls = "thumb")
    {
        var div = new TagBuilder("div");
        div.AddCssClass(cls);
        if (!string.IsNullOrEmpty(url))
        {
            var img = new TagBuilder("img");
            img.Attributes["src"] = url; img.Attributes["alt"] = ""; img.Attributes["loading"] = "lazy";
            div.InnerHtml.AppendHtml(img);
        }
        else
        {
            var i = new TagBuilder("i");
            i.AddCssClass("bi bi-flower2");
            div.InnerHtml.AppendHtml(i);
        }
        return div;
    }
}

public class ImageStore
{
    private readonly string _root;
    public ImageStore(IWebHostEnvironment env) => _root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public async Task<(string? Url, string? Error)> SaveAsync(IFormFile? file, int subscriberId)
    {
        if (file == null || file.Length == 0) return (null, null);
        if (file.Length > 3 * 1024 * 1024) return (null, "حجم الصورة أكبر من 3 ميجابايت");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) return (null, "الصيغ المسموحة: JPG أو PNG أو WEBP");

        await using var s = file.OpenReadStream();
        var head = new byte[12];
        var n = await s.ReadAsync(head, 0, 12);
        bool jpg = n >= 3 && head[0] == 0xFF && head[1] == 0xD8;
        bool png = n >= 4 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47;
        bool webp = n >= 12 && head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P';
        if (!(jpg || png || webp)) return (null, "الملف ليس صورة صالحة");

        var dir = Path.Combine(_root, "uploads", subscriberId.ToString());
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + ext;
        s.Seek(0, SeekOrigin.Begin);
        await using (var fs = File.Create(Path.Combine(dir, name))) await s.CopyToAsync(fs);
        return ($"/uploads/{subscriberId}/{name}", null);
    }

    public void Delete(string? url, int subscriberId)
    {
        if (string.IsNullOrEmpty(url) || url.Contains("..") || !url.StartsWith($"/uploads/{subscriberId}/")) return;
        var p = Path.Combine(_root, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        try { if (File.Exists(p)) File.Delete(p); } catch { }
    }
}
