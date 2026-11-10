using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class WeekController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;

    public WeekController(AppDbContext db, StatsService stats)
    {
        _db = db;
        _stats = stats;
    }

    public async Task<IActionResult> Index(string? date)
    {
        var d = Jalali.Parse(date) != null ? date! : Jalali.TodayJalali();
        var start = Jalali.WeekStart(d);
        var end = Jalali.AddDays(start, 6);

        var vm = new WeekVm
        {
            Start = start,
            End = end,
            Entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
                .Where(e => string.Compare(e.Date, start) >= 0 && string.Compare(e.Date, end) <= 0)
                .OrderBy(e => e.Date).ThenBy(e => e.StartTime == "" ? 1 : 0).ThenBy(e => e.StartTime).ThenBy(e => e.Id)
                .ToListAsync(),
            Stats = await _stats.StatsAsync(start, end),
            StatsDone = await _stats.StatsAsync(start, end, true),
            Goals = await _db.Goals.AsNoTracking().Where(g => g.Period == "week" && g.Key == start).ToListAsync(),
            Subjects = await _stats.SubjectsAsync(),
        };
        return View(vm);
    }
}
