using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Models;

namespace Momentum.Web.Controllers;

/// <summary>هدف‌گذاری هفتگی/ماهانه (فراخوانی Ajax از تب هفتگی و ماهانه)</summary>
public class GoalsController : Controller
{
    private readonly AppDbContext _db;

    public GoalsController(AppDbContext db) => _db = db;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(string period, string key, int subjectId, int minutes)
    {
        if (period is not ("week" or "month"))
            return Json(new { error = "بازهٔ نامعتبر" });

        subjectId = Math.Max(0, subjectId); // صفر = هدف کلی
        minutes = Math.Max(0, minutes);

        var goal = await _db.Goals.FirstOrDefaultAsync(g => g.Period == period && g.Key == key && g.SubjectId == subjectId);
        if (minutes == 0)
        {
            if (goal != null) _db.Goals.Remove(goal);
        }
        else if (goal != null)
        {
            goal.Minutes = minutes;
        }
        else
        {
            _db.Goals.Add(new Goal { Period = period, Key = key, SubjectId = subjectId, Minutes = minutes });
        }
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }
}
