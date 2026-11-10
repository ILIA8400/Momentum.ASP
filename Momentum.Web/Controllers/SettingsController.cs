using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Models;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class SettingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;
    private readonly SettingsService _settings;
    private readonly BackupService _backup;

    public SettingsController(AppDbContext db, StatsService stats, SettingsService settings, BackupService backup)
    {
        _db = db;
        _stats = stats;
        _settings = settings;
        _backup = backup;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new SettingsVm
        {
            Settings = await _settings.GetAllAsync(),
            Subjects = await _stats.SubjectsAsync(),
            Snapshots = _backup.ListSnapshots(),
            BackupDir = _backup.BackupDir,
        };
        return View(vm);
    }

    /// <summary>پروفایل: نام، عنوان و تاریخ کنکور</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string userName, string examTitle, string examDate)
    {
        try
        {
            await _settings.SetAsync("user_name", (userName ?? "").Trim());
            await _settings.SetAsync("exam_title", examTitle ?? "");
            if (Jalali.Parse(examDate) != null)
                await _settings.SetAsync("exam_date", examDate);
            TempData["Toast"] = "ذخیره شد";
        }
        catch (Exception ex)
        {
            TempData["ToastErr"] = ex.Message;
        }
        return Redirect("/Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubject(string name, string color)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            TempData["ToastErr"] = "نام درس خالی است";
            return Redirect("/Settings");
        }
        var max = await _db.Subjects.MaxAsync(s => (int?)s.Sort) ?? 0;
        _db.Subjects.Add(new Subject { Name = name, Color = color, Sort = max + 1 });
        await _db.SaveChangesAsync();
        TempData["Toast"] = "درس اضافه شد";
        return Redirect("/Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSubject(int id, string name, string color, bool archived)
    {
        var s = await _db.Subjects.FindAsync(id);
        if (s == null) return NotFound();
        s.Name = (name ?? "").Trim();
        s.Color = color;
        s.Archived = archived;
        await _db.SaveChangesAsync();
        TempData["Toast"] = "ذخیره شد";
        return Redirect("/Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubject(int id)
    {
        var has = await _db.Entries.AnyAsync(e => e.SubjectId == id);
        if (has)
        {
            TempData["ToastErr"] = "این درس آیتم ثبت‌شده دارد؛ به جای حذف، بایگانی کن";
            return Redirect("/Settings");
        }
        var s = await _db.Subjects.FindAsync(id);
        if (s != null)
        {
            _db.Subjects.Remove(s);
            await _db.SaveChangesAsync();
        }
        TempData["Toast"] = "حذف شد";
        return Redirect("/Settings");
    }

    /// <summary>بکاپ کامل JSON (دانلود)</summary>
    public async Task<IActionResult> BackupJson()
    {
        var json = await _backup.ExportJsonAsync();
        var name = $"plan-backup-{Jalali.TodayJalali()}.json";
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", name);
    }

    /// <summary>خروجی CSV برای اکسل (دانلود)</summary>
    public async Task<IActionResult> BackupCsv()
    {
        var bytes = await _backup.ExportCsvAsync();
        var name = $"plan-{Jalali.TodayJalali()}.csv";
        return File(bytes, "text/csv; charset=utf-8", name);
    }

    /// <summary>بکاپ فوری در پوشهٔ backups</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Snapshot()
    {
        var file = await _backup.SnapshotAsync("manual");
        TempData["Toast"] = $"بکاپ ساخته شد: {System.IO.Path.GetFileName(file)}";
        return Redirect("/Settings");
    }

    /// <summary>بازیابی از JSON (همه‌چیز جایگزین می‌شود؛ قبلش بکاپ گرفته می‌شود)</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["ToastErr"] = "فایلی انتخاب نشد";
            return Redirect("/Settings");
        }
        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = JsonSerializer.Deserialize<BackupData>(ms.ToArray());
            if (data == null) throw new InvalidOperationException("فایل بکاپ معتبر نیست");

            await _backup.SnapshotAsync("pre-restore");
            await _backup.ImportAsync(data);
            TempData["Toast"] = "بازیابی شد";
        }
        catch (Exception ex)
        {
            TempData["ToastErr"] = "بازیابی ناموفق: " + ex.Message;
        }
        return Redirect("/Settings");
    }

    /// <summary>پاک کردن کل تاریخچه (دو مرحله تأیید؛ قبلش بکاپ گرفته می‌شود)</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wipe(string confirm)
    {
        if (confirm?.Trim() != "پاک کن")
        {
            TempData["ToastErr"] = "عبارت تأیید درست نبود؛ چیزی پاک نشد";
            return Redirect("/Settings");
        }

        await _backup.SnapshotAsync("pre-wipe");

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.ReviewLogs.RemoveRange(_db.ReviewLogs);
        _db.ReviewItems.RemoveRange(_db.ReviewItems);
        _db.Goals.RemoveRange(_db.Goals);
        _db.Entries.RemoveRange(_db.Entries);
        _db.DayNotes.RemoveRange(_db.DayNotes);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Toast"] = "تاریخچه پاک شد — بکاپ قبل از پاک شدن در پوشهٔ backups ذخیره شد";
        return Redirect("/Settings");
    }
}
