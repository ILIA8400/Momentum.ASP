using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Helpers;
using Momentum.Web.Models;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class ReviewController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;
    private readonly Fsrs _fsrs;

    public ReviewController(AppDbContext db, StatsService stats, Fsrs fsrs)
    {
        _db = db;
        _stats = stats;
        _fsrs = fsrs;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        var vm = new ReviewVm
        {
            Subjects = await _stats.SubjectsAsync(),
            DueItems = await _db.ReviewItems.AsNoTracking().Include(r => r.Subject)
                .Where(r => r.DueAt <= now).OrderBy(r => r.DueAt).ToListAsync(),
            OverdueItems = await _db.ReviewItems.AsNoTracking().Include(r => r.Subject)
                .Where(r => r.DueAt < today).OrderBy(r => r.DueAt).ToListAsync(),
            AllItems = await _db.ReviewItems.AsNoTracking().Include(r => r.Subject)
                .OrderBy(r => r.DueAt).ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int subjectId, string topic, string? description)
    {
        topic = topic.Trim();
        if (string.IsNullOrEmpty(topic))
        {
            TempData["ToastErr"] = "مبحث الزامی است";
            return Redirect("/Review");
        }
        if (!await _db.Subjects.AnyAsync(s => s.Id == subjectId))
        {
            TempData["ToastErr"] = "درس انتخاب نشده";
            return Redirect("/Review");
        }

        var now = DateTime.UtcNow;
        var card = Fsrs.CreateEmptyCard(now);
        _db.ReviewItems.Add(new ReviewItem
        {
            SubjectId = subjectId,
            Topic = topic,
            Description = (description ?? "").Trim(),
            FsrsCard = FsrsJson.Stringify(card),
            DueAt = card.Due,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();
        TempData["Toast"] = "مبحث اضافه شد";
        return Redirect("/Review");
    }

    /// <summary>جلسهٔ مرور: یادآوری از حافظه ← نمایش توضیحات ← امتیازدهی</summary>
    public async Task<IActionResult> Session(int id, bool reveal = false)
    {
        var item = await _db.ReviewItems.AsNoTracking().Include(r => r.Subject).FirstOrDefaultAsync(r => r.Id == id);
        if (item == null) return NotFound();

        var card = FsrsJson.Parse(item.FsrsCard);
        var now = DateTime.UtcNow;
        var preview = _fsrs.Preview(card, now);

        string Fmt(FsrsResult r)
        {
            var diff = (int)Math.Round((r.Card.Due - now).TotalDays);
            if (diff <= 0) return "امروز";
            if (diff == 1) return "فردا";
            return $"{Fa.D(diff)} روز دیگر";
        }

        var vm = new ReviewSessionVm
        {
            Item = item,
            Reveal = reveal,
            SubjectName = item.Subject!.Name,
            SubjectColor = item.Subject.Color,
            Preview = new Dictionary<string, string>
            {
                ["again"] = Fmt(preview[FsrsRating.Again]),
                ["hard"] = Fmt(preview[FsrsRating.Hard]),
                ["good"] = Fmt(preview[FsrsRating.Good]),
                ["easy"] = Fmt(preview[FsrsRating.Easy]),
            }
        };
        return View(vm);
    }

    /// <summary>ثبت امتیاز مرور: کارت FSRS به‌روز می‌شود و تاریخچه ثبت می‌گردد</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(int id, int rating)
    {
        if (rating is < 1 or > 4)
        {
            TempData["ToastErr"] = "امتیاز نامعتبر";
            return Redirect("/Review");
        }

        var item = await _db.ReviewItems.Include(r => r.Subject).FirstOrDefaultAsync(r => r.Id == id);
        if (item == null) return NotFound();

        var card = FsrsJson.Parse(item.FsrsCard);
        var now = DateTime.UtcNow;
        var previousDue = item.DueAt;

        var result = _fsrs.Next(card, now, rating);
        var nextDue = result.Card.Due;

        item.FsrsCard = FsrsJson.Stringify(result.Card);
        item.DueAt = nextDue;
        item.UpdatedAt = now;

        _db.ReviewLogs.Add(new ReviewLog
        {
            ReviewItemId = id,
            ReviewedAt = now,
            Rating = rating,
            PreviousDueAt = Iso(previousDue),
            NextDueAt = Iso(nextDue),
            ScheduledDays = result.Log.ScheduledDays,
            State = result.Card.State,
            Stability = result.Card.Stability,
            Difficulty = result.Card.Difficulty
        });
        await _db.SaveChangesAsync();

        var diff = (int)Math.Round((nextDue - now).TotalDays);
        var when = diff <= 0 ? "امروز" : diff == 1 ? "فردا" : $"{Fa.D(diff)} روز دیگر";
        TempData["Toast"] = $"مرور ثبت شد ✓ — مرور بعدی: {when}";
        return Redirect("/Review");
    }

    private static string Iso(DateTime d)
        => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.ReviewItems.AsNoTracking().Include(r => r.Subject).FirstOrDefaultAsync(r => r.Id == id);
        if (item == null) return NotFound();
        ViewBag.Subjects = await _stats.SubjectsAsync();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSave(int id, int subjectId, string topic, string? description)
    {
        var item = await _db.ReviewItems.FirstOrDefaultAsync(r => r.Id == id);
        if (item == null) return NotFound();

        item.SubjectId = subjectId;
        item.Topic = topic.Trim();
        item.Description = (description ?? "").Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Toast"] = "ذخیره شد";
        return Redirect("/Review");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.ReviewItems.FirstOrDefaultAsync(r => r.Id == id);
        if (item != null)
        {
            _db.ReviewItems.Remove(item); // لاگ‌ها با cascade حذف می‌شوند
            await _db.SaveChangesAsync();
        }
        TempData["Toast"] = "حذف شد";
        return Redirect("/Review");
    }
}
