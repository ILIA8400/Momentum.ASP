using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- سرویس‌ها ----------
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// خروجی متن فارسی به‌صورت UTF-8 خام (مثل نسخهٔ اصلی) به‌جای HTML entity — حجم صفحات ~۹ برابر کمتر
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddSingleton<ExcelImportService>();
builder.Services.AddSingleton<Fsrs>();

// بکاپ خودکار: هنگام اجرا + هر ۶ ساعت
builder.Services.AddHostedService<BackupHostedService>();

var app = builder.Build();

// ---------- دیتابیس: ساخت در اولین اجرا + داده‌های اولیه ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        DbInitializer.Initialize(db);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex,
            "اتصال به SQL Server برقرار نشد. ConnectionString را در appsettings.json بررسی کن " +
            "(راهنما: README.md بخش «راه‌اندازی دیتابیس»).");
        throw;
    }
}

// ---------- خط لوله ----------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
