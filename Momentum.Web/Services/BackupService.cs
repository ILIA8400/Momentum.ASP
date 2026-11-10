using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Momentum.Web.Data;
using Momentum.Web.Models;

namespace Momentum.Web.Services;

// ---------- مدل‌های JSON بکاپ (دقیقاً هم‌فرمت نسخهٔ اصلی Node.js) ----------
#pragma warning disable IDE1006 // نام‌گذاری snake_case برای سازگاری با فرمت فایل

public class BackupData
{
    [JsonPropertyName("version")] public int Version { get; set; } = 3;
    [JsonPropertyName("exported_at")] public string ExportedAt { get; set; } = "";
    [JsonPropertyName("exported_jalali")] public string ExportedJalali { get; set; } = "";
    [JsonPropertyName("subjects")] public List<BackupSubject> Subjects { get; set; } = new();
    [JsonPropertyName("entries")] public List<BackupEntry> Entries { get; set; } = new();
    [JsonPropertyName("goals")] public List<BackupGoal> Goals { get; set; } = new();
    [JsonPropertyName("day_notes")] public List<BackupDayNote> DayNotes { get; set; } = new();
    [JsonPropertyName("settings")] public List<BackupSetting> Settings { get; set; } = new();
    [JsonPropertyName("review_items")] public List<BackupReviewItem> ReviewItems { get; set; } = new();
    [JsonPropertyName("review_logs")] public List<BackupReviewLog> ReviewLogs { get; set; } = new();
}

public class BackupSubject
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "#3b82f6";
    [JsonPropertyName("sort")] public int Sort { get; set; }
    [JsonPropertyName("archived")] public int Archived { get; set; }
}

public class BackupEntry
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("subject_id")] public int SubjectId { get; set; }
    [JsonPropertyName("minutes")] public int Minutes { get; set; }
    [JsonPropertyName("note")] public string Note { get; set; } = "";
    [JsonPropertyName("done")] public int Done { get; set; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("tests")] public int Tests { get; set; }
    [JsonPropertyName("start_time")] public string StartTime { get; set; } = "";
    [JsonPropertyName("end_time")] public string EndTime { get; set; } = "";
    [JsonPropertyName("study_min")] public int StudyMin { get; set; }
    [JsonPropertyName("review_min")] public int ReviewMin { get; set; }
    [JsonPropertyName("test_min")] public int TestMin { get; set; }
    [JsonPropertyName("correct")] public int Correct { get; set; }
    [JsonPropertyName("wrong")] public int Wrong { get; set; }
}

public class BackupGoal
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("period")] public string Period { get; set; } = "";
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("subject_id")] public int SubjectId { get; set; }
    [JsonPropertyName("minutes")] public int Minutes { get; set; }
}

public class BackupDayNote
{
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
}

public class BackupSetting
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("value")] public string Value { get; set; } = "";
}

public class BackupReviewItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("subject_id")] public int SubjectId { get; set; }
    [JsonPropertyName("topic")] public string Topic { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("fsrs_card")] public string FsrsCard { get; set; } = "";
    [JsonPropertyName("due_at")] public string DueAt { get; set; } = "";
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
}

public class BackupReviewLog
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("review_item_id")] public int ReviewItemId { get; set; }
    [JsonPropertyName("reviewed_at")] public string ReviewedAt { get; set; } = "";
    [JsonPropertyName("rating")] public int Rating { get; set; }
    [JsonPropertyName("previous_due_at")] public string? PreviousDueAt { get; set; }
    [JsonPropertyName("next_due_at")] public string? NextDueAt { get; set; }
    [JsonPropertyName("scheduled_days")] public double? ScheduledDays { get; set; }
    [JsonPropertyName("state")] public int? State { get; set; }
    [JsonPropertyName("stability")] public double? Stability { get; set; }
    [JsonPropertyName("difficulty")] public double? Difficulty { get; set; }
}
#pragma warning restore IDE1006

/// <summary>
/// بکاپ و بازیابی — فرمت JSON کاملاً سازگار با نسخهٔ اصلی (version 3)،
/// خروجی CSV برای اکسل و اسنپ‌شات خودکار دوره‌ای.
/// </summary>
public class BackupService
{
    public const int Keep = 30;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public BackupService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string BackupDir => Path.Combine(_env.ContentRootPath, "backups");

    // ---------- خروجی ----------
    public async Task<BackupData> ExportAsync()
    {
        var now = DateTime.UtcNow;
        return new BackupData
        {
            Version = 3,
            ExportedAt = Iso(now),
            ExportedJalali = Jalali.TodayJalali(),
            Subjects = await _db.Subjects.AsNoTracking()
                .Select(s => new BackupSubject { Id = s.Id, Name = s.Name, Color = s.Color, Sort = s.Sort, Archived = s.Archived ? 1 : 0 })
                .ToListAsync(),
            Entries = await _db.Entries.AsNoTracking().OrderBy(e => e.Date).ThenBy(e => e.Id)
                .Select(e => new BackupEntry
                {
                    Id = e.Id, Date = e.Date, SubjectId = e.SubjectId, Minutes = e.Minutes,
                    Note = e.Note, Done = e.Done ? 1 : 0, CreatedAt = Iso(e.CreatedAt),
                    Tests = e.Tests, StartTime = e.StartTime, EndTime = e.EndTime,
                    StudyMin = e.StudyMin, ReviewMin = e.ReviewMin, TestMin = e.TestMin,
                    Correct = e.Correct, Wrong = e.Wrong
                })
                .ToListAsync(),
            Goals = await _db.Goals.AsNoTracking()
                .Select(g => new BackupGoal { Id = g.Id, Period = g.Period, Key = g.Key, SubjectId = g.SubjectId, Minutes = g.Minutes })
                .ToListAsync(),
            DayNotes = await _db.DayNotes.AsNoTracking().OrderBy(n => n.Date)
                .Select(n => new BackupDayNote { Date = n.Date, Text = n.Text, UpdatedAt = Iso(n.UpdatedAt) })
                .ToListAsync(),
            Settings = await _db.Settings.AsNoTracking()
                .Select(s => new BackupSetting { Key = s.Key, Value = s.Value })
                .ToListAsync(),
            ReviewItems = await _db.ReviewItems.AsNoTracking().OrderBy(r => r.Id)
                .Select(r => new BackupReviewItem
                {
                    Id = r.Id, SubjectId = r.SubjectId, Topic = r.Topic, Description = r.Description,
                    FsrsCard = r.FsrsCard, DueAt = Iso(r.DueAt), CreatedAt = Iso(r.CreatedAt), UpdatedAt = Iso(r.UpdatedAt)
                })
                .ToListAsync(),
            ReviewLogs = await _db.ReviewLogs.AsNoTracking().OrderBy(l => l.Id)
                .Select(l => new BackupReviewLog
                {
                    Id = l.Id, ReviewItemId = l.ReviewItemId, ReviewedAt = Iso(l.ReviewedAt), Rating = l.Rating,
                    PreviousDueAt = l.PreviousDueAt, NextDueAt = l.NextDueAt, ScheduledDays = l.ScheduledDays,
                    State = l.State, Stability = l.Stability, Difficulty = l.Difficulty
                })
                .ToListAsync(),
        };
    }

    public async Task<string> ExportJsonAsync()
        => JsonSerializer.Serialize(await ExportAsync(), JsonOpts);

    /// <summary>خروجی CSV همهٔ آیتم‌ها (همان ستون‌های نسخهٔ اصلی)</summary>
    public async Task<byte[]> ExportCsvAsync()
    {
        var entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
            .OrderBy(e => e.Date).ThenBy(e => e.Id).ToListAsync();

        string[] head = { "تاریخ", "روز هفته", "درس", "شروع", "پایان", "مدت کل (دقیقه)", "خواندن", "مرور", "تست (دقیقه)", "تعداد تست", "درست", "غلط", "درصد", "انجام شد", "شرح" };
        static string Cell(object? v)
        {
            var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        var lines = new List<string> { string.Join(",", head.Select(Cell)) };
        foreach (var e in entries)
        {
            var percent = Helpers.Fa.PercentOf(e.Correct, e.Wrong, e.Tests);
            lines.Add(string.Join(",", new object?[]
            {
                e.Date, Jalali.Weekdays[Jalali.Weekday(e.Date)], e.Subject!.Name, e.StartTime, e.EndTime,
                e.Minutes, e.StudyMin, e.ReviewMin, e.TestMin, e.Tests, e.Correct, e.Wrong,
                percent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                e.Done ? "بله" : "خیر", e.Note
            }.Select(Cell)));
        }
        // BOM + CRLF برای سازگاری با اکسل فارسی
        return Encoding.UTF8.GetBytes("\uFEFF" + string.Join("\r\n", lines));
    }

    /// <summary>اسنپ‌شات JSON در پوشهٔ بکاپ (نگه‌داشت ۳۰ فایل آخر)</summary>
    public async Task<string> SnapshotAsync(string label = "auto")
    {
        Directory.CreateDirectory(BackupDir);
        var file = Path.Combine(BackupDir, $"momentum-{label}-{Stamp()}.json");
        await File.WriteAllTextAsync(file, await ExportJsonAsync(), Encoding.UTF8);
        Prune();
        return file;
    }

    public List<(string Name, long Size, DateTime Time)> ListSnapshots()
    {
        if (!Directory.Exists(BackupDir)) return new();
        return Directory.GetFiles(BackupDir, "*.json")
            .Select(f => (Name: Path.GetFileName(f), Size: new FileInfo(f).Length, Time: File.GetLastWriteTime(f)))
            .OrderByDescending(x => x.Time)
            .ToList();
    }

    private void Prune()
    {
        var files = Directory.GetFiles(BackupDir, "*.json")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Skip(Keep);
        foreach (var f in files) { try { f.Delete(); } catch { /* بی‌اهمیت */ } }
    }

    private static string Stamp()
    {
        var d = DateTime.Now;
        return $"{d:yyyyMMdd}-{d:HHmmss}-{d.Millisecond:D3}";
    }

    private static string Iso(DateTime d) => new DateTime(d.Ticks, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    // ---------- بازیابی ----------
    public async Task ImportAsync(BackupData data)
    {
        if (data.Version is < 1 or > 3 || data.Subjects == null || data.Entries == null)
            throw new InvalidOperationException("فایل بکاپ معتبر نیست");

        await using var tx = await _db.Database.BeginTransactionAsync();

        // پاک‌سازی (به ترتیب وابستگی‌ها)
        _db.ReviewLogs.RemoveRange(_db.ReviewLogs);
        _db.ReviewItems.RemoveRange(_db.ReviewItems);
        _db.Goals.RemoveRange(_db.Goals);
        _db.Entries.RemoveRange(_db.Entries);
        _db.Subjects.RemoveRange(_db.Subjects);
        _db.DayNotes.RemoveRange(_db.DayNotes);
        _db.Settings.RemoveRange(_db.Settings);
        await _db.SaveChangesAsync();

        // درج با حفظ شناسه‌ها (IDENTITY_INSERT)
        await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Subjects] ON");
        foreach (var x in data.Subjects)
            _db.Subjects.Add(new Subject { Id = x.Id, Name = x.Name, Color = x.Color, Sort = x.Sort, Archived = x.Archived != 0 });
        await _db.SaveChangesAsync();
        await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Subjects] OFF");

        await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Entries] ON");
        foreach (var x in data.Entries)
            _db.Entries.Add(new Entry
            {
                Id = x.Id, Date = x.Date, SubjectId = x.SubjectId, Minutes = x.Minutes,
                Note = x.Note ?? "", Done = x.Done != 0,
                CreatedAt = ParseIso(x.CreatedAt) ?? DateTime.UtcNow,
                Tests = x.Tests, StartTime = x.StartTime ?? "", EndTime = x.EndTime ?? "",
                StudyMin = x.StudyMin, ReviewMin = x.ReviewMin, TestMin = x.TestMin,
                Correct = x.Correct, Wrong = x.Wrong
            });
        await _db.SaveChangesAsync();
        await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Entries] OFF");

        if (data.Goals?.Count > 0)
        {
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Goals] ON");
            foreach (var x in data.Goals)
                _db.Goals.Add(new Goal { Id = x.Id, Period = x.Period, Key = x.Key, SubjectId = x.SubjectId, Minutes = x.Minutes });
            await _db.SaveChangesAsync();
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Goals] OFF");
        }

        foreach (var x in data.DayNotes ?? new())
            _db.DayNotes.Add(new DayNote { Date = x.Date, Text = x.Text ?? "", UpdatedAt = ParseIso(x.UpdatedAt) ?? DateTime.UtcNow });

        foreach (var x in data.Settings ?? new())
            _db.Settings.Add(new Setting { Key = x.Key, Value = x.Value });

        if (data.ReviewItems?.Count > 0)
        {
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [ReviewItems] ON");
            foreach (var x in data.ReviewItems)
                _db.ReviewItems.Add(new ReviewItem
                {
                    Id = x.Id, SubjectId = x.SubjectId, Topic = x.Topic, Description = x.Description ?? "",
                    FsrsCard = x.FsrsCard, DueAt = ParseIso(x.DueAt) ?? DateTime.UtcNow,
                    CreatedAt = ParseIso(x.CreatedAt) ?? DateTime.UtcNow, UpdatedAt = ParseIso(x.UpdatedAt) ?? DateTime.UtcNow
                });
            await _db.SaveChangesAsync();
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [ReviewItems] OFF");
        }

        if (data.ReviewLogs?.Count > 0)
        {
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [ReviewLogs] ON");
            foreach (var x in data.ReviewLogs)
                _db.ReviewLogs.Add(new ReviewLog
                {
                    Id = x.Id, ReviewItemId = x.ReviewItemId, ReviewedAt = ParseIso(x.ReviewedAt) ?? DateTime.UtcNow,
                    Rating = x.Rating, PreviousDueAt = x.PreviousDueAt, NextDueAt = x.NextDueAt,
                    ScheduledDays = x.ScheduledDays, State = x.State, Stability = x.Stability, Difficulty = x.Difficulty
                });
            await _db.SaveChangesAsync();
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [ReviewLogs] OFF");
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static DateTime? ParseIso(string? s)
        => DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
               System.Globalization.DateTimeStyles.RoundtripKind, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : null;
}

/// <summary>بکاپ خودکار: هنگام اجرا + هر ۶ ساعت (مثل نسخهٔ اصلی)</summary>
public class BackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupHostedService> _logger;

    public BackupHostedService(IServiceScopeFactory scopeFactory, ILogger<BackupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Run("start", stoppingToken);
        try
        {
            var period = TimeSpan.FromHours(6);
            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(period, stoppingToken);
        }
        catch (OperationCanceledException) { /* خاموشی نرمال */ }
        await Run("exit", stoppingToken);
    }

    private async Task Run(string label, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backup = scope.ServiceProvider.GetRequiredService<BackupService>();
            var file = await backup.SnapshotAsync(label);
            _logger.LogInformation("بکاپ {Label} ساخته شد: {File}", label, file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ساخت بکاپ {Label} ناموفق بود", label);
        }
    }
}
