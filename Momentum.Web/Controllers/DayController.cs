using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Helpers;
using Momentum.Web.Models;
using Momentum.Web.Services;
using Momentum.Web.ViewModels;

namespace Momentum.Web.Controllers;

public class DayController : Controller
{
    private readonly AppDbContext _db;
    private readonly StatsService _stats;

    public DayController(AppDbContext db, StatsService stats)
    {
        _db = db;
        _stats = stats;
    }

    public async Task<IActionResult> Index(string? date, int? edit)
    {
        var d = Jalali.Parse(date) != null ? date! : Jalali.TodayJalali();

        var entries = await _db.Entries.AsNoTracking().Include(e => e.Subject)
            .Where(e => e.Date == d)
            .OrderBy(e => e.StartTime == "" ? 1 : 0).ThenBy(e => e.StartTime).ThenBy(e => e.Id)
            .ToListAsync();

        var vm = new DayVm
        {
            Date = d,
            Entries = entries,
            Subjects = await _stats.SubjectsAsync(),
            Edit = edit is > 0 ? entries.FirstOrDefault(e => e.Id == edit) : null,
            Note = await _db.DayNotes.AsNoTracking().Where(n => n.Date == d).Select(n => n.Text).FirstOrDefaultAsync()
        };
        return View(vm);
    }

    /// <summary>افزودن یا ویرایش آیتم مطالعه (اعتبارسنجی مثل نسخهٔ اصلی)</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EntryInput input)
    {
        var date = input.Date;
        if (Jalali.Parse(date) == null)
        {
            TempData["ToastErr"] = "تاریخ نامعتبر است";
            return Redirect($"/Day?date={date}");
        }

        var subject = await _db.Subjects.FindAsync(input.SubjectId);
        if (subject == null)
        {
            TempData["ToastErr"] = "درس انتخاب نشده";
            return Redirect($"/Day?date={date}");
        }

        var startTime = Fa.IsValidTime(input.StartTime) ? input.StartTime! : "";
        var endTime = Fa.IsValidTime(input.EndTime) ? input.EndTime! : "";
        int study = Math.Max(0, input.StudyMin ?? 0), review = Math.Max(0, input.ReviewMin ?? 0), test = Math.Max(0, input.TestMin ?? 0);
        int tests = Math.Max(0, input.Tests ?? 0), correct = Math.Max(0, input.Correct ?? 0), wrong = Math.Max(0, input.Wrong ?? 0);
        var minutes = Math.Max(0, input.Minutes ?? 0);
        if (minutes == 0)
        {
            var parts = study + review + test;
            if (parts > 0) minutes = parts;
            else if (startTime != "" && endTime != "") minutes = Fa.DiffMinutes(startTime, endTime);
        }

        if (minutes <= 0)
        {
            TempData["ToastErr"] = "مدت مطالعه را وارد کن";
            return Redirect(input.Id is > 0 ? $"/Day?date={date}&edit={input.Id}" : $"/Day?date={date}");
        }
        if (minutes > 24 * 60)
        {
            TempData["ToastErr"] = "مدت زمان نامعتبر است";
            return Redirect(input.Id is > 0 ? $"/Day?date={date}&edit={input.Id}" : $"/Day?date={date}");
        }
        if (correct + wrong > tests)
        {
            TempData["ToastErr"] = "درست + غلط نمی‌تواند از تعداد کل تست بیشتر باشد";
            return Redirect(input.Id is > 0 ? $"/Day?date={date}&edit={input.Id}" : $"/Day?date={date}");
        }

        if (input.Id is > 0)
        {
            var e = await _db.Entries.FirstOrDefaultAsync(x => x.Id == input.Id);
            if (e == null) return NotFound();
            e.Date = date;
            e.SubjectId = input.SubjectId;
            e.Minutes = minutes;
            e.Note = (input.Note ?? "").Trim();
            e.Done = input.Done;
            e.StartTime = startTime;
            e.EndTime = endTime;
            e.StudyMin = study;
            e.ReviewMin = review;
            e.TestMin = test;
            e.Tests = tests;
            e.Correct = correct;
            e.Wrong = wrong;
            TempData["Toast"] = "ذخیره شد";
        }
        else
        {
            _db.Entries.Add(new Entry
            {
                Date = date,
                SubjectId = input.SubjectId,
                Minutes = minutes,
                Note = (input.Note ?? "").Trim(),
                Done = input.Done,
                CreatedAt = DateTime.UtcNow,
                StartTime = startTime,
                EndTime = endTime,
                StudyMin = study,
                ReviewMin = review,
                TestMin = test,
                Tests = tests,
                Correct = correct,
                Wrong = wrong
            });
            TempData["Toast"] = "اضافه شد";
        }

        await _db.SaveChangesAsync();
        return Redirect($"/Day?date={date}");
    }

    /// <summary>تیک «انجام شد» (فراخوانی Ajax)</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool done)
    {
        var e = await _db.Entries.FindAsync(id);
        if (e == null) return Json(new { error = "یافت نشد" });
        e.Done = done;
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string date)
    {
        var e = await _db.Entries.FindAsync(id);
        if (e != null)
        {
            _db.Entries.Remove(e);
            await _db.SaveChangesAsync();
            TempData["Toast"] = "حذف شد";
        }
        return Redirect($"/Day?date={date}");
    }

    /// <summary>ذخیرهٔ خودکار یادداشت روز (فراخوانی Ajax)</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNote(string date, string? text)
    {
        if (Jalali.Parse(date) == null) return Json(new { error = "تاریخ نامعتبر" });
        text ??= "";
        var note = await _db.DayNotes.FirstOrDefaultAsync(n => n.Date == date);
        if (string.IsNullOrWhiteSpace(text))
        {
            if (note != null) _db.DayNotes.Remove(note);
        }
        else if (note != null)
        {
            note.Text = text;
            note.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DayNotes.Add(new DayNote { Date = date, Text = text, UpdatedAt = DateTime.UtcNow });
        }
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }
}
