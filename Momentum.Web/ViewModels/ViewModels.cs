using Momentum.Web.Models;
using Momentum.Web.Services;

namespace Momentum.Web.ViewModels;

/// <summary>مدل صفحهٔ داشبورد</summary>
public class DashboardVm
{
    public string Today { get; set; } = "";
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<Entry> Entries { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public StatsResult Yesterday { get; set; } = new();
    public StatsResult Last7 { get; set; } = new();
    public StatsResult WeekStats { get; set; } = new();
    public StatsResult WeekStatsDone { get; set; } = new();
    public List<Goal> WeekGoals { get; set; } = new();
    public RecordsResult Records { get; set; } = new();
    public string TodayNote { get; set; } = "";
    public StatsResult HeatStats { get; set; } = new();
    public string HeatFrom { get; set; } = "";
    public List<ReviewItem> DueReviews { get; set; } = new();
    public string WeekStart { get; set; } = "";
    public string WeekEnd { get; set; } = "";

    public int Total => Entries.Sum(e => e.Minutes);
    public int Tests => Entries.Sum(e => e.Tests);
    public double? Percent
    {
        get
        {
            var scored = Entries.Where(e => e.Correct + e.Wrong > 0).ToList();
            return scored.Count == 0 ? null
                : Helpers.Fa.PercentOf(scored.Sum(e => e.Correct), scored.Sum(e => e.Wrong), scored.Sum(e => e.Tests));
        }
    }
    public List<Entry> Done => Entries.Where(e => e.Done).ToList();
    public List<Entry> Pending => Entries.Where(e => !e.Done).ToList();

    public int ExamDays => Jalali.DiffDays(Today, Settings.GetValueOrDefault("exam_date", "1406-02-16"));
    public string ExamDate => Settings.GetValueOrDefault("exam_date", "1406-02-16");
    public string ExamTitle => Settings.GetValueOrDefault("exam_title", "کنکور ارشد کامپیوتر");
    public string UserName => Settings.GetValueOrDefault("user_name", "");

    public int Span
    {
        get
        {
            var first = Records.FirstDay ?? Today;
            return Math.Max(1, Jalali.DiffDays(first, ExamDate));
        }
    }
    public int Elapsed => Math.Min(Span, Math.Max(0, Jalali.DiffDays(Records.FirstDay ?? Today, Today)));

    public int WeekGoal => WeekGoals.FirstOrDefault(g => g.SubjectId == 0)?.Minutes ?? 0;
    public SubjectStat? BestSubject => WeekStats.BySubject.FirstOrDefault();
}

/// <summary>مدل تب روزانه</summary>
public class DayVm
{
    public string Date { get; set; } = "";
    public List<Entry> Entries { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public Entry? Edit { get; set; }
    public string? Note { get; set; }
    public int Total => Entries.Sum(e => e.Minutes);
    public int Tests => Entries.Sum(e => e.Tests);
}

/// <summary>فرم افزودن/ویرایش آیتم مطالعه</summary>
public class EntryInput
{
    public int? Id { get; set; }
    public string Date { get; set; } = "";
    public int SubjectId { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int? StudyMin { get; set; }
    public int? ReviewMin { get; set; }
    public int? TestMin { get; set; }
    public int? Minutes { get; set; }
    public int? Tests { get; set; }
    public int? Correct { get; set; }
    public int? Wrong { get; set; }
    public string? Note { get; set; }
    public bool Done { get; set; }
}

/// <summary>مدل تب هفتگی</summary>
public class WeekVm
{
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public List<Entry> Entries { get; set; } = new();
    public StatsResult Stats { get; set; } = new();
    public StatsResult StatsDone { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public List<string> Days => Enumerable.Range(0, 7).Select(i => Jalali.AddDays(Start, i)).ToList();
    public int Total => Entries.Sum(e => e.Minutes);
    public int Tests => Entries.Sum(e => e.Tests);
    public int StudiedDays => Entries.Select(e => e.Date).Distinct().Count();
}

/// <summary>مدل تب ماهانه</summary>
public class MonthVm
{
    public string MonthKey { get; set; } = "";
    public int Jy { get; set; }
    public int Jm { get; set; }
    public string First { get; set; } = "";
    public string Last { get; set; } = "";
    public int Len { get; set; }
    public List<Entry> Entries { get; set; } = new();
    public StatsResult Stats { get; set; } = new();
    public StatsResult StatsDone { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public int Total => Entries.Sum(e => e.Minutes);
    public int Tests => Entries.Sum(e => e.Tests);

    public Dictionary<string, (int Min, int Tests)> ByDay => Entries
        .GroupBy(e => e.Date)
        .ToDictionary(g => g.Key, g => (g.Sum(x => x.Minutes), g.Sum(x => x.Tests)));

    public int Max => Math.Max(60, ByDay.Count == 0 ? 60 : ByDay.Values.Max(x => x.Min));
}

/// <summary>مدل تب آمار</summary>
public class StatsVm
{
    public string Kind { get; set; } = "month";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Mode { get; set; } = "done"; // «done» یا «all»
    public StatsResult Stats { get; set; } = new();
    public RecordsResult Records { get; set; } = new();
    public bool IsAll => Kind == "all";
    public int StudiedDays => Stats.ByDay.Count;
    public double Avg => StudiedDays > 0 ? Stats.Total / (double)StudiedDays : 0;
    public int MaxSub => Math.Max(1, Stats.BySubject.Count == 0 ? 1 : Stats.BySubject.Max(s => s.Minutes));
}

/// <summary>مدل تب مرور</summary>
public class ReviewVm
{
    public List<Subject> Subjects { get; set; } = new();
    public List<ReviewItem> DueItems { get; set; } = new();
    public List<ReviewItem> OverdueItems { get; set; } = new();
    public List<ReviewItem> AllItems { get; set; } = new();
}

/// <summary>مدل جلسهٔ مرور</summary>
public class ReviewSessionVm
{
    public ReviewItem Item { get; set; } = new();
    public bool Reveal { get; set; }
    public Dictionary<string, string> Preview { get; set; } = new(); // دوباره/سخت/خوب/خیلی راحت → متن
    public string SubjectName { get; set; } = "";
    public string SubjectColor { get; set; } = "";
}

/// <summary>مدل تب ورود از اکسل</summary>
public class ImportVm
{
    public string? PreviewKey { get; set; }
    public ImportAnalyzeResult? Result { get; set; }
    public string FileName { get; set; } = "";
}

/// <summary>مدل تب تنظیمات</summary>
public class SettingsVm
{
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public List<(string Name, long Size, DateTime Time)> Snapshots { get; set; } = new();
    public string BackupDir { get; set; } = "";
}

/// <summary>پنل هدف‌گذاری (مشترک بین تب هفتگی و ماهانه)</summary>
public class GoalsPanelVm
{
    public string Period { get; set; } = "week";
    public string Key { get; set; } = "";
    public string Label { get; set; } = "هفته";
    public List<Entry> Entries { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public int DoneTotal { get; set; }

    public int Overall => Goals.FirstOrDefault(g => g.SubjectId == 0)?.Minutes ?? 0;
    public Dictionary<int, int> PerSubjectDone => Entries.Where(e => e.Done)
        .GroupBy(e => e.SubjectId)
        .ToDictionary(g => g.Key, g => g.Sum(e => e.Minutes));
}

/// <summary>مدل All Plan (جدول هفتگی چاپی)</summary>
public class AllPlanVm
{
    public string Range { get; set; } = "all";
    public string FirstWeek { get; set; } = "";
    public string ThisWeek { get; set; } = "";
    public List<string> Weeks { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public Dictionary<(string Week, int SubjectId), WeekAgg> Agg { get; set; } = new();
    public Dictionary<int, WeekAgg> SubTotal { get; set; } = new();
    public string UserName { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public string ExamDate { get; set; } = "";
    public static string[] Ordinals => new[] { "اول", "دوم", "سوم", "چهارم", "پنجم", "ششم", "هفتم", "هشتم", "نهم", "دهم", "یازدهم", "دوازدهم", "سیزدهم", "چهاردهم", "پانزدهم", "شانزدهم", "هفدهم", "هجدهم", "نوزدهم", "بیستم" };
    public string Ordinal(int i) => i < Ordinals.Length ? Ordinals[i] : Helpers.Fa.D(i + 1);
}

public class WeekAgg
{
    public int Minutes { get; set; }
    public int Study { get; set; }
    public int Review { get; set; }
    public int Test { get; set; }
    public int Tests { get; set; }
    public List<string> NotesStudy { get; set; } = new();
    public List<string> NotesReview { get; set; } = new();
    public List<string> NotesTest { get; set; } = new();
    public bool IsEmpty => Minutes == 0 && NotesStudy.Count == 0 && NotesReview.Count == 0 && NotesTest.Count == 0;
}

/// <summary>مدل گزارش تفصیلی هفته</summary>
public class ReportVm
{
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public List<Entry> Entries { get; set; } = new();
    public Dictionary<string, string> Notes { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public StatsResult Stats { get; set; } = new();
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<string> Days => Enumerable.Range(0, 7).Select(i => Jalali.AddDays(Start, i)).ToList();
    public int ExamDays => Jalali.DiffDays(Jalali.TodayJalali(), Settings.GetValueOrDefault("exam_date", "1406-02-16"));
    public int Goal => Goals.FirstOrDefault(g => g.SubjectId == 0)?.Minutes ?? 0;
    public int DoneCount => Entries.Count(e => e.Done);
    public int MaxSub => Math.Max(1, Stats.BySubject.Count == 0 ? 1 : Stats.BySubject.Max(s => s.Minutes));
}

/// <summary>مدل جدول خلاصهٔ هفته</summary>
public class SummaryVm
{
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public StatsResult Stats { get; set; } = new();
    public List<Entry> Entries { get; set; } = new();
    public Dictionary<string, string> Settings { get; set; } = new();
    public int Days => Entries.Select(e => e.Date).Distinct().Count();
    public string UserName => Settings.GetValueOrDefault("user_name", "");
}
