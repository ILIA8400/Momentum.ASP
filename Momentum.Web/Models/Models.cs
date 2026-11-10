using System.ComponentModel.DataAnnotations;
using Momentum.Web.Helpers;

namespace Momentum.Web.Models;

/// <summary>درس (مثل گسسته، مدار و …)</summary>
public class Subject
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public string Color { get; set; } = "#3b82f6";

    public int Sort { get; set; }

    public bool Archived { get; set; }
}

/// <summary>یک جلسه مطالعه در یک روز (تاریخ شمسی به شکل «1405-06-27»)</summary>
public class Entry
{
    public int Id { get; set; }

    /// <summary>تاریخ شمسی «YYYY-MM-DD»</summary>
    [Required]
    public string Date { get; set; } = "";

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    /// <summary>مدت کل (دقیقه)</summary>
    public int Minutes { get; set; }

    public string Note { get; set; } = "";

    public bool Done { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";

    public int StudyMin { get; set; }
    public int ReviewMin { get; set; }
    public int TestMin { get; set; }

    public int Tests { get; set; }
    public int Correct { get; set; }
    public int Wrong { get; set; }

    /// <summary>درصد کنکوری با نمره منفی: (۳×درست − غلط) ÷ (۳×کل)</summary>
    public double? Percent(int? correct = null, int? wrong = null, int? tests = null)
        => Fa.PercentOf(correct ?? Correct, wrong ?? Wrong, tests ?? Tests);
}

/// <summary>هدف مطالعه برای یک بازه (هفته یا ماه)؛ SubjectId صفر یعنی هدف کلی</summary>
public class Goal
{
    public int Id { get; set; }

    /// <summary>«week» یا «month»</summary>
    public string Period { get; set; } = "";

    /// <summary>شناسهٔ بازه؛ برای هفته: تاریخ شنبه، برای ماه: «1405-06»</summary>
    public string Key { get; set; } = "";

    public int SubjectId { get; set; }

    public int Minutes { get; set; }
}

/// <summary>یادداشت روزانه</summary>
public class DayNote
{
    /// <summary>تاریخ شمسی «YYYY-MM-DD» (کلید اصلی)</summary>
    public string Date { get; set; } = "";

    public string Text { get; set; } = "";

    public DateTime UpdatedAt { get; set; }
}

/// <summary>تنظیمات کلید/مقدار</summary>
public class Setting
{
    public string Key { get; set; } = "";

    public string Value { get; set; } = "";
}

/// <summary>مبحث قابل مرور با زمان‌بندی FSRS</summary>
public class ReviewItem
{
    public int Id { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public string Topic { get; set; } = "";

    public string Description { get; set; } = "";

    /// <summary>کارت FSRS به صورت JSON (سازگار با ts-fsrs)</summary>
    public string FsrsCard { get; set; } = "";

    /// <summary>موعد مرور بعدی (UTC)</summary>
    public DateTime DueAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>تاریخچهٔ مرورهای یک مبحث</summary>
public class ReviewLog
{
    public int Id { get; set; }

    public int ReviewItemId { get; set; }
    public ReviewItem? ReviewItem { get; set; }

    public DateTime ReviewedAt { get; set; }

    /// <summary>۱=دوباره، ۲=سخت، ۳=خوب، ۴=خیلی راحت</summary>
    public int Rating { get; set; }

    public string? PreviousDueAt { get; set; }
    public string? NextDueAt { get; set; }

    public double? ScheduledDays { get; set; }
    public int? State { get; set; }
    public double? Stability { get; set; }
    public double? Difficulty { get; set; }
}

/// <summary>تنظیمات پیش‌فرض برنامه</summary>
public static class AppSettings
{
    public const string ExamDate = "1406-02-16";
    public const string ExamTitle = "کنکور ارشد کامپیوتر";
    public const string UserName = "";

    public static readonly (string Name, string Color)[] DefaultSubjects =
    {
        ("گسسته", "#f97316"),
        ("نظریه زبان", "#f59e0b"),
        ("مدار", "#84cc16"),
        ("معماری", "#22c55e"),
        ("هوش", "#14b8a6"),
        ("سیستم عامل", "#06b6d4"),
        ("ساختمان داده", "#3b82f6"),
        ("الگوریتم", "#6366f1"),
        ("آمار", "#8b5cf6"),
        ("جبر خطی", "#d946ef"),
        ("زبان", "#ec4899"),
        ("مبانی", "#78716c"),
    };
}
