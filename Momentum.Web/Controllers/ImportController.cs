using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Models;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class ImportController : Controller
{
    private readonly AppDbContext _db;
    private readonly ExcelImportService _excel;
    private readonly BackupService _backup;

    /// <summary>حافظهٔ موقت برای نتیجهٔ تحلیل فایل (کلید تصادفی که در فرم مخفی می‌ماند)</summary>
    private static readonly ConcurrentDictionary<string, (ImportAnalyzeResult Result, string FileName, DateTime Created)> Cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public ImportController(AppDbContext db, ExcelImportService excel, BackupService backup)
    {
        _db = db;
        _excel = excel;
        _backup = backup;
    }

    public async Task<IActionResult> Index(string? preview)
    {
        var vm = new ImportVm();
        if (!string.IsNullOrEmpty(preview) && Cache.TryGetValue(preview, out var hit) && DateTime.UtcNow - hit.Created < CacheTtl)
        {
            vm.PreviewKey = preview;
            vm.Result = hit.Result;
            vm.FileName = hit.FileName;
        }
        return View(vm);
    }

    /// <summary>دانلود قالب خالی اکسل</summary>
    public async Task<IActionResult> Template()
    {
        var subjects = await _db.Subjects.AsNoTracking().OrderBy(s => s.Sort).ToListAsync();
        var bytes = _excel.BuildTemplate(subjects);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "momentum-template.xlsx");
    }

    /// <summary>دانلود نمونهٔ پرشده (۳ ردیف)</summary>
    public async Task<IActionResult> Sample()
    {
        var subjects = await _db.Subjects.AsNoTracking().OrderBy(s => s.Sort).ToListAsync();
        var bytes = _excel.BuildSample(subjects);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "momentum-sample.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Analyze(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["ToastErr"] = "فایلی ارسال نشد";
            return Redirect("/Import");
        }

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var subjects = await _db.Subjects.AsNoTracking().ToListAsync();
            var existing = await _db.Entries.AsNoTracking().ToListAsync();

            var result = _excel.Analyze(ms.ToArray(), file.FileName, subjects, existing);
            var key = Guid.NewGuid().ToString("N")[..16];
            Cache[key] = (result, file.FileName, DateTime.UtcNow);

            // پاک‌سازی کلیدهای منقضی‌شده
            foreach (var k in Cache.Keys)
                if (Cache.TryGetValue(k, out var old) && DateTime.UtcNow - old.Created > CacheTtl)
                    Cache.TryRemove(k, out _);

            return Redirect($"/Import?preview={key}");
        }
        catch (Exception ex)
        {
            TempData["ToastErr"] = ex.Message;
            return Redirect("/Import");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Commit(string key, int[]? selected)
    {
        if (string.IsNullOrEmpty(key) || !Cache.TryGetValue(key, out var hit) || DateTime.UtcNow - hit.Created > CacheTtl)
        {
            TempData["ToastErr"] = "پیش‌نمایش منقضی شده؛ دوباره فایل را بررسی کن";
            return Redirect("/Import");
        }

        var rows = hit.Result.Rows
            .Where(r => r.Ok && (selected?.Contains(r.Row) ?? false))
            .ToList();

        if (rows.Count == 0)
        {
            TempData["ToastErr"] = "ردیفی برای افزودن نیست";
            return Redirect($"/Import?preview={key}");
        }

        // قبل از افزودن، بکاپ خودکار (مثل نسخهٔ اصلی)
        await _backup.SnapshotAsync("pre-import");

        var added = ExcelImportService.AddEntries(_db, rows);

        Cache.TryRemove(key, out _);
        TempData["Toast"] = $"{Helpers.Fa.D(added.Count)} ردیف اضافه شد";
        return Redirect("/");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cancel(string key)
    {
        if (!string.IsNullOrEmpty(key)) Cache.TryRemove(key, out _);
        return Redirect("/Import");
    }
}
