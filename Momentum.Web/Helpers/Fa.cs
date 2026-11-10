using System.Globalization;
using System.Text;
using System.Web;

namespace Momentum.Web.Helpers;

/// <summary>
/// ابزارهای نمایش فارسی: ارقام فارسی، قالب‌بندی مدت‌ها، درصدها، تاریخ‌ها،
/// جمله‌های انگیزشی و سلام بر اساس ساعت — پورت مستقیم از app.js نسخهٔ اصلی.
/// </summary>
public static class Fa
{
    private const string FaDigits = "۰۱۲۳۴۵۶۷۸۹";
    private const string ArDigits = "٠١٢٣٤٥٦٧٨٩";

    /// <summary>تبدیل ارقام لاتین به فارسی</summary>
    public static string D(object? n)
    {
        string s = n switch
        {
            null => "",
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            decimal m => m.ToString("0.###", CultureInfo.InvariantCulture),
            _ => Convert.ToString(n, CultureInfo.InvariantCulture) ?? ""
        };
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is >= '0' and <= '9') sb.Append(FaDigits[c - '0']);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>تبدیل ارقام فارسی/عربی به لاتین</summary>
    public static string En(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            var i = FaDigits.IndexOf(c);
            if (i >= 0) { sb.Append((char)('0' + i)); continue; }
            i = ArDigits.IndexOf(c);
            if (i >= 0) { sb.Append((char)('0' + i)); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>«۳ ساعت» / «۴۵ دقیقه» / «۲:۳۰»</summary>
    public static string Hm(int minutes)
    {
        minutes = Math.Max(0, minutes);
        int h = minutes / 60, m = minutes % 60;
        if (h == 0) return $"{D(m)} دقیقه";
        if (m == 0) return $"{D(h)} ساعت";
        return $"{D(h)}:{D($"{m:D2}")}";
    }

    /// <summary>همیشه «ساعت:دقیقه» فارسی</summary>
    public static string Hmc(int minutes)
    {
        minutes = Math.Max(0, minutes);
        return $"{D(minutes / 60)}:{D($"{minutes % 60:D2}")}";
    }

    /// <summary>ساعت اعشاری مثل «۳.۵»</summary>
    public static string Hours(double minutes)
    {
        var v = Math.Round(minutes / 60.0, 1, MidpointRounding.AwayFromZero)
            .ToString("0.0", CultureInfo.InvariantCulture);
        if (v.EndsWith(".0")) v = v[..^2];
        return D(v);
    }

    /// <summary>«۲۷ شهریور ۱۴۰۵»</summary>
    public static string FmtDate(string jalaliDate)
    {
        var j = Services.Jalali.Parse(jalaliDate);
        if (j == null) return jalaliDate;
        return $"{D(j.Value.Jd)} {Services.Jalali.Months[j.Value.Jm - 1]} {D(j.Value.Jy)}";
    }

    /// <summary>«۲۷ شهریور»</summary>
    public static string FmtShort(string jalaliDate)
    {
        var j = Services.Jalali.Parse(jalaliDate);
        if (j == null) return jalaliDate;
        return $"{D(j.Value.Jd)} {Services.Jalali.Months[j.Value.Jm - 1]}";
    }

    /// <summary>«شنبه، ۲۷ شهریور ۱۴۰۵»</summary>
    public static string FmtFull(string jalaliDate)
        => $"{Services.Jalali.Weekdays[Services.Jalali.Weekday(jalaliDate)]}، {FmtDate(jalaliDate)}";

    /// <summary>درصد کنکوری با نمره منفی: (۳×درست − غلط) ÷ (۳×کل)</summary>
    public static double? PercentOf(int correct, int wrong, int total)
    {
        if (total == 0 || correct + wrong == 0) return null;
        return Math.Round((3.0 * correct - wrong) / (3.0 * total) * 1000) / 10;
    }

    /// <summary>درصد ساده: درست ÷ کل</summary>
    public static double? PercentSimple(int correct, int total)
        => total > 0 ? Math.Round(correct * 1000.0 / total) / 10 : null;

    public static string TimeFa(string t) => string.IsNullOrEmpty(t) ? "" : D(t);

    /// <summary>سلام بر اساس ساعت روز</summary>
    public static string Greeting(string? name)
    {
        var h = DateTime.Now.Hour;
        var g = h < 5 ? "شب بخیر" : h < 12 ? "صبح بخیر" : h < 15 ? "ظهر بخیر" : h < 19 ? "عصر بخیر" : "شب بخیر";
        return string.IsNullOrWhiteSpace(name) ? g : $"{g}، {name}";
    }

    /// <summary>جمله‌های انگیزشی — مثل نسخهٔ اصلی، هر روز یک جمله</summary>
    public static readonly string[] Quotes =
    {
        "فقط شروع کن. ادامه‌ش خودش میاد.",
        "آینده‌ت داره از امروزت ساخته میشه.",
        "رتبه خوب خودش درس نمی‌خونه، داداش.",
        "قهوه هست. جزوه هست. پس بهانه چیه؟",
        "یه روز دیگه، یه عالمه تست دیگه.",
        "هدف: قبولی. وضعیت فعلی: در حال رنج کشیدن.",
        "هر ساعت مطالعه = یک قدم کمتر تا رتبه‌ای که می‌خوای.",
        "کم‌کم؛ ولی هر روز.",
        "بزن بریم؛ قبل از اینکه مغزت یه بهونه جدید پیدا کنه.",
        "هیچ رتبه‌ای با برنامه قشنگ ساخته نشد؛ با اجرای برنامه ساخته شد.",
        "زبان بخون؛ حداقل یه زبانی بلدی غر بزنی.",
        "یه کم دیگه.",
        "فقط اجرا.",
        "برنامه‌ریزی تموم شد؛ وقت خراب کردن برنامه‌ست.",
    };

    /// <summary>جملهٔ روز (بر اساس تاریخ شمسی) با امکان جابه‌جایی</summary>
    public static string QuoteOfDay(int offset = 0)
    {
        var t = Services.Jalali.TodayJalali();
        var seed = Services.Jalali.DiffDays("1400-01-01", t);
        return Quotes[(int)(((long)seed + offset + (long)Quotes.Length * 1024) % Quotes.Length)];
    }

    public static string HtmlEncode(string? s) => HttpUtility.HtmlEncode(s ?? "");

    /// <summary>اعتبارسنجی ساعت «HH:mm»</summary>
    public static bool IsValidTime(string? t)
        => !string.IsNullOrEmpty(t) && System.Text.RegularExpressions.Regex.IsMatch(t, @"^([01]\d|2[0-3]):[0-5]\d$");

    /// <summary>اختلاف دقیقه بین دو ساعت (با پشتیبانی عبور از نیمه‌شب)</summary>
    public static int DiffMinutes(string a, string b)
    {
        static int M(string t) => int.Parse(t[..2]) * 60 + int.Parse(t[3..]);
        var d = M(b) - M(a);
        if (d < 0) d += 24 * 60;
        return d;
    }
}

/// <summary>آیکون‌های SVG (مجموعهٔ Lucide) — همان آیکون‌های نسخهٔ اصلی</summary>
public static class Svg
{
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["book"] = "<path d=\"M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20\"/>",
        ["home"] = "<path d=\"m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z\"/><path d=\"M9 22V12h6v10\"/>",
        ["sun"] = "<circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41\"/>",
        ["moon"] = "<path d=\"M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z\"/>",
        ["monitor"] = "<rect x=\"2\" y=\"3\" width=\"20\" height=\"14\" rx=\"2\"/><path d=\"M8 21h8M12 17v4\"/>",
        ["week"] = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"18\" rx=\"2\"/><path d=\"M8 2v4M16 2v4M3 10h18M8 14h.01M12 14h.01M16 14h.01M8 18h.01M12 18h.01M16 18h.01\"/>",
        ["calendar"] = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"18\" rx=\"2\"/><path d=\"M8 2v4M16 2v4M3 10h18\"/>",
        ["chart"] = "<path d=\"M3 3v18h18M18 17V9M13 17V5M8 17v-3\"/>",
        ["settings"] = "<path d=\"M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6\"/>",
        ["left"] = "<path d=\"m15 18-6-6 6-6\"/>",
        ["right"] = "<path d=\"m9 18 6-6-6-6\"/>",
        ["plus"] = "<path d=\"M5 12h14M12 5v14\"/>",
        ["pencil"] = "<path d=\"M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z\"/>",
        ["trash"] = "<path d=\"M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2\"/>",
        ["check"] = "<path d=\"M20 6 9 17l-5-5\"/>",
        ["x"] = "<path d=\"M18 6 6 18M6 6l12 12\"/>",
        ["clock"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M12 6v6l4 2\"/>",
        ["tests"] = "<path d=\"m3 17 2 2 4-4M3 7l2 2 4-4M13 6h8M13 12h8M13 18h8\"/>",
        ["target"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><circle cx=\"12\" cy=\"12\" r=\"6\"/><circle cx=\"12\" cy=\"12\" r=\"2\"/>",
        ["trophy"] = "<path d=\"M6 9H4.5a2.5 2.5 0 0 1 0-5H6M18 9h1.5a2.5 2.5 0 0 0 0-5H18M4 22h16M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22M18 2H6v7a6 6 0 0 0 12 0V2Z\"/>",
        ["flame"] = "<path d=\"M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z\"/>",
        ["download"] = "<path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3\"/>",
        ["upload"] = "<path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12\"/>",
        ["database"] = "<ellipse cx=\"12\" cy=\"5\" rx=\"9\" ry=\"3\"/><path d=\"M3 5v14a9 3 0 0 0 18 0V5M3 12a9 3 0 0 0 18 0\"/>",
        ["list"] = "<path d=\"M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01\"/>",
        ["inbox"] = "<path d=\"M22 12h-6l-2 3h-4l-2-3H2\"/><path d=\"M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z\"/>",
        ["star"] = "<path d=\"m12 2 3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z\"/>",
        ["sigma"] = "<path d=\"M18 7V4H6l6 8-6 8h12v-3\"/>",
        ["save"] = "<path d=\"M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z\"/><path d=\"M17 21v-8H7v8M7 3v5h8\"/>",
        ["camera"] = "<path d=\"M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z\"/><circle cx=\"12\" cy=\"13\" r=\"3\"/>",
        ["folder"] = "<path d=\"M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z\"/>",
        ["timer"] = "<path d=\"M10 2h4M12 14l3-3\"/><circle cx=\"12\" cy=\"14\" r=\"8\"/>",
        ["graduation"] = "<path d=\"M22 10v6M2 10l10-5 10 5-10 5z\"/><path d=\"M6 12v5c3 3 9 3 12 0v-5\"/>",
        ["note"] = "<path d=\"M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z\"/><path d=\"M14 2v6h6M16 13H8M16 17H8M10 9H8\"/>",
        ["activity"] = "<path d=\"M22 12h-4l-3 9L9 3l-3 9H2\"/>",
        ["print"] = "<path d=\"M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><path d=\"M6 14h12v8H6z\"/>",
        ["compare"] = "<path d=\"M16 3h5v5M4 20 21 3M21 16v5h-5M15 15l6 6M4 4l5 5\"/>",
        ["sheet"] = "<path d=\"M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z\"/><path d=\"M14 2v6h6M8 13h8M8 17h8M8 9h2\"/>",
        ["zap"] = "<path d=\"M13 2 3 14h9l-1 8 10-12h-9l1-8z\"/>",
        ["help"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3M12 17h.01\"/>",
        ["alert"] = "<path d=\"m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z\"/><path d=\"M12 9v4M12 17h.01\"/>",
        ["brain"] = "<path d=\"M12 5a3 3 0 1 0-6 0 3 3 0 0 0 6 0Z\"/><path d=\"M12 8a5 5 0 1 0-10 0 5 5 0 0 0 10 0Z\"/><path d=\"M12 11a7 7 0 1 0-14 0 7 7 0 0 0 14 0Z\"/><path d=\"M12 14a9 9 0 1 0-18 0 9 9 0 0 0 18 0Z\"/>",
        ["eye"] = "<path d=\"M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["minus"] = "<path d=\"M5 12h14\"/>",
    };

    /// <summary>SVG آیکون با نام داده‌شده (مثل نسخهٔ اصلی)</summary>
    public static string Ic(string name, string cls = "")
    {
        var path = Icons.TryGetValue(name, out var p) ? p : Icons["help"];
        return $"<svg class=\"ic {cls}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">{path}</svg>";
    }
}
