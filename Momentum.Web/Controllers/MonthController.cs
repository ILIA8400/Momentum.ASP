using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class MonthController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;

    public MonthController(AppDbContext db, StatsService stats)
    {
        _db = db;
        _stats = stats;
    }

    public async Task<IActionResult> Index(string? date)
    {
        var d = Jalali.Parse(date) != null ? date! : Jalali.TodayJalali();
        var j = Jalali.Parse(d)!.Value;
        var (first, last) = Jalali.MonthRange(d);
        var mk = Jalali.MonthKey(d);

        var vm = new MonthVm
        {
            MonthKey = mk,
            Jy = j.Jy,
            Jm = j.Jm,
            First = first,
            Last = last,
            Len = Jalali.JalaliMonthLength(j.Jy, j.Jm),
            Entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
                .Where(e => string.Compare(e.Date, first) >= 0 && string.Compare(e.Date, last) <= 0)
                .ToListAsync(),
            Stats = await _stats.StatsAsync(first, last),
            StatsDone = await _stats.StatsAsync(first, last, true),
            Goals = await _db.Goals.AsNoTracking().Where(g => g.Period == "month" && g.Key == mk).ToListAsync(),
            Subjects = await _stats.SubjectsAsync(),
        };
        return View(vm);
    }
}
