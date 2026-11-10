using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Momentum.Web.Data;
using Momentum.Web.Models;

namespace Momentum.Web.Services;

public class ImportColumn
{
    public string Key = "";
    public string Title = "";
    public string Hint = "";
    public double Width;
    public bool Required;
}

public class ImportRowResult
{
    public int Row { get; set; } // شمارهٔ ردیف اکسل (۱ مبنا)
    public string? Date { get; set; }
    public string SubjectName { get; set; } = "";
    public int? SubjectId { get; set; }
    public string Color { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public int Minutes { get; set; }
    public int StudyMin { get; set; }
    public int ReviewMin { get; set; }
    public int TestMin { get; set; }
    public int Tests { get; set; }
    public int Correct { get; set; }
    public int Wrong { get; set; }
    public string Note { get; set; } = "";
    public bool Done { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool Ok => Errors.Count == 0;
}

public class ImportAnalyzeResult
{
    public int HeaderRow { get; set; }
    public List<ImportRowResult> Rows { get; set; } = new();
    public int OkCount => Rows.Count(r => r.Ok);
    public int BadCount => Rows.Count(r => !r.Ok);
}

/// <summary>
/// ورود دسته‌ای از اکسل — پورت کامل منطق importer.js نسخهٔ اصلی:
/// ساخت قالب با اعتبارسنجی، تشخیص سرستون‌ها با نام‌های جایگزین،
/// تبدیل ارقام فارسی/عربی، تاریخ شمسی/میلادی و مدت به شکل‌های مختلف.
/// </summary>
public class ExcelImportService
{
    private static readonly ImportColumn[] Columns =
    {
        new() { Key = "date",       Title = "تاریخ",      Hint = "مثل ۱۴۰۵/۰۶/۲۷", Width = 14, Required = true },
        new() { Key = "subject",    Title = "درس",        Hint = "دقیقاً نام درس داخل اپ", Width = 16, Required = true },
        new() { Key = "start_time", Title = "ساعت شروع",  Hint = "اختیاری، مثل 08:30", Width = 11 },
        new() { Key = "end_time",   Title = "ساعت پایان", Hint = "اختیاری، مثل 10:00", Width = 11 },
        new() { Key = "minutes",    Title = "مدت کل",     Hint = "دقیقه یا ساعت:دقیقه (مثل 180 یا 3:00)", Width = 12 },
        new() { Key = "study_min",  Title = "خواندن",     Hint = "دقیقه، اختیاری", Width = 10 },
        new() { Key = "review_min", Title = "مرور",       Hint = "دقیقه، اختیاری", Width = 10 },
        new() { Key = "test_min",   Title = "تست زدن",    Hint = "دقیقه، اختیاری", Width = 10 },
        new() { Key = "tests",      Title = "تعداد تست",  Hint = "اختیاری", Width = 10 },
        new() { Key = "correct",    Title = "درست",       Hint = "اختیاری", Width = 8 },
        new() { Key = "wrong",      Title = "غلط",        Hint = "اختیاری", Width = 8 },
        new() { Key = "note",       Title = "شرح",        Hint = "اختیاری، مثل فیلم جلسه ۱۲", Width = 34 },
        new() { Key = "done",       Title = "انجام شد",   Hint = "بله / خیر (خالی = بله)", Width = 10 },
    };

    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["date"] = new[] { "تاریخ", "date", "روز" },
        ["subject"] = new[] { "درس", "subject", "نامدرس" },
        ["start_time"] = new[] { "ساعتشروع", "شروع", "start", "starttime", "از" },
        ["end_time"] = new[] { "ساعتپایان", "پایان", "end", "endtime", "تا" },
        ["minutes"] = new[] { "مدتکل", "مدت", "مدتمطالعه", "minutes", "duration", "کل", "زمان" },
        ["study_min"] = new[] { "خواندن", "مطالعه", "study", "studymin" },
        ["review_min"] = new[] { "مرور", "review", "reviewmin" },
        ["test_min"] = new[] { "تستزدن", "مدتتست", "زمانتست", "testmin", "testtime" },
        ["tests"] = new[] { "تعدادتست", "تست", "تستها", "tests", "count" },
        ["correct"] = new[] { "درست", "صحیح", "correct" },
        ["wrong"] = new[] { "غلط", "نادرست", "wrong" },
        ["note"] = new[] { "شرح", "توضیح", "توضیحات", "یادداشت", "note", "description" },
        ["done"] = new[] { "انجامشد", "انجام", "وضعیت", "done", "status" },
    };

    // ---------- نرمال‌سازی ----------
    private static readonly Regex SpaceLike = new(@"[‌​\s_\-()]", RegexOptions.Compiled);

    private static string ToEn(string? s) => Helpers.Fa.En(s);

    private static string Norm(string? s)
    {
        var x = ToEn(s).Replace("ي", "ی").Replace("ك", "ک");
        x = SpaceLike.Replace(x, "");
        if (x.EndsWith("ه") || x.EndsWith("ة")) x = x[..^1] + "ه";
        return x.ToLowerInvariant();
    }

    private static string NormName(string? s)
    {
        var x = ToEn(s).Replace("ي", "ی").Replace("ك", "ک").Replace("‌", "").Trim();
        x = Regex.Replace(x, @"\s+", " ");
        return x.ToLowerInvariant();
    }

    // ---------- ساخت قالب ----------
    public byte[] BuildTemplate(List<Subject> subjects, List<List<string?>>? prefill = null)
    {
        var active = subjects.Where(s => !s.Archived).Select(s => s.Name).ToList();
        using var wb = new XLWorkbook();

        var ws = wb.Worksheets.Add("برنامه");
        ws.RightToLeft = true;
        ws.SheetView.FreezeRows(1);

        // سرستون‌ها
        for (int c = 0; c < Columns.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = Columns[c].Title;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.FromHtml("#1E40AF");
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            ws.Column(c + 1).Width = Columns[c].Width;
        }

        // ردیف‌های نمونه/خالی با فرمت متن برای تاریخ و ساعت‌ها (تا اکسل دستکاری‌شان نکند)
        int row = 2;
        if (prefill != null)
        {
            foreach (var r in prefill)
            {
                for (int c = 0; c < Columns.Length; c++)
                {
                    var v = c < r.Count ? r[c] : null;
                    if (!string.IsNullOrEmpty(v)) ws.Cell(row, c + 1).Value = v;
                }
                row++;
            }
        }
        for (int i = 0; i < 300; i++)
        {
            foreach (var c in new[] { 1, 3, 4 }) // تاریخ، شروع، پایان
                ws.Cell(row, c).Style.NumberFormat.Format = "@";
            row++;
        }

        // اعتبارسنجی: نام درس و بله/خیر (شیت راهنما باید قبل از ارجاع ساخته شده باشد)
        var last = Math.Max(row, 1000);

        // شیت راهنما
        var g = wb.Worksheets.Add("راهنما");
        g.RightToLeft = true;
        var widths = new[] { 16d, 44, 4, 22 };
        for (int i = 0; i < widths.Length; i++) g.Column(i + 1).Width = widths[i];

        Action<int, int, string, bool> put = (r, c, v, bold) =>
        {
            var cell = g.Cell(r, c);
            cell.Value = v;
            if (bold) cell.Style.Font.Bold = true;
        };

        put(1, 1, "ستون", true); put(1, 2, "توضیح", true); put(1, 4, "درس‌های اپ (دقیقاً همین املا)", true);
        put(2, 2, "ردیف‌های شیت «برنامه» را پر کن؛ فقط تاریخ، درس و مدت اجباری است.", false);
        for (int i = 0; i < Columns.Length; i++)
        {
            put(i + 3, 1, Columns[i].Title + (Columns[i].Required ? " *" : ""), false);
            put(i + 3, 2, Columns[i].Hint, false);
        }
        for (int i = 0; i < active.Count; i++) put(i + 3, 4, active[i], false);

        var tips = new[]
        {
            "", "نکته‌ها:",
            "• تاریخ شمسی: ۱۴۰۵/۰۶/۲۷ یا 1405-06-27. اگر تاریخ میلادی اکسل هم بگذاری تبدیل می‌شود.",
            "• مدت کل را می‌توانی خالی بگذاری اگر خواندن/مرور/تست یا ساعت شروع و پایان را نوشته‌ای؛ خودش حساب می‌شود.",
            "• مدت به دقیقه است (۱۸۰) یا ساعت:دقیقه (3:00). عدد کوچک مثل «3» یعنی ۳ دقیقه، نه ۳ ساعت!",
            "• درست + غلط نباید از تعداد تست بیشتر باشد.",
            "• چند ردیف با یک تاریخ و درس مشکلی ندارد (چند جلسه در یک روز).",
            "• بعد از پر کردن، فایل را ذخیره کن (xlsx) و در اپ: تنظیمات ← «ورود از اکسل».",
            "", "نمونه:",
        };
        int r0 = Math.Max(Columns.Length + 3, active.Count + 3) + 1;
        foreach (var t in tips) { if (t != "") put(r0, 1, t, t.EndsWith(":")); r0++; }
        put(r0, 1, "تاریخ", true); put(r0, 2, "درس", true); put(r0, 3, "ساعت شروع", true); put(r0, 4, "ساعت پایان", true);
        put(r0, 5, "مدت کل", true); put(r0, 6, "خواندن", true); put(r0, 7, "مرور", true); put(r0, 8, "تست زدن", true);
        put(r0, 9, "تعداد تست", true); put(r0, 10, "درست", true); put(r0, 11, "غلط", true); put(r0, 12, "شرح", true); put(r0, 13, "انجام شد", true);
        r0++;
        var first = active.Count > 0 ? active[0] : "گسسته";
        var second = active.Count > 1 ? active[1] : "مدار";
        var third = active.Count > 2 ? active[2] : "هوش";
        put(r0, 1, "1405/06/27", false); put(r0, 2, first, false); put(r0, 3, "08:00", false); put(r0, 4, "09:40", false);
        put(r0, 5, "100", false); put(r0, 6, "70", false); put(r0, 8, "30", false); put(r0, 9, "60", false);
        put(r0, 10, "42", false); put(r0, 11, "9", false); put(r0, 12, "تست فصل ۳", false); put(r0, 13, "بله", false);
        r0++;
        put(r0, 1, "1405/06/27", false); put(r0, 2, second, false); put(r0, 5, "3:00", false);
        put(r0, 12, "فیلم جلسه ۱۲", false); put(r0, 13, "بله", false);
        r0++;
        put(r0, 1, "1405/06/28", false); put(r0, 2, first, false); put(r0, 6, "90", false); put(r0, 7, "30", false);
        put(r0, 12, "فصل ۴", false); put(r0, 13, "خیر", false);

        // اعتبارسنجی‌ها (بعد از ساخت هر دو شیت):
        // نام درس از لیست شیت راهنما + بله/خیر
        var dv1 = ws.Range($"B2:B{last}").CreateDataValidation();
        dv1.List(g.Range($"D3:D{2 + Math.Max(1, active.Count)}"));
        dv1.ShowErrorMessage = true;
        dv1.ErrorTitle = "نامعتبر";
        dv1.ErrorMessage = "نام درس باید دقیقاً یکی از درس‌های اپ باشد (لیست را در شیت راهنما ببین)";
        dv1.ErrorStyle = XLErrorStyle.Stop;

        var dv2 = ws.Range($"M2:M{last}").CreateDataValidation();
        dv2.List("\"بله,خیر\"");
        dv2.ShowErrorMessage = true;
        dv2.ErrorTitle = "نامعتبر";
        dv2.ErrorMessage = "فقط بله یا خیر";
        dv2.ErrorStyle = XLErrorStyle.Stop;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] BuildSample(List<Subject> subjects)
    {
        var active = subjects.Where(s => !s.Archived).Select(s => s.Name).ToList();
        var today = Jalali.TodayJalali().Replace("-", "/");
        var yesterday = Jalali.AddDays(Jalali.TodayJalali(), -1).Replace("-", "/");
        var prefill = new List<List<string?>>
        {
            new() { yesterday, active.Count > 0 ? active[0] : "گسسته", "08:00", "09:40", null, "70", null, "30", "60", "42", "9", "تست فصل ۳ — مدت از ساعت شروع/پایان حساب می‌شود؛ ۶۰ تست: ۴۲ درست، ۹ غلط", "بله" },
            new() { yesterday, active.Count > 1 ? active[1] : "مدار", null, null, "3:00", null, null, null, null, null, null, "فیلم جلسه ۱۲ — فقط مدت کل", "بله" },
            new() { today, active.Count > 2 ? active[2] : "هوش", null, null, null, "90", "30", null, null, null, null, "خواندن ۹۰ + مرور ۳۰ = مدت کل ۱۲۰", "خیر" },
        };
        return BuildTemplate(subjects, prefill);
    }

    // ---------- خواندن فایل ----------
    private static List<object?[]> ReadSheet(byte[] buffer, string filename)
    {
        if (filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return ReadCsv(buffer);

        using var wb = new XLWorkbook(new MemoryStream(buffer));
        var ws = wb.Worksheets.First();
        var lastRow = Math.Max(1, ws.LastRowUsed()?.RowNumber() ?? 1);
        var lastCol = Math.Max(1, ws.LastColumnUsed()?.ColumnNumber() ?? 1);

        var rows = new List<object?[]>(lastRow);
        for (int r = 1; r <= lastRow; r++)
        {
            var row = new object?[lastCol];
            for (int c = 1; c <= lastCol; c++)
            {
                var cell = ws.Cell(r, c);
                row[c - 1] = cell.DataType switch
                {
                    XLDataType.Blank => null,
                    XLDataType.Boolean => cell.GetBoolean().ToString(),
                    XLDataType.DateTime => cell.GetDateTime(),          // تاریخ میلادی اکسل
                    XLDataType.TimeSpan => cell.GetTimeSpan(),           // سلول ساعت
                    XLDataType.Number => cell.GetDouble(),
                    _ => cell.GetString()
                };
            }
            rows.Add(row);
        }
        return rows;
    }

    private static List<object?[]> ReadCsv(byte[] buffer)
    {
        // BOM را حذف کن
        var text = Encoding.UTF8.GetString(buffer).TrimStart('\uFEFF');
        var rows = new List<object?[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (q)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else q = false;
                }
                else cell.Append(ch);
            }
            else if (ch == '"') q = true;
            else if (ch is ',' or ';' or '\t') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch is '\n' or '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row.ToArray()); row = new List<string>();
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }

    // ---------- تبدیل مقدارها (پورت مستقیم از importer.js) ----------
    private static string? ParseDate(object? v)
    {
        if (v == null) return null;
        if (v is DateTime dt) return Jalali.FromDateTime(dt.Date); // تاریخ میلادی اکسل → شمسی
        if (v is double num)
        {
            if (num > 20000 && num < 80000)
            {
                var g = new DateTime(1899, 12, 30).AddDays(Math.Round(num));
                return Jalali.FromDateTime(g);
            }
            v = num.ToString("0.############", CultureInfo.InvariantCulture);
        }

        var s = ToEn(v.ToString()).Trim().Replace(".", "-").Replace("/", "-");
        var m = Regex.Match(s, @"^(\d{4})-(\d{1,2})-(\d{1,2})$");
        if (!m.Success)
        {
            m = Regex.Match(s, @"^(\d{2})-(\d{1,2})-(\d{1,2})$");
            if (m.Success) s = "14" + s;
            else return null;
        }
        if (!Regex.IsMatch(s, @"^\d{4}-\d{1,2}-\d{1,2}$")) return null;
        var parts = s.Split('-');
        if (!int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var mo) || !int.TryParse(parts[2], out var d)) return null;
        if (y >= 1900 && y <= 2200) return Jalali.FromDateTime(new DateTime(y, mo, d)); // میلادی
        var js = $"{y:D4}-{mo:D2}-{d:D2}";
        return Jalali.Parse(js) != null ? js : null;
    }

    private static string? ParseTime(object? v)
    {
        if (v == null) return "";
        if (v is TimeSpan ts)
        {
            if (ts.TotalDays >= 1) return null;
            return $"{(int)ts.Hours:D2}:{ts.Minutes:D2}";
        }
        if (v is DateTime dt) return $"{dt.Hour:D2}:{dt.Minute:D2}";
        if (v is double num)
        {
            if (num >= 0 && num < 1)
            {
                var m = (int)Math.Round(num * 1440) % 1440;
                return $"{m / 60:D2}:{m % 60:D2}";
            }
            return null;
        }
        var s = ToEn(v.ToString()).Trim().Replace(":", ":").Replace(".", ":").Replace("،", ":").Replace("٫", ":");
        var m2 = Regex.Match(s, @"^(\d{1,2}):(\d{2})(?::\d{2})?$");
        if (!m2.Success || int.Parse(m2.Groups[1].Value) > 23 || int.Parse(m2.Groups[2].Value) > 59) return null;
        return $"{int.Parse(m2.Groups[1].Value):D2}:{m2.Groups[2].Value}";
    }

    private static int? ParseMinutes(object? v)
    {
        if (v == null || (v is string s0 && string.IsNullOrWhiteSpace(s0))) return 0;
        if (v is double num)
        {
            if (num >= 0 && num < 1) return (int)Math.Round(num * 1440);
            return (int)Math.Round(num);
        }
        if (v is TimeSpan ts) return (int)ts.TotalMinutes;
        var s = ToEn(v.ToString()).Trim();
        var m = Regex.Match(s, @"^(\d{1,2}):(\d{2})$");
        if (m.Success) return int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
        m = Regex.Match(s, @"^(\d+(?:\.\d+)?)\s*(ساعت|h|hr|hour|hours)$", RegexOptions.IgnoreCase);
        if (m.Success) return (int)Math.Round(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60);
        m = Regex.Match(s, @"^(\d+)\s*(دقیقه|min|m)?$", RegexOptions.IgnoreCase);
        if (m.Success) return int.Parse(m.Groups[1].Value);
        return null;
    }

    private static int? ParseInt0(object? v)
    {
        if (v == null || (v is string s0 && string.IsNullOrWhiteSpace(s0))) return 0;
        if (v is double num) return num >= 0 && Math.Abs(num - Math.Round(num)) < 1e-9 ? (int)Math.Round(num) : null;
        if (!int.TryParse(ToEn(v.ToString()).Trim(), out var n)) return null;
        return n >= 0 ? n : null;
    }

    private static bool? ParseDone(object? v)
    {
        if (v == null || string.IsNullOrWhiteSpace(v.ToString())) return true;
        var s = Norm(v.ToString());
        if (new[] { "بله", "آره", "اره", "yes", "y", "true", "1", "✓", "انجامشد", "done", "ok" }.Contains(s)) return true;
        if (new[] { "خیر", "نه", "no", "n", "false", "0", "انجامنشد", "notdone" }.Contains(s)) return false;
        return null;
    }

    private static (int headerRow, Dictionary<string, int> map)? DetectHeader(List<object?[]> rows)
    {
        for (int i = 0; i < Math.Min(rows.Count, 10); i++)
        {
            var map = new Dictionary<string, int>();
            var row = rows[i];
            for (int c = 0; c < row.Length; c++)
            {
                var n = Norm(row[c]?.ToString());
                if (string.IsNullOrEmpty(n)) continue;
                foreach (var (key, names) in Aliases)
                {
                    if (!map.ContainsKey(key) && names.Any(a => Norm(a) == n))
                        map[key] = c;
                }
            }
            if (map.ContainsKey("date") && map.ContainsKey("subject"))
                return (i, map);
        }
        return null;
    }

    private static string TitleOf(string key) => Columns.First(c => c.Key == key).Title;

    /// <summary>تحلیل فایل اکسل/CSV و اعتبارسنجی ردیف‌ها</summary>
    public ImportAnalyzeResult Analyze(byte[] buffer, string filename, List<Subject> subjects, List<Entry> existing)
    {
        var rows = ReadSheet(buffer, filename);
        var det = DetectHeader(rows)
            ?? throw new InvalidOperationException("سرستون‌های قالب پیدا نشد. حداقل ستون‌های «تاریخ» و «درس» باید در ردیف اول باشند. از قالب دانلودشده استفاده کن.");

        var (headerRow, map) = det;
        var subjByName = subjects.ToDictionary(s => NormName(s.Name), s => s);
        var existingKeys = existing
            .Select(e => $"{e.Date}|{e.SubjectId}|{e.Minutes}|{(e.Note ?? "").Trim()}")
            .ToHashSet();
        var seen = new HashSet<string>();
        var outRows = new List<ImportRowResult>();

        for (int r = headerRow + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            object? Get(string k) => map.TryGetValue(k, out var c) && c < row.Length ? row[c] : null;

            // ردیف کاملاً خالی؟
            bool isEmpty = Columns.All(c => Get(c.Key) is null or "" or " ");
            if (isEmpty) continue;

            var errors = new List<string>();
            var warnings = new List<string>();
            var e = new ImportRowResult { Row = r + 1 };

            e.Date = ParseDate(Get("date"));
            if (e.Date == null) errors.Add("تاریخ نامعتبر");

            var sname = Get("subject")?.ToString()?.Trim() ?? "";
            e.SubjectName = sname;
            if (string.IsNullOrEmpty(sname)) errors.Add("درس خالی است");
            else if (subjByName.TryGetValue(NormName(sname), out var subj))
            {
                e.SubjectId = subj.Id;
                e.SubjectName = subj.Name;
                e.Color = subj.Color;
                if (subj.Archived) warnings.Add("درس بایگانی شده");
            }
            else errors.Add($"درس «{sname}» در اپ وجود ندارد");

            var st = ParseTime(Get("start_time"));
            if (st == null) { errors.Add("ساعت شروع نامعتبر"); st = ""; }
            e.StartTime = st;

            var et = ParseTime(Get("end_time"));
            if (et == null) { errors.Add("ساعت پایان نامعتبر"); et = ""; }
            e.EndTime = et;

            foreach (var k in new[] { "minutes", "study_min", "review_min", "test_min" })
            {
                var v = ParseMinutes(Get(k));
                if (v == null) { errors.Add($"{TitleOf(k)} نامعتبر"); }
                else switch (k)
                {
                    case "minutes": e.Minutes = v.Value; break;
                    case "study_min": e.StudyMin = v.Value; break;
                    case "review_min": e.ReviewMin = v.Value; break;
                    case "test_min": e.TestMin = v.Value; break;
                }
            }

            foreach (var k in new[] { "tests", "correct", "wrong" })
            {
                var v = ParseInt0(Get(k));
                if (v == null) errors.Add($"{TitleOf(k)} نامعتبر");
                else switch (k)
                {
                    case "tests": e.Tests = v.Value; break;
                    case "correct": e.Correct = v.Value; break;
                    case "wrong": e.Wrong = v.Value; break;
                }
            }

            e.Note = Get("note")?.ToString()?.Trim() ?? "";
            var dn = ParseDone(Get("done"));
            if (dn == null) { errors.Add("انجام شد باید بله/خیر باشد"); e.Done = true; }
            else e.Done = dn.Value;

            // محاسبهٔ مدت کل در صورت خالی بودن
            if (e.Minutes == 0)
            {
                var parts = e.StudyMin + e.ReviewMin + e.TestMin;
                if (parts > 0) e.Minutes = parts;
                else if (!string.IsNullOrEmpty(e.StartTime) && !string.IsNullOrEmpty(e.EndTime))
                    e.Minutes = Helpers.Fa.DiffMinutes(e.StartTime, e.EndTime);
            }
            if (e.Minutes == 0) errors.Add("مدت مطالعه مشخص نیست (مدت کل یا خواندن/مرور/تست یا ساعت شروع و پایان)");
            if (e.Minutes > 1440) errors.Add("مدت بیشتر از ۲۴ ساعت");
            if (e.Minutes is > 0 and < 15 && Get("minutes") is string or double && !string.IsNullOrWhiteSpace(Get("minutes")?.ToString())
                && !Regex.IsMatch(ToEn(Get("minutes")?.ToString()), @"[:hس]"))
                warnings.Add($"مدت فقط {e.Minutes} دقیقه است — اگر منظورت ساعت بود، مثل 3:00 بنویس");
            if (e.Correct + e.Wrong > e.Tests) errors.Add("درست + غلط بیشتر از تعداد تست");

            if (e.Date != null && e.SubjectId != null)
            {
                var key = $"{e.Date}|{e.SubjectId}|{e.Minutes}|{e.Note}";
                if (existingKeys.Contains(key)) warnings.Add("احتمالاً تکراری: همین آیتم قبلاً در اپ ثبت شده");
                if (!seen.Add(key)) warnings.Add("ردیف تکراری داخل فایل");
            }

            e.Errors = errors;
            e.Warnings = warnings;
            outRows.Add(e);
        }

        if (outRows.Count == 0)
            throw new InvalidOperationException("هیچ ردیف پرشده‌ای در فایل نیست");

        return new ImportAnalyzeResult { HeaderRow = headerRow + 1, Rows = outRows };
    }

    /// <summary>افزودن دسته‌ای ردیف‌ها در یک تراکنش</summary>
    public static List<int> AddEntries(AppDbContext db, IEnumerable<ImportRowResult> rows)
    {
        var ids = new List<int>();
        using var tx = db.Database.BeginTransaction();
        foreach (var x in rows)
        {
            var entry = new Entry
            {
                Date = x.Date!,
                SubjectId = x.SubjectId!.Value,
                Minutes = x.Minutes,
                Note = x.Note,
                Done = x.Done,
                CreatedAt = DateTime.UtcNow,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                StudyMin = x.StudyMin,
                ReviewMin = x.ReviewMin,
                TestMin = x.TestMin,
                Tests = x.Tests,
                Correct = x.Correct,
                Wrong = x.Wrong
            };
            db.Entries.Add(entry);
            db.SaveChanges();
            ids.Add(entry.Id);
        }
        tx.Commit();
        return ids;
    }
}
