using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Models;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;
    private readonly SettingsService _settings;

    public HomeController(AppDbContext db, StatsService stats, SettingsService settings)
    {
        _db = db;
        _stats = stats;
        _settings = settings;
    }

    /// <summary>داشبورد: شمارش معکوس، خلاصهٔ امروز، برنامه، یادداشت، مقایسه، هفته و نقشهٔ مطالعه</summary>
    public async Task<IActionResult> Index(string? hm)
    {
        var t = Jalali.TodayJalali();
        var y = Jalali.AddDays(t, -1);
        var ws = Jalali.WeekStart(t);
        var we = Jalali.AddDays(ws, 6);
        var hmRange = hm == "12" ? 12 : 6;
        var hmFrom = Jalali.WeekStart(Jalali.AddDays(t, -(hmRange == 12 ? 365 : 182)));
        var now = DateTime.UtcNow;
        ViewData["HmRange"] = hmRange;

        var vm = new DashboardVm
        {
            Today = t,
            Settings = await _settings.GetAllAsync(),
            Entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
                .Where(e => e.Date == t)
                .OrderBy(e => e.StartTime == "" ? 1 : 0).ThenBy(e => e.StartTime).ThenBy(e => e.Id)
                .ToListAsync(),
            Subjects = await _stats.SubjectsAsync(),
            Yesterday = await _stats.StatsAsync(y, y, true),
            Last7 = await _stats.StatsAsync(Jalali.AddDays(t, -7), y, true),
            WeekStats = await _stats.StatsAsync(ws, we),
            WeekStatsDone = await _stats.StatsAsync(ws, we, true),
            WeekGoals = await _db.Goals.AsNoTracking().Where(g => g.Period == "week" && g.Key == ws).ToListAsync(),
            Records = await _stats.RecordsAsync(),
            TodayNote = (await _db.DayNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Date == t))?.Text ?? "",
            HeatStats = await _stats.StatsAsync(hmFrom, t, true),
            HeatFrom = hmFrom,
            DueReviews = await _db.ReviewItems.AsNoTracking().Include(r => r.Subject)
                .Where(r => r.DueAt <= now)
                .OrderBy(r => r.DueAt)
                .ToListAsync(),
            WeekStart = ws,
            WeekEnd = we,
        };

        return View(vm);
    }

    /// <summary>تغییر تاریخ کنکور از داشبورد (فراخوانی Ajax از انتخابگر تاریخ)</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetExamDate(string date)
    {
        if (Jalali.Parse(date) == null) return Json(new { error = "تاریخ نامعتبر" });
        await _settings.SetAsync("exam_date", date);
        return Json(new { ok = true });
    }

    public IActionResult Error()
    {
        ViewData["StatusCode"] = Response.StatusCode;
        return View();
    }
}
