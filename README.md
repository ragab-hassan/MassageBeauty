# منصات المساج والجمال

## تشغيل محلي
1. Visual Studio 2022+ و .NET 8.
2. Package Manager Console:  Add-Migration Init   (أو احذف القاعدة المحلية Drop-Database ثم شغّل مباشرة)
3. F5 ← الدخول: owner / ChangeMe@123

## النشر على MonsterASP
1. من لوحة MonsterASP أنشئ قاعدة SQL Server فارغة وانسخ بيانات الاتصال.
2. عدّل appsettings.Production.json (Connection String وكلمة مرور المالك).
3. اختر إصدار .NET 8 للموقع من اللوحة.
4. Visual Studio ← Publish ← Web Deploy (ملف .publish من اللوحة) بإعداد Release.
5. عند أول تشغيل تُنشأ الجداول تلقائيًا (EnsureCreated أو Migrate لو وُجدت Migrations) ويُضاف حساب المالك والباقات.
6. لو ظهر خطأ 500.30 افتح /startup-error لقراءة السبب، ثم اجعل Diagnostics:ShowStartupError = false.

## ملاحظات
- EnsureCreated لا يعدّل جداول موجودة. للتحديثات المستقبلية استخدم Migrations من البداية: Add-Migration Init قبل أول نشر.
- مجلد wwwroot/uploads يجب أن يكون قابلًا للكتابة (صور المراكز والعاملين).
- غيّر كلمة مرور المالك فور أول دخول.

## الرحلات
- العميل: / ← مركز ← خدمة ← أخصائي ← دخول/تسجيل ← يوم ووقت ← واتساب بكود B{مركز}-{تسلسل}.
- المشترك: الحجوزات / الخدمات / العاملون / بيانات المركز.
- المالك: لوحة المشتركين / مشترك جديد / تصميم عروض وباقات.

## إنشاء الجداول بعد رفع الملفات (Setup)
1. في appsettings.Production.json ضع في Setup:Key مفتاحًا سريًا طويلًا (16 حرفًا فأكثر) وعدّل Connection String.
2. افتح: /Setup/Status?key=المفتاح  لاختبار الاتصال.
3. افتح: /Setup/Init?key=المفتاح  لإنشاء الجداول وحساب المالك والباقات.
4. بديل يدوي: /Setup/Script?key=المفتاح يعطيك سكربت SQL كاملًا تنفّذه في لوحة قاعدة البيانات.
5. بعد النجاح اجعل Setup:Key فارغًا لتعطيل الأداة.
