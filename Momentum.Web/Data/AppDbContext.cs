using Microsoft.EntityFrameworkCore;
using Momentum.Web.Models;

namespace Momentum.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<DayNote> DayNotes => Set<DayNote>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ReviewItem> ReviewItems => Set<ReviewItem>();
    public DbSet<ReviewLog> ReviewLogs => Set<ReviewLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Subject>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Color).IsRequired();
            e.HasIndex(x => new { x.Archived, x.Sort, x.Id });
        });

        b.Entity<Entry>(e =>
        {
            e.HasIndex(x => x.Date);
            e.Property(x => x.Date).IsRequired();
            // مثل نسخهٔ اصلی: حذف درسی که آیتم دارد ممنوع است (در سطح برنامه چک می‌شود)
            e.HasOne(x => x.Subject)
             .WithMany()
             .HasForeignKey(x => x.SubjectId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SubjectId);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        b.Entity<Goal>(e =>
        {
            e.HasIndex(x => new { x.Period, x.Key, x.SubjectId }).IsUnique();
            e.HasIndex(x => new { x.Period, x.Key });
        });

        b.Entity<DayNote>(e =>
        {
            e.HasKey(x => x.Date);
        });

        b.Entity<Setting>(e => e.HasKey(x => x.Key));

        b.Entity<ReviewItem>(e =>
        {
            e.HasIndex(x => x.DueAt);
            e.HasOne(x => x.Subject)
             .WithMany()
             .HasForeignKey(x => x.SubjectId)
             .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        b.Entity<ReviewLog>(e =>
        {
            e.HasIndex(x => x.ReviewItemId);
            e.HasOne(x => x.ReviewItem)
             .WithMany()
             .HasForeignKey(x => x.ReviewItemId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>ساخت دیتابیس در اولین اجرا + داده‌های اولیه (درس‌ها و تنظیمات پیش‌فرض)</summary>
public static class DbInitializer
{
    public static void Initialize(AppDbContext db)
    {
        // دیتابیس و جداول را در صورت نبود می‌سازد (معادل EnsureCreated)
        db.Database.EnsureCreated();

        if (!db.Subjects.Any())
        {
            int i = 0;
            foreach (var (name, color) in AppSettings.DefaultSubjects)
                db.Subjects.Add(new Subject { Name = name, Color = color, Sort = i++ });
        }

        SeedSetting(db, "exam_date", AppSettings.ExamDate);
        SeedSetting(db, "exam_title", AppSettings.ExamTitle);
        SeedSetting(db, "user_name", AppSettings.UserName);

        db.SaveChanges();
    }

    private static void SeedSetting(AppDbContext db, string key, string value)
    {
        if (!db.Settings.Any(s => s.Key == key))
            db.Settings.Add(new Setting { Key = key, Value = value });
    }
}
