using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Models;

namespace Momentum.Web.Services;

public class SubjectStat
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public int Minutes { get; set; }
    public int Tests { get; set; }
    public int StudyMin { get; set; }
    public int ReviewMin { get; set; }
    public int TestMin { get; set; }
    public int Correct { get; set; }
    public int Wrong { get; set; }
    public int ScoredTests { get; set; }
    public int Count { get; set; }
    public int Done { get; set; }
    public double? Percent { get; set; }
}

public class DayStat
{
    public string Date { get; set; } = "";
    public int Minutes { get; set; }
    public int Tests { get; set; }
    public int Correct { get; set; }
    public int Wrong { get; set; }
    public int ScoredTests { get; set; }
    public int Count { get; set; }
    public int Done { get; set; }
    public double? Percent { get; set; }
}

public class StatsResult
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Total { get; set; }
    public int Tests { get; set; }
    public int Count { get; set; }
    public int Done { get; set; }
    public double? Percent { get; set; }
    public List<SubjectStat> BySubject { get; set; } = new();
    public List<DayStat> ByDay { get; set; } = new();
}

public class WeekRec
{
    public string Key { get; set; } = "";
    public int Minutes { get; set; }
    public int Tests { get; set; }
}

public class RecordsResult
{
    public DayStat? BestDay { get; set; }
    public DayStat? BestTestDay { get; set; }
    public WeekRec? BestWeek { get; set; }
    public WeekRec? BestTestWeek { get; set; }
    public int LongestStreak { get; set; }
    public int CurrentStreak { get; set; }
    public int TotalDays { get; set; }
    public string? FirstDay { get; set; }
}

/// <summary>آمار و رکوردها — پورت مستقیم از stats/records در db.js نسخهٔ اصلی</summary>
public class StatsService
{
    private readonly AppDbContext _db;

    public StatsService(AppDbContext db) => _db = db;

    private static double? WithPercent(int correct, int wrong, int scoredTests)
        => Helpers.Fa.PercentOf(correct, wrong, scoredTests);

    public async Task<StatsResult> StatsAsync(string from, string to, bool doneOnly = false)
    {
        var q = _db.Entries.AsNoTracking()
            .Include(e => e.Subject)
            .Where(e => string.Compare(e.Date, from) >= 0 && string.Compare(e.Date, to) <= 0);
        if (doneOnly) q = q.Where(e => e.Done);
        var entries = await q.ToListAsync();

        var bySubject = entries
            .GroupBy(e => new { e.SubjectId, e.Subject!.Name, e.Subject.Color })
            .Select(g =>
            {
                var scored = g.Sum(e => e.Correct + e.Wrong > 0 ? e.Tests : 0);
                return new SubjectStat
                {
                    Id = g.Key.SubjectId,
                    Name = g.Key.Name,
                    Color = g.Key.Color,
                    Minutes = g.Sum(e => e.Minutes),
                    Tests = g.Sum(e => e.Tests),
                    StudyMin = g.Sum(e => e.StudyMin),
                    ReviewMin = g.Sum(e => e.ReviewMin),
                    TestMin = g.Sum(e => e.TestMin),
                    Correct = g.Sum(e => e.Correct),
                    Wrong = g.Sum(e => e.Wrong),
                    ScoredTests = scored,
                    Count = g.Count(),
                    Done = g.Count(e => e.Done),
                    Percent = WithPercent(g.Sum(e => e.Correct), g.Sum(e => e.Wrong), scored)
                };
            })
            .OrderByDescending(s => s.Minutes)
            .ToList();

        var byDay = entries
            .GroupBy(e => e.Date)
            .Select(g =>
            {
                var scored = g.Sum(e => e.Correct + e.Wrong > 0 ? e.Tests : 0);
                return new DayStat
                {
                    Date = g.Key,
                    Minutes = g.Sum(e => e.Minutes),
                    Tests = g.Sum(e => e.Tests),
                    Correct = g.Sum(e => e.Correct),
                    Wrong = g.Sum(e => e.Wrong),
                    ScoredTests = scored,
                    Count = g.Count(),
                    Done = g.Count(e => e.Done),
                    Percent = WithPercent(g.Sum(e => e.Correct), g.Sum(e => e.Wrong), scored)
                };
            })
            .OrderBy(d => d.Date)
            .ToList();

        var result = new StatsResult
        {
            From = from,
            To = to,
            Total = byDay.Sum(d => d.Minutes),
            Tests = byDay.Sum(d => d.Tests),
            Count = byDay.Sum(d => d.Count),
            Done = byDay.Sum(d => d.Done),
            Percent = WithPercent(byDay.Sum(d => d.Correct), byDay.Sum(d => d.Wrong), byDay.Sum(d => d.ScoredTests)),
            BySubject = bySubject,
            ByDay = byDay
        };
        return result;
    }

    /// <summary>رکوردها روی کل تاریخچه (فقط موارد انجام‌شده)</summary>
    public async Task<RecordsResult> RecordsAsync()
    {
        var days = (await _db.Entries.AsNoTracking()
                .Where(e => e.Done)
                .GroupBy(e => e.Date)
                .Select(g => new DayStat
                {
                    Date = g.Key,
                    Minutes = g.Sum(x => x.Minutes),
                    Tests = g.Sum(x => x.Tests)
                })
                .OrderBy(d => d.Date)
                .ToListAsync())
            .ToList();

        var bestDay = days.OrderByDescending(d => d.Minutes).FirstOrDefault(d => d.Minutes > 0);
        var bestTestDay = days.OrderByDescending(d => d.Tests).FirstOrDefault(d => d.Tests > 0);

        // تجمیع هفتگی
        var weeks = new Dictionary<string, WeekRec>();
        foreach (var d in days)
        {
            var k = Jalali.WeekStart(d.Date);
            if (!weeks.TryGetValue(k, out var w))
                weeks[k] = w = new WeekRec { Key = k };
            w.Minutes += d.Minutes;
            w.Tests += d.Tests;
        }
        var wl = weeks.Values.ToList();
        var bestWeek = wl.OrderByDescending(w => w.Minutes).FirstOrDefault(w => w.Minutes > 0);
        var bestTestWeek = wl.OrderByDescending(w => w.Tests).FirstOrDefault(w => w.Tests > 0);

        // زنجیره‌ها
        int longest = 0, cur = 0;
        string? prev = null;
        foreach (var d in days)
        {
            if (d.Minutes == 0) { prev = null; cur = 0; continue; }
            cur = prev != null && Jalali.AddDays(prev, 1) == d.Date ? cur + 1 : 1;
            longest = Math.Max(longest, cur);
            prev = d.Date;
        }

        int current = 0;
        var set = days.Where(d => d.Minutes > 0).Select(d => d.Date).ToHashSet();
        var t = Jalali.TodayJalali();
        if (!set.Contains(t)) t = Jalali.AddDays(t, -1);
        while (set.Contains(t)) { current++; t = Jalali.AddDays(t, -1); }

        return new RecordsResult
        {
            BestDay = bestDay,
            BestTestDay = bestTestDay,
            BestWeek = bestWeek,
            BestTestWeek = bestTestWeek,
            LongestStreak = longest,
            CurrentStreak = current,
            TotalDays = days.Count(d => d.Minutes > 0),
            FirstDay = days.FirstOrDefault()?.Date
        };
    }

    /// <summary>درس‌ها به ترتیب نمایش (بایگانی‌شده‌ها آخر)</summary>
    public Task<List<Subject>> SubjectsAsync()
        => _db.Subjects.AsNoTracking().OrderBy(s => s.Archived).ThenBy(s => s.Sort).ThenBy(s => s.Id).ToListAsync();
}

/// <summary>تنظیمات برنامه (خواندن/نوشتن کلید/مقدارها)</summary>
public class SettingsService
{
    private readonly AppDbContext _db;
    public Dictionary<string, string> Cache { get; private set; } = new();

    public SettingsService(AppDbContext db) => _db = db;

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        var rows = await _db.Settings.AsNoTracking().ToListAsync();
        var result = new Dictionary<string, string>
        {
            ["exam_date"] = Models.AppSettings.ExamDate,
            ["exam_title"] = Models.AppSettings.ExamTitle,
            ["user_name"] = Models.AppSettings.UserName,
        };
        foreach (var r in rows) result[r.Key] = r.Value;
        Cache = result;
        return result;
    }

    public async Task SetAsync(string key, string value)
    {
        if (key is not ("exam_date" or "exam_title" or "user_name"))
            throw new InvalidOperationException("تنظیم ناشناخته");
        if (key == "exam_date" && Jalali.Parse(value) == null)
            throw new FormatException("تاریخ نامعتبر");
        var row = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (row == null) _db.Settings.Add(new Setting { Key = key, Value = value });
        else row.Value = value;
        await _db.SaveChangesAsync();
    }
}
