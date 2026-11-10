using Microsoft.AspNetCore.Mvc;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class StatsController : Controller
{
    private readonly StatsService _stats;

    public StatsController(StatsService stats) => _stats = stats;

    public async Task<IActionResult> Index(string? kind, string? from, string? to, string? mode)
    {
        kind ??= "month";
        mode = mode == "all" ? "all" : "done";
        var t = Jalali.TodayJalali();

        string f, e;
        switch (kind)
        {
            case "week":
                (f, e) = (Jalali.WeekStart(t), Jalali.AddDays(Jalali.WeekStart(t), 6));
                break;
            case "30":
                (f, e) = (Jalali.AddDays(t, -29), t);
                break;
            case "all":
                (f, e) = ("1300-01-01", "1500-12-29");
                break;
            case "custom":
                f = Jalali.Parse(from) != null ? from! : t;
                e = Jalali.Parse(to) != null ? to! : t;
                if (string.CompareOrdinal(f, e) > 0) (f, e) = (e, f);
                break;
            default:
                kind = "month";
                var (mf, ml) = Jalali.MonthRange(t);
                (f, e) = (mf, ml);
                break;
        }

        var vm = new StatsVm
        {
            Kind = kind,
            From = f,
            To = e,
            Mode = mode,
            Stats = await _stats.StatsAsync(f, e, mode == "done"),
            Records = await _stats.RecordsAsync(),
        };
        return View(vm);
    }

    /// <summary>روزهایی که در آن‌ها آیتم ثبت شده (برای علامت‌گذاری تقویم انتخابگر تاریخ)</summary>
    public async Task<IActionResult> Marked(string from, string to)
    {
        if (Jalali.Parse(from) == null || Jalali.Parse(to) == null)
            return Json(Array.Empty<string>());

        var stats = await _stats.StatsAsync(from!, to!);
        return Json(stats.ByDay.Select(d => d.Date).ToArray());
    }
}
