using System.Security.Cryptography;
using System.Text;
using MassageBeauty.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MassageBeauty.Controllers;

// أداة إعداد قاعدة البيانات بعد رفع الملفات. محمية بمفتاح سري من الإعدادات (Setup:Key).
// بعد الانتهاء اجعل Setup:Key فارغًا لتعطيلها نهائيًا.
public class SetupController : Controller
{
    private readonly AppDb _db;
    private readonly IConfiguration _cfg;
    private readonly IServiceProvider _sp;
    public SetupController(AppDb db, IConfiguration cfg, IServiceProvider sp) { _db = db; _cfg = cfg; _sp = sp; }

    private bool KeyOk(string? key)
    {
        var real = _cfg["Setup:Key"];
        if (string.IsNullOrWhiteSpace(real) || real.Length < 16 || string.IsNullOrEmpty(key)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(real));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private ContentResult Text(string s, int status = 200) =>
        new() { Content = s, ContentType = "text/plain; charset=utf-8", StatusCode = status };

    // 1) اختبار الاتصال وعرض حالة الجداول
    [HttpGet]
    public async Task<IActionResult> Status(string? key)
    {
        if (!KeyOk(key)) return NotFound();
        try
        {
            var can = await _db.Database.CanConnectAsync();
            if (!can) return Text("فشل الاتصال بالقاعدة: راجع ConnectionStrings:Default");
            var pending = _db.Database.GetMigrations().Any() ? string.Join(", ", await _db.Database.GetPendingMigrationsAsync()) : "(لا توجد Migrations - إنشاء مباشر)";
            var owners = await _db.Users.CountAsync();
            return Text($"الاتصال ناجح\nالمستخدمون: {owners}\nMigrations معلّقة: {pending}");
        }
        catch (Exception ex) { return Text("الجداول غير موجودة أو الاتصال فاشل:\n" + ex.GetBaseException().Message); }
    }

    // 2) إنشاء الجداول + حساب المالك + الباقات الافتراضية
    [HttpGet]
    public async Task<IActionResult> Init(string? key)
    {
        if (!KeyOk(key)) return NotFound();
        try
        {
            await Seeder.RunAsync(_sp, _cfg);
            return Text("تم إنشاء الجداول وتهيئة البيانات الأولية بنجاح. يمكنك الآن الدخول بحساب المالك.\nلا تنسَ تفريغ Setup:Key بعد الانتهاء.");
        }
        catch (Exception ex) { return Text("فشل الإنشاء:\n" + ex.GetBaseException().Message, 500); }
    }

    // 3) سكربت SQL كامل لتنفيذه يدويًا في لوحة قاعدة البيانات (بديل لو منع الاستضافة الإنشاء التلقائي)
    [HttpGet]
    public IActionResult Script(string? key)
    {
        if (!KeyOk(key)) return NotFound();
        return Text(_db.Database.GenerateCreateScript());
    }
}
