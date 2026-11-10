using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Models;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

/// <summary>All Plan: جدول بزرگ «برنامه هفتگی انجام شده» برای چاپ (A3 افقی)</summary>
public class AllPlanController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;

    public AllPlanController(AppDbContext db, StatsService stats)
    {
        _db = db;
        _stats = stats;
    }

    public async Task<IActionResult> Index(string? range)
    {
        range = range is "4" or "8" or "12" or "all" ? range : "all";
        var today = Jalali.TodayJalali();
        var records = await _stats.RecordsAsync();

        var thisWeek = Jalali.WeekStart(today);
        var firstWeek = records.FirstDay != null ? Jalali.WeekStart(records.FirstDay) : thisWeek;
        if (range != "all")
        {
            var n = int.Parse(range);
            var alt = Jalali.AddDays(thisWeek, -7 * (n - 1));
            if (string.CompareOrdinal(alt, firstWeek) > 0) firstWeek = alt;
        }

        var weeks = new List<string>();
        for (var w = firstWeek; string.CompareOrdinal(w, thisWeek) <= 0; w = Jalali.AddDays(w, 7)) weeks.Add(w);

        var last = Jalali.AddDays(thisWeek, 6);
        var entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
            .Where(e => string.Compare(e.Date, firstWeek) >= 0 && string.Compare(e.Date, last) <= 0)
            .ToListAsync();

        var allSubjects = await _db.Subjects.AsNoTracking().OrderBy(s => s.Archived).ThenBy(s => s.Sort).ToListAsync();
        var used = entries.Select(e => e.SubjectId).ToHashSet();
        var subjects = allSubjects.Where(s => !s.Archived || used.Contains(s.Id)).ToList();

        var settings = await _db.Settings.AsNoTracking().ToListAsync();
        string Get(string k, string def) => settings.FirstOrDefault(s => s.Key == k)?.Value ?? def;

        var vm = new AllPlanVm
        {
            Range = range,
            FirstWeek = firstWeek,
            ThisWeek = thisWeek,
            Weeks = weeks,
            Subjects = subjects,
            UserName = Get("user_name", ""),
            ExamTitle = Get("exam_title", "کنکور ارشد کامپیوتر"),
            ExamDate = Get("exam_date", "1406-02-16"),
        };

        // تجمیع هفته × درس + جمع هر درس
        foreach (var s in subjects) vm.SubTotal[s.Id] = new WeekAgg();
        foreach (var e in entries)
        {
            var w = Jalali.WeekStart(e.Date);
            var key = (w, e.SubjectId);
            if (!vm.Agg.TryGetValue(key, out var a)) vm.Agg[key] = a = new WeekAgg();
            foreach (var tgt in new[] { a, vm.SubTotal.GetValueOrDefault(e.SubjectId) })
            {
                if (tgt == null) continue;
                tgt.Minutes += e.Minutes;
                tgt.Study += e.StudyMin;
                tgt.Review += e.ReviewMin;
                tgt.Test += e.TestMin;
                tgt.Tests += e.Tests;
            }
            if (!string.IsNullOrEmpty(e.Note))
            {
                // یادداشت زیر عنوانی می‌رود که آن آیتم بیشتر ازش داشته؛ بدون جزئیات → خواندن
                var kind = e.TestMin > 0 || (e.Tests > 0 && e.StudyMin == 0 && e.ReviewMin == 0) ? "test"
                    : e.ReviewMin > 0 && e.StudyMin == 0 ? "review" : "study";
                var text = e.Note + (e.Done ? "" : " ○");
                if (kind == "test") a.NotesTest.Add(text);
                else if (kind == "review") a.NotesReview.Add(text);
                else a.NotesStudy.Add(text);
            }
        }

        return View(vm);
    }
}

/// <summary>گزارش تفصیلی هفته (A4 عمودی)</summary>
public class ReportController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;

    public ReportController(AppDbContext db, StatsService stats)
    {
        _db = db;
        _stats = stats;
    }

    public async Task<IActionResult> Index(string? date)
    {
        var d = Jalali.Parse(date) != null ? date! : Jalali.TodayJalali();
        var start = Jalali.WeekStart(d);
        var end = Jalali.AddDays(start, 6);

        var notes = await _db.DayNotes.AsNoTracking()
            .Where(n => string.Compare(n.Date, start) >= 0 && string.Compare(n.Date, end) <= 0)
            .ToListAsync();

        var vm = new ReportVm
        {
            Start = start,
            End = end,
            Entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
                .Where(e => string.Compare(e.Date, start) >= 0 && string.Compare(e.Date, end) <= 0)
                .OrderBy(e => e.Date).ThenBy(e => e.StartTime == "" ? 1 : 0).ThenBy(e => e.StartTime).ThenBy(e => e.Id)
                .ToListAsync(),
            Notes = notes.ToDictionary(n => n.Date, n => n.Text),
            Goals = await _db.Goals.AsNoTracking().Where(g => g.Period == "week" && g.Key == start).ToListAsync(),
            Stats = await _stats.StatsAsync(start, end),
            Settings = (await _db.Settings.AsNoTracking().ToListAsync()).ToDictionary(s => s.Key, s => s.Value),
        };
        return View(vm);
    }
}

/// <summary>جدول خلاصهٔ هفته (A4 افقی)</summary>
public class SummaryController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;

    public SummaryController(AppDbContext db, StatsService stats)
    {
        _db = db;
        _stats = stats;
    }

    public async Task<IActionResult> Index(string? date)
    {
        var d = Jalali.Parse(date) != null ? date! : Jalali.TodayJalali();
        var start = Jalali.WeekStart(d);
        var end = Jalali.AddDays(start, 6);

        var vm = new SummaryVm
        {
            Start = start,
            End = end,
            Stats = await _stats.StatsAsync(start, end),
            Entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
                .Where(e => string.Compare(e.Date, start) >= 0 && string.Compare(e.Date, end) <= 0)
                .ToListAsync(),
            Settings = (await _db.Settings.AsNoTracking().ToListAsync()).ToDictionary(s => s.Key, s => s.Value),
        };
        return View(vm);
    }
}
