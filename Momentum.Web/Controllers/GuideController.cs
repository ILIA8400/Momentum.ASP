using Microsoft.AspNetCore.Mvc;

namespace Momentum.Web.Controllers;

/// <summary>راهنمای استفاده (صفحهٔ مستقل مثل نسخهٔ اصلی)</summary>
public class GuideController : Controller
{
    public IActionResult Index() => View();
}
