# ⚡ Momentum — نسخهٔ ASP.NET Core

برنامه‌ریز مطالعهٔ لوکال برای کنکور، بازنویسی کامل [نسخهٔ اصلی Node.js](https://github.com/ILIA8400/Momentum) با **ASP.NET Core MVC** و **SQL Server**.

| | |
|---|---|
| فریم‌ورک | ASP.NET Core MVC روی **.NET 10** |
| دیتابیس | **SQL Server** (Express / Developer / LocalDB) با **EF Core** |
| تقویم | **شمسی** (پورت دقیق الگوریتم jalaali-js) |
| مرور هوشمند | الگوریتم **FSRS-6** (پورت کامل کتابخانهٔ ts-fsrs 5.4.2) |
| زبان رابط کاربری | فارسی، RTL کامل |
| اکسل | ClosedXML (قالب + ورود دسته‌ای) |

---

## ✨ امکانات (همان نسخهٔ اصلی)

- **داشبورد**: روزشمار کنکور با نوار پیشرفت، جملهٔ انگیزشی روز، سلام بر اساس ساعت، خلاصهٔ امروز (ساعت/تست/درصد با مقایسهٔ دیروز)، تایم‌لاین امروز، لیست انجام‌شده/مانده، یادداشت روز با ذخیرهٔ خودکار، مقایسه با میانگین ۷ روز و رکوردها
- **نقشهٔ مطالعه** (مثل GitHub): ۶ ماه یا ۱ سال اخیر، سطح‌بندی بر اساس چارک دادهٔ واقعی
- **ثبت مطالعه**: درس، بازهٔ ساعت شروع/پایان (با عبور از نیمه‌شب)، تفکیک خواندن/مرور/تست، تعداد تست و درست/غلط، **درصد کنکوری با نمرهٔ منفی**: (۳×درست − غلط) ÷ (۳×کل)
- **نماهای روزانه/هفتگی/ماهانه** با هدف‌گذاری (کلی و به تفکیک درس)
- **مرور FSRS**: زمان‌بندی بهینهٔ مرور مباحث با امتیاز «دوباره/سخت/خوب/خیلی راحت» + پیش‌نمایش موعد بعدی
- **آمار**: بازه‌های آماده و دلخواه، بهترین رکوردها (روز/هفته/زنجیره)، تفکیک درس، نمودار روند روزانه
- **All Plan**: جدول بزرگ هفتگی برای چاپ (A3 افقی) + **گزارش تفصیلی هفته** (A4) + **جدول خلاصه** (A4 افقی)
- **ورود از اکسل**: قالب با اعتبارسنجی و شیت راهنما، تحلیل ردیف‌ها با تشخیص تاریخ شمسی/میلادی، ارقام فارسی، مدت به شکل «3:00» یا «۳ ساعت»، تشخیص تکراری‌ها و پیش‌نمایش قبل از ثبت
- **بکاپ**: JSON کامل (سازگار با نسخهٔ Node.js!)، خروجی CSV، اسنپ‌شات خودکار هر ۶ ساعت (۳۰ فایل آخر)، بازیابی از JSON
- **تم روشن/تاریک/خودکار**

> تفاوت با نسخهٔ Node: اسکریپت‌های `install.bat/start.bat/update.bat` و به‌روزرسانی خودکار حذف شده‌اند (اجرای برنامه با Visual Studio یا `dotnet run` انجام می‌شود) و فایل دیتابیس SQLite با SQL Server جایگزین شده است.

---

## 🚀 راه‌اندازی

### ۱) پیش‌نیازها

1. **.NET 10 SDK** → [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
2. **SQL Server** — یکی از این سه:
   - **SQL Server Express** (رایگان) → [مقایسهٔ نسخه‌ها و دانلود](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
   - **SQL Server Developer** (رایگان، کامل‌ترین)
   - **LocalDB** (اگر Visual Studio نصب دارید، معمولاً از قبل موجود است)

### ۲) تنظیم ConnectionString

فایل `Momentum.Web/appsettings.json` را باز کن و مطابق نوع SQL Server خودت تنظیم کن:

| نوع SQL Server | ConnectionString |
|---|---|
| **SQL Server Express** (پیش‌فرض) | `Server=.\SQLEXPRESS;Database=Momentum;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true` |
| **LocalDB** (همراه Visual Studio) | `Server=(localdb)\MSSQLLocalDB;Database=Momentum;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true` |
| **نمونهٔ پیش‌فرض (Developer/Standard)** | `Server=.;Database=Momentum;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true` |

> ⚠️ هنگام اجرا با **F5 در Visual Studio** (حالت Development) مقدار داخل `appsettings.Development.json` اعمال می‌شود — آن را هم مثل بالا تنظیم کن. پیش‌فرض این فایل روی LocalDB است.
>
> اگر SQL Server با احراز هویت SQL اجرا می‌کنی: `Server=.;Database=Momentum;User Id=sa;Password=***;TrustServerCertificate=True`

### ۳) اجرا

**با Visual Studio:**
1. `Momentum.sln` را باز کن.
2. صبر کن NuGet پکیج‌ها را بازیابی کند (اولین بار اینترنت لازم است).
3. `Ctrl+F5` (اجرای بدون دیباگ) — مرورگر خودش باز می‌شود.

**با خط فرمان:**
```bash
cd Momentum.Web
dotnet run
```
سپس آدرس نمایش‌داده‌شده در کنسول را در مرورگر باز کن (معمولاً `http://localhost:5000`).

> 💡 دیتابیس و همهٔ جدول‌ها **خودکار** در اولین اجرا ساخته می‌شوند و ۱۲ درس پیش‌فرض + تنظیمات اولیه ثبت می‌شود — نیازی به اسکریپت SQL نیست.

---

## 📂 ساختار پروژه

```
Momentum.sln
└── Momentum.Web/
    ├── Program.cs                 ← نقطهٔ شروع، DI، ساخت خودکار دیتابیس
    ├── appsettings.json           ← ConnectionString
    ├── Models/Models.cs           ← Subject, Entry, Goal, DayNote, Setting, ReviewItem, ReviewLog
    ├── Data/AppDbContext.cs       ← نگاشت EF Core + مقداردهی اولیه
    ├── Services/
    │   ├── Jalali.cs              ← تقویم شمسی (پورت jalaali-js)
    │   ├── Fsrs.cs                ← الگوریتم FSRS-6 (پورت ts-fsrs 5.4.2)
    │   ├── StatsService.cs        ← آمار، رکوردها + SettingsService
    │   ├── BackupService.cs       ← بکاپ JSON/CSV + بکاپ خودکار دوره‌ای
    │   └── ExcelImportService.cs  ← قالب اکسل + تحلیل ورود دسته‌ای
    ├── Helpers/
    │   ├── Fa.cs                  ← ارقام فارسی، قالب‌بندی‌ها، آیکون‌های SVG
    │   └── Charts.cs              ← نمودار روند SVG + نقشهٔ مطالعه
    ├── ViewModels/ViewModels.cs   ← مدل‌های اختصاصی صفحات
    ├── Controllers/               ← Home, Day, Week, Month, Review, Stats,
    │                                 Import, Settings, Goals, AllPlan, Report, Summary, Guide
    ├── Views/                     ← Razor Views (RTL فارسی)
    └── wwwroot/                   ← site.css (طراحی نسخهٔ اصلی), site.js, jalali.js
```

## 🗄️ نکات دیتابیس

- **تاریخ‌ها** به شکل رشتهٔ شمسی `YYYY-MM-DD` ذخیره می‌شوند (مثل نسخهٔ اصلی) تا رفتار مرتب‌سازی و بکاپ یکسان بماند.
- **کارت FSRS** به شکل JSON سازگار با ts-fsrs در ستون `FsrsCard` نگهداری می‌شود.
- ساخت دیتابیس با `EnsureCreated` انجام می‌شود (بدون فایل Migration). اگر بعداً اسکیمای مدل‌ها را تغییر دادی، یا دیتابیس را Drop کن و دوباره اجرا کن، یا EF Migrations را اضافه کن:
  ```bash
  dotnet tool install --global dotnet-ef
  dotnet ef migrations add Init --project Momentum.Web
  ```
- **بکاپ SQL Server**: علاوه بر بکاپ JSON داخلی برنامه، می‌توانی هر زمان از SQL Server Management Studio روی دیتابیس `Momentum` بکاپ کامل `.bak` بگیری.

## 🔁 سازگاری با نسخهٔ Node.js

- فایل **بکاپ JSON** (شامل همهٔ داده‌ها و کارت‌های FSRS) بین دو نسخه **دو طرفه** قابل استفاده است: از تنظیمات نسخهٔ Node خروجی بگیر و در «بازیابی از JSON» همین نسخه برگردان (و برعکس).
- قالب **ورود اکسل** همان ستون‌ها و شیت «راهنما» را دارد؛ فایل‌های پرشده در نسخهٔ Node را بدون تغییر می‌توانی همین‌جا import کنی.

## 🧪 تست الگوریتم‌ها

منطق تقویم شمسی و FSRS به‌صورت پورت **خط‌به‌خط** از نسخه‌های اصلی (jalaali-js و ts-fsrs 5.4.2 با پارامترهای پیش‌فرض) نوشته شده است؛ شامل:

- تبدیل جلالی↔میلادی با آرایهٔ breakpoints یکسان
- `weekday`: شنبه = ۰ … جمعه = ۶
- FSRS-6 با ۲۱ پارامتر پیش‌فرض، retention ‏۰٫۹، max interval ‏۳۶۵۰۰، short-term فعال، fuzz غیرفعال، مراحل یادگیری ۱/۱۰ دقیقه

## 📄 مجوز

مطابق پروژهٔ اصلی: **MIT**
