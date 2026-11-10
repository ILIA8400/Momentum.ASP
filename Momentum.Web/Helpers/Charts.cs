using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;
using Momentum.Web.Helpers;
using Momentum.Web.Models;
using Momentum.Web.Services;

namespace Momentum.Web.Helpers;

/// <summary>رندر سمت سرور نمودار روند روزانه (SVG) و نقشهٔ مطالعه — پورت از app.js</summary>
public static class Charts
{
    /// <summary>نمودار میله‌ای روند روزانه (۹۰ روز آخر بازه)</summary>
    public static HtmlString Trend(List<DayStat> byDay, string from, string to)
    {
        var map = byDay.ToDictionary(d => d.Date, d => d.Minutes);
        if (string.CompareOrdinal(from, "1301-01-01") < 0)
        {
            if (byDay.Count == 0)
                return new HtmlString($"<div class=\"empty\">{Svg.Ic("inbox")}داده‌ای نیست</div>");
            var last = byDay[^1].Date;
            var today = Jalali.TodayJalali();
            from = byDay[0].Date;
            to = string.CompareOrdinal(today, last) > 0 ? today : last;
        }

        var days = new List<string>();
        for (var d = from; string.CompareOrdinal(d, to) <= 0 && days.Count < 400; d = Jalali.AddDays(d, 1))
            days.Add(d);
        var shown = days.TakeLast(90).ToList();

        const double W = 900, H = 220, padL = 36, padB = 26, padT = 10;
        var max = Math.Max(60, shown.Count == 0 ? 60 : shown.Max(d => map.GetValueOrDefault(d)));
        var bw = (W - padL) / shown.Count;
        double Y(double m) => padT + (H - padT - padB) * (1 - m / max);

        var sb = new StringBuilder();
        sb.Append($"<svg class=\"trend\" viewBox=\"0 0 {W} {H}\" preserveAspectRatio=\"none\" direction=\"ltr\">");

        // خطوط شبکه
        foreach (var f in new[] { 0.0, 0.5, 1.0 })
        {
            var y = Y(max * f);
            sb.Append($"<line x1=\"{padL}\" x2=\"{W}\" y1=\"{y:0.##}\" y2=\"{y:0.##}\"/>");
            sb.Append($"<text x=\"{padL - 6:0.##}\" y=\"{y + 4:0.##}\" text-anchor=\"end\">{Fa.Hours(max * f)}</text>");
        }

        // میله‌ها
        for (int i = 0; i < shown.Count; i++)
        {
            var d = shown[i];
            if (!map.TryGetValue(d, out var m) || m == 0) continue;
            var x = padL + i * bw + 1;
            var w = Math.Max(2, bw - 2);
            var y = Y(m);
            var h = H - padB - y;
            var tip = $"{WebUtility.HtmlEncode(Fa.FmtFull(d))}: {WebUtility.HtmlEncode(Fa.Hm(m))}";
            sb.Append($"<rect class=\"bar\" x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{w:0.##}\" height=\"{h:0.##}\" rx=\"2\"><title>{tip}</title></rect>");
        }

        // برچسب‌ها
        var step = Math.Max(1, (int)Math.Ceiling(shown.Count / 12.0));
        for (int i = 0; i < shown.Count; i += step)
        {
            var x = padL + i * bw + bw / 2;
            sb.Append($"<text x=\"{x:0.##}\" y=\"{H - 8}\" text-anchor=\"middle\">{WebUtility.HtmlEncode(Fa.FmtShort(shown[i]))}</text>");
        }

        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>نقشهٔ مطالعه (مثل GitHub) برای بازهٔ داده‌شده</summary>
    public static HtmlString Heatmap(List<DayStat> byDay, string from, string to)
    {
        var map = byDay.ToDictionary(d => d.Date, d => d);
        var minutes = byDay.Select(d => d.Minutes).Where(m => m > 0).OrderBy(m => m).ToList();
        int[] th = { 60, 120, 240 };
        if (minutes.Count >= 8)
        {
            int Q(double p) => minutes[Math.Min(minutes.Count - 1, (int)Math.Floor(p * minutes.Count))];
            var t = new[] { Q(0.25), Q(0.5), Q(0.75) };
            if (t[0] < t[1] && t[1] < t[2]) th = t;
        }
        int Level(int m) => m == 0 ? 0 : m <= th[0] ? 1 : m <= th[1] ? 2 : m <= th[2] ? 3 : 4;

        var start = Jalali.WeekStart(from);
        var weeks = new List<string>();
        for (var w = start; string.CompareOrdinal(w, to) <= 0; w = Jalali.AddDays(w, 7)) weeks.Add(w);

        var today = Jalali.TodayJalali();
        var months = new List<string>();
        for (int i = 0; i < weeks.Count; i++)
        {
            var j = Jalali.Parse(weeks[i])!.Value;
            var show = i == 0 || Jalali.Parse(weeks[i - 1])!.Value.Jm != j.Jm;
            months.Add(show ? Jalali.Months[j.Jm - 1] : "");
        }

        var sb = new StringBuilder();
        sb.Append($"<div class=\"hm-wrap\" data-weeks=\"{weeks.Count}\"><div class=\"hm-scroll\" id=\"hmScroll\">");
        sb.Append($"<div class=\"hm-months\" style=\"grid-template-columns:repeat({weeks.Count},var(--hm))\">");
        foreach (var m in months) sb.Append($"<span>{WebUtility.HtmlEncode(m)}</span>");
        sb.Append("</div><div class=\"hm-body\"><div class=\"hm-days\">");
        for (int i = 0; i < 7; i++)
            sb.Append($"<span>{(i % 2 == 0 ? Jalali.Weekdays[i][..1] : "")}</span>");
        sb.Append($"</div><div class=\"hm-grid\" style=\"grid-template-columns:repeat({weeks.Count},var(--hm))\">");

        foreach (var w in weeks)
        {
            sb.Append("<div class=\"hm-col\">");
            for (int i = 0; i < 7; i++)
            {
                var d = Jalali.AddDays(w, i);
                if (string.CompareOrdinal(d, from) < 0 || string.CompareOrdinal(d, to) > 0)
                {
                    sb.Append("<i class=\"hm-cell hm-empty\"></i>");
                    continue;
                }
                map.TryGetValue(d, out var x);
                var lv = Level(x?.Minutes ?? 0);
                var tip = string.Join("\n", new[]
                {
                    Fa.FmtFull(d),
                    $"مطالعه: {(x != null ? Fa.Hm(x.Minutes) : "هیچ")}",
                    x is { Tests: > 0 } ? $"تست: {Fa.D(x.Tests)}" : "",
                    x?.Percent != null ? $"درصد: {Fa.D(x.Percent)}٪" : ""
                }.Where(s => !string.IsNullOrEmpty(s)));
                var cls = $"hm-cell l{lv}" + (d == today ? " today" : "");
                sb.Append($"<i class=\"{cls}\" data-d=\"{d}\" data-tip=\"{WebUtility.HtmlEncode(tip)}\"></i>");
            }
            sb.Append("</div>");
        }
        sb.Append("</div></div></div>");

        var studied = byDay.Count(d => d.Minutes > 0);
        var total = byDay.Sum(d => d.Minutes);
        var best = byDay.OrderByDescending(d => d.Minutes).FirstOrDefault();

        sb.Append("<div class=\"hm-foot\">");
        sb.Append($"<span class=\"sub\">{Fa.D(studied)} روز مطالعه · {Fa.Hours(total)} ساعت");
        if (best is { Minutes: > 0 })
            sb.Append($" · بهترین روز: {Fa.Hm(best.Minutes)} ({Fa.FmtShort(best.Date)})");
        sb.Append("</span><span class=\"spacer\"></span>");
        sb.Append("<span class=\"hm-legend\"><span class=\"muted\">کمتر</span>");
        sb.Append($"<i class=\"hm-cell l0\"></i><i class=\"hm-cell l1\" title=\"تا {Fa.Hm(th[0])}\"></i>");
        sb.Append($"<i class=\"hm-cell l2\" title=\"تا {Fa.Hm(th[1])}\"></i><i class=\"hm-cell l3\" title=\"تا {Fa.Hm(th[2])}\"></i>");
        sb.Append($"<i class=\"hm-cell l4\" title=\"بیشتر از {Fa.Hm(th[2])}\"></i><span class=\"muted\">بیشتر</span></span>");
        sb.Append("</div><div class=\"hm-tip\" id=\"hmTip\"></div></div>");

        return new HtmlString(sb.ToString());
    }

    /// <summary>نوار سهم درس‌های امروز (روی داشبورد)</summary>
    public static HtmlString Daybar(IEnumerable<Entry> entries, int total)
    {
        var per = entries
            .GroupBy(e => new { e.SubjectId, Name = e.Subject!.Name, Color = e.Subject.Color })
            .Select(g => new { g.Key.Name, g.Key.Color, Min = g.Sum(e => e.Minutes) })
            .OrderByDescending(x => x.Min)
            .ToList();
        if (total <= 0 || per.Count == 0) return HtmlString.Empty;

        var sb = new StringBuilder("<div class=\"daybar\">");
        foreach (var s in per)
        {
            var label = s.Min / (double)total > 0.12 ? WebUtility.HtmlEncode(s.Name) : "";
            sb.Append($"<span style=\"flex:{s.Min};background:{s.Color}\" title=\"{WebUtility.HtmlEncode(s.Name)}: {Fa.Hm(s.Min)}\">{label}</span>");
        }
        sb.Append("</div><div class=\"daybar-legend\">");
        foreach (var s in per)
            sb.Append($"<span><span class=\"dot\" style=\"background:{s.Color}\"></span>{WebUtility.HtmlEncode(s.Name)} <b>{Fa.Hmc(s.Min)}</b></span>");
        sb.Append("</div>");
        return new HtmlString(sb.ToString());
    }
}
