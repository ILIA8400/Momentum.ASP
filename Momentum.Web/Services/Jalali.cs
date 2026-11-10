using System.Globalization;

namespace Momentum.Web.Services;

/// <summary>
/// تبدیل جلالی ↔ میلادی — پورت دقیق کتابخانهٔ jalaali-js (MIT) که در نسخهٔ اصلی Momentum استفاده شده بود.
/// تاریخ‌ها به شکل رشتهٔ «YYYY-MM-DD» با ماه/روز دو رقمی دست و پا می‌شوند.
/// روز هفته: شنبه = ۰ … جمعه = ۶
/// </summary>
public static class Jalali
{
    public static readonly string[] Months =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };

    public static readonly string[] Weekdays =
    {
        "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه"
    };

    private static readonly int[] Breaks =
    {
        -61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635,
        2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178
    };

    public readonly record struct JDate(int Jy, int Jm, int Jd);

    // تقسیم و باقیماندهٔ صحیح با گرد شدن به سمت صفر (معادل ~~ در جاوااسکریپت)
    private static long TDiv(long a, long b) => a / b;
    private static long TMod(long a, long b) => a - (a / b) * b;

    private static (long leap, long gy, long march) JalCal(int jy)
    {
        long gy = jy + 621;
        long leapJ = -14, jp = Breaks[0], jm, jump = 0;

        for (int i = 1; i < Breaks.Length; i++)
        {
            jm = Breaks[i];
            jump = jm - jp;
            if (jy < jm) break;
            leapJ += TDiv(jump, 33) * 8 + TDiv(TMod(jump, 33), 4);
            jp = jm;
        }

        long n = jy - jp;
        leapJ += TDiv(n, 33) * 8 + TDiv(TMod(n, 33) + 3, 4);
        if (TMod(jump, 33) == 4 && jump - n == 4) leapJ += 1;

        long leapG = TDiv(gy, 4) - TDiv((TDiv(gy, 100) + 1) * 3, 4) - 150;
        long march = 20 + leapJ - leapG;

        if (jump - n < 6) n = n - jump + TDiv(jump + 4, 33) * 33;

        long leap = TMod(TMod(n + 1, 33) - 1, 4);
        if (leap == -1) leap = 4;
        return (leap, gy, march);
    }

    private static long G2D(long gy, long gm, long gd)
    {
        long d = TDiv((gy + TDiv(gm - 8, 6) + 100100) * 1461, 4)
                 + TDiv(153 * TMod(gm + 9, 12) + 2, 5)
                 + gd - 34840408;
        d = d - TDiv(TDiv(gy + 100100 + TDiv(gm - 8, 6), 100) * 3, 4) + 752;
        return d;
    }

    private static (long gy, long gm, long gd) D2G(long jdn)
    {
        long j = 4 * jdn + 139361631;
        j = j + TDiv(TDiv(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908;
        long i = TDiv(TMod(j, 1461), 4) * 5 + 308;
        long gd = TDiv(TMod(i, 153), 5) + 1;
        long gm = TMod(TDiv(i, 153), 12) + 1;
        long gy = TDiv(j, 1461) - 100100 + TDiv(8 - gm, 6);
        return (gy, gm, gd);
    }

    private static long J2D(int jy, int jm, int jd)
    {
        var r = JalCal(jy);
        return G2D(r.gy, 3, r.march) + (jm - 1) * 31 - TDiv(jm, 7) * (jm - 7) + jd - 1;
    }

    private static JDate D2J(long jdn)
    {
        long gy = D2G(jdn).gy;
        int jy = (int)gy - 621;
        var r = JalCal(jy);
        long jdn1f = G2D(gy, 3, r.march);
        long k = jdn - jdn1f;
        if (k >= 0)
        {
            if (k <= 185) return new JDate(jy, (int)(1 + TDiv(k, 31)), (int)(TMod(k, 31) + 1));
            k -= 186;
        }
        else
        {
            jy -= 1;
            k += 179;
            if (r.leap == 1) k += 1;
        }
        return new JDate(jy, (int)(7 + TDiv(k, 30)), (int)(TMod(k, 30) + 1));
    }

    public static JDate ToJalali(int gy, int gm, int gd) => D2J(G2D(gy, gm, gd));

    public static (int Gy, int Gm, int Gd) ToGregorian(int jy, int jm, int jd)
    {
        var g = D2G(J2D(jy, jm, jd));
        return ((int)g.gy, (int)g.gm, (int)g.gd);
    }

    public static int JalaliMonthLength(int jy, int jm)
    {
        if (jm <= 6) return 31;
        if (jm <= 11) return 30;
        return JalCal(jy).leap == 0 ? 30 : 29;
    }

    public static string Format(JDate j) => $"{j.Jy:D4}-{j.Jm:D2}-{j.Jd:D2}";

    /// <summary>«1405-06-27» را به JDate تبدیل می‌کند؛ نامعتبر بودن → null</summary>
    public static JDate? Parse(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 10 || s[4] != '-' || s[7] != '-') return null;
        if (!int.TryParse(s.AsSpan(0, 4), out var jy) ||
            !int.TryParse(s.AsSpan(5, 2), out var jm) ||
            !int.TryParse(s.AsSpan(8, 2), out var jd)) return null;
        if (jm < 1 || jm > 12 || jd < 1 || jd > JalaliMonthLength(jy, jm)) return null;
        return new JDate(jy, jm, jd);
    }

    public static string AddDays(string s, int n)
    {
        var j = Parse(s) ?? throw new FormatException($"تاریخ نامعتبر: {s}");
        return Format(D2J(J2D(j.Jy, j.Jm, j.Jd) + n));
    }

    /// <summary>روز هفته: شنبه = ۰ … جمعه = ۶</summary>
    public static int Weekday(string s)
    {
        var j = Parse(s) ?? throw new FormatException($"تاریخ نامعتبر: {s}");
        var g = ToGregorian(j.Jy, j.Jm, j.Jd);
        var dow = (int)new DateTime(g.Gy, g.Gm, g.Gd).DayOfWeek; // یکشنبه = ۰
        return (dow + 1) % 7;
    }

    /// <summary>تاریخ شنبهٔ همان هفته</summary>
    public static string WeekStart(string s) => AddDays(s, -Weekday(s));

    /// <summary>تفاوت روزها: b − a (اگر b بعد از a باشد مثبت است)</summary>
    public static int DiffDays(string a, string b)
    {
        var pa = Parse(a) ?? throw new FormatException($"تاریخ نامعتبر: {a}");
        var pb = Parse(b) ?? throw new FormatException($"تاریخ نامعتبر: {b}");
        return (int)(J2D(pb.Jy, pb.Jm, pb.Jd) - J2D(pa.Jy, pa.Jm, pa.Jd));
    }

    public static string TodayJalali()
    {
        var d = DateTime.Today;
        return Format(ToJalali(d.Year, d.Month, d.Day));
    }

    /// <summary>ماهِ تاریخ داده‌شده به شکل «1405-06»</summary>
    public static string MonthKey(string s) => s[..7];

    /// <summary>اولین و آخرین روز ماهِ تاریخ داده‌شده</summary>
    public static (string First, string Last) MonthRange(string s)
    {
        var j = Parse(s) ?? throw new FormatException($"تاریخ نامعتبر: {s}");
        var mk = MonthKey(s);
        return ($"{mk}-01", $"{mk}-{JalaliMonthLength(j.Jy, j.Jm):D2}");
    }

    public static DateTime ToDateTime(string s)
    {
        var j = Parse(s) ?? throw new FormatException($"تاریخ نامعتبر: {s}");
        var g = ToGregorian(j.Jy, j.Jm, j.Jd);
        return new DateTime(g.Gy, g.Gm, g.Gd);
    }

    /// <summary>تبدیل تاریخ میلادی DateTime به رشتهٔ شمسی</summary>
    public static string FromDateTime(DateTime d) => Format(ToJalali(d.Year, d.Month, d.Day));
}
