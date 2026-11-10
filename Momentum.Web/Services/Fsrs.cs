using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Momentum.Web.Services;

public enum FsrsState { New = 0, Learning = 1, Review = 2, Relearning = 3 }
public enum FsrsRating { Manual = 0, Again = 1, Hard = 2, Good = 3, Easy = 4 }

/// <summary>
/// کارت FSRS — دقیقاً هم‌ساختار ts-fsrs؛ سریال‌سازی JSON آن با فرمت نسخهٔ اصلی
/// (Node.js) سازگار است تا فایل‌های بکاپ دو طرفه کار کنند.
/// </summary>
public class FsrsCard
{
    [JsonPropertyName("due")] public DateTime Due { get; set; }
    [JsonPropertyName("stability")] public double Stability { get; set; }
    [JsonPropertyName("difficulty")] public double Difficulty { get; set; }
    [JsonPropertyName("elapsed_days")] public int ElapsedDays { get; set; }
    [JsonPropertyName("scheduled_days")] public int ScheduledDays { get; set; }
    [JsonPropertyName("reps")] public int Reps { get; set; }
    [JsonPropertyName("lapses")] public int Lapses { get; set; }
    [JsonPropertyName("learning_steps")] public int LearningSteps { get; set; }
    [JsonPropertyName("state")] public int State { get; set; }
    [JsonPropertyName("last_review")] public DateTime? LastReview { get; set; }

    public FsrsCard Clone() => (FsrsCard)MemberwiseClone();
}

public class FsrsReviewLog
{
    [JsonPropertyName("rating")] public int Rating { get; set; }
    [JsonPropertyName("state")] public int State { get; set; }
    [JsonPropertyName("due")] public DateTime Due { get; set; }
    [JsonPropertyName("stability")] public double Stability { get; set; }
    [JsonPropertyName("difficulty")] public double Difficulty { get; set; }
    [JsonPropertyName("elapsed_days")] public int ElapsedDays { get; set; }
    [JsonPropertyName("last_elapsed_days")] public int LastElapsedDays { get; set; }
    [JsonPropertyName("scheduled_days")] public int ScheduledDays { get; set; }
    [JsonPropertyName("learning_steps")] public int LearningSteps { get; set; }
    [JsonPropertyName("review")] public DateTime Review { get; set; }
}

public class FsrsResult
{
    public FsrsCard Card { get; set; } = new();
    public FsrsReviewLog Log { get; set; } = new();
}

/// <summary>
/// پیاده‌سازی کامل الگوریتم FSRS (نسخهٔ ۶، مطابق کتابخانهٔ ts-fsrs 5.4.2 که نسخهٔ
/// اصلی Momentum استفاده می‌کرد) با پارامترهای پیش‌فرض:
/// retention = 0.9 ، max interval = 36500 ، short-term فعال ، fuzz غیرفعال،
/// learning steps = ‏1m/10m و relearning = ‏10m.
/// </summary>
public class Fsrs
{
    // پارامترهای پیش‌فرض FSRS-6 (w0 … w20)
    private static readonly double[] W =
    {
        0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334, 3.0194, 1e-3,
        1.8722, 0.1666, 0.796, 1.4835, 0.0614, 0.2629, 1.6483, 0.6014,
        1.8729, 0.5425, 0.0912, 0.0658, 0.1542
    };

    private const double RequestRetention = 0.9;
    private const double MaximumInterval = 36500;
    private const bool EnableFuzz = false;
    private const bool EnableShortTerm = true;

    private static readonly int[] LearningSteps = { 1, 10 };   // دقیقه
    private static readonly int[] RelearningSteps = { 10 };    // دقیقه

    private const double SMin = 1e-3;
    private const double SMax = 36500;

    // مقادیر پیش‌محاسبه‌شده فقط از پارامترهای ثابت می‌آیند؛ استاتیک و بی‌تغییرند
    private static readonly double _decay;
    private static readonly double _factor;
    private static readonly double _intervalModifier;

    static Fsrs()
    {
        _decay = -W[20];
        // FACTOR = exp(decay^(-1) × ln(0.9)) − 1  (منهای ۱ بیرون از exp — مطابق computeDecayFactor در ts-fsrs)
        _factor = RoundTo(Math.Exp(Math.Pow(_decay, -1) * Math.Log(0.9)) - 1, 8);
        _intervalModifier = RoundTo((Math.Pow(RequestRetention, 1.0 / _decay) - 1) / _factor, 8);
    }

    public Fsrs() { }

    // ---------- ابزارها ----------
    private static double Clamp(double v, double min, double max) => Math.Min(Math.Max(v, min), max);

    private static double RoundTo(double num, int decimals)
    {
        var f = Math.Pow(10, decimals);
        return Math.Round(num * f, MidpointRounding.AwayFromZero) / f;
    }

    private static int RoundJs(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>تفاوت روز تقویمی بین دو تاریخ (بر اساس تاریخ UTC، مثل نسخهٔ اصلی)</summary>
    private static int DateDiffInDays(DateTime last, DateTime cur)
    {
        var a = DateTime.SpecifyKind(last, DateTimeKind.Utc);
        var b = DateTime.SpecifyKind(cur, DateTimeKind.Utc);
        return (int)(b.Date - a.Date).TotalDays;
    }

    /// <summary>R(t,S) = (1 + FACTOR × t / S)^DECAY</summary>
    public static double ForgettingCurve(double elapsedDays, double stability)
        => RoundTo(Math.Pow(1 + _factor * elapsedDays / stability, _decay), 8);

    public static double IntervalModifier => _intervalModifier;

    // ---------- اجزای الگوریتم ----------
    private static double InitStability(int g) => Math.Max(W[g - 1], 0.1);

    private static double InitDifficulty(int g) => RoundTo(W[4] - Math.Exp((g - 1) * W[5]) + 1, 8);

    private static double LinearDamping(double deltaD, double oldD) => RoundTo(deltaD * (10 - oldD) / 9, 8);

    private static double MeanReversion(double init, double current) => RoundTo(W[7] * init + (1 - W[7]) * current, 8);

    private static double NextDifficulty(double d, int g)
    {
        var deltaD = -W[6] * (g - 3);
        var nextD = d + LinearDamping(deltaD, d);
        return Clamp(MeanReversion(InitDifficulty((int)FsrsRating.Easy), nextD), 1, 10);
    }

    private static double NextRecallStability(double d, double s, double r, int g)
    {
        var hardPenalty = g == (int)FsrsRating.Hard ? W[15] : 1;
        var easyBound = g == (int)FsrsRating.Easy ? W[16] : 1;
        return RoundTo(Clamp(
            s * (1 + Math.Exp(W[8]) * (11 - d) * Math.Pow(s, -W[9])
                * (Math.Exp((1 - r) * W[10]) - 1) * hardPenalty * easyBound),
            SMin, SMax), 8);
    }

    private static double NextForgetStability(double d, double s, double r)
        => RoundTo(Clamp(W[11] * Math.Pow(d, -W[12]) * (Math.Pow(s + 1, W[13]) - 1) * Math.Exp((1 - r) * W[14]), SMin, SMax), 8);

    private static double NextShortTermStability(double s, int g)
    {
        var sinc = Math.Pow(s, -W[19]) * Math.Exp(W[17] * (g - 3 + W[18]));
        var maskedSinc = g >= (int)FsrsRating.Hard ? Math.Max(sinc, 1) : sinc;
        return RoundTo(Clamp(s * maskedSinc, SMin, SMax), 8);
    }

    /// <summary>حالت بعدی حافظه (difficulty, stability)</summary>
    private static (double D, double S) NextState(double d, double s, double t, int g, double? r = null)
    {
        if (t < 0) throw new ArgumentOutOfRangeException(nameof(t));
        if (g < 0 || g > 4) throw new ArgumentOutOfRangeException(nameof(g));

        if (d == 0 && s == 0)
            return (Clamp(InitDifficulty(g), 1, 10), InitStability(g));

        if (g == 0) return (d, s);

        var rv = r ?? ForgettingCurve(t, s);
        double newS;
        if (t == 0 && EnableShortTerm)
        {
            newS = NextShortTermStability(s, g);
        }
        else if (g == 1)
        {
            var sAfterFail = NextForgetStability(d, s, rv);
            var w17 = EnableShortTerm ? W[17] : 0;
            var w18 = EnableShortTerm ? W[18] : 0;
            var nextSMin = s / Math.Exp(w17 * w18);
            newS = Clamp(RoundTo(nextSMin, 8), SMin, sAfterFail);
        }
        else
        {
            newS = NextRecallStability(d, s, rv, g);
        }
        var newD = NextDifficulty(d, g);
        return (newD, newS);
    }

    private static int NextInterval(double s, int elapsedDays)
    {
        var newInterval = Math.Min(Math.Max(1, RoundJs(s * _intervalModifier)), MaximumInterval);
        return (int)ApplyFuzz(newInterval, elapsedDays);
    }

    private static double ApplyFuzz(double ivl, int elapsedDays)
        => EnableFuzz && ivl >= 2.5 ? FuzzRange(ivl, elapsedDays) : Math.Round(ivl);
    private static double FuzzRange(double ivl, int elapsedDays)
    {
        // فقط وقتی fuzz فعال باشد فراخوانی می‌شود؛ برای کامل بودن پیاده‌سازی آمده است.
        double delta = 1;
        var ranges = new (double start, double end, double factor)[] { (2.5, 7, .15), (7, 20, .1), (20, double.MaxValue, .05) };
        foreach (var (st, en, fa) in ranges)
            delta += fa * Math.Max(Math.Min(ivl, en) - st, 0);
        ivl = Math.Min(ivl, MaximumInterval);
        var minIvl = Math.Max(2, RoundJs(ivl - delta));
        var maxIvl = Math.Min(RoundJs(ivl + delta), MaximumInterval);
        if (ivl > elapsedDays) minIvl = Math.Max(minIvl, elapsedDays + 1);
        minIvl = (int)Math.Min(minIvl, maxIvl);
        return minIvl; // بدون seed تصادفی، کران پایین
    }

    // ---------- زمان‌بند (BasicScheduler) ----------

    /// <summary>کارت خالی جدید (due = همان لحظه)</summary>
    public static FsrsCard CreateEmptyCard(DateTime? now = null)
        => new()
        {
            Due = now ?? DateTime.UtcNow,
            Stability = 0,
            Difficulty = 0,
            ElapsedDays = 0,
            ScheduledDays = 0,
            Reps = 0,
            Lapses = 0,
            LearningSteps = 0,
            State = (int)FsrsState.New,
            LastReview = null
        };

    /// <summary>پیش‌نمایش نتیجهٔ هر چهار امتیاز بدون تغییر کارت</summary>
    public Dictionary<FsrsRating, FsrsResult> Preview(FsrsCard card, DateTime now)
        => new()
        {
            [FsrsRating.Again] = Review(card, now, (int)FsrsRating.Again),
            [FsrsRating.Hard] = Review(card, now, (int)FsrsRating.Hard),
            [FsrsRating.Good] = Review(card, now, (int)FsrsRating.Good),
            [FsrsRating.Easy] = Review(card, now, (int)FsrsRating.Easy),
        };

    public FsrsResult Next(FsrsCard card, DateTime now, int rating) => Review(card, now, rating);

    private FsrsResult Review(FsrsCard input, DateTime now, int grade)
    {
        if (grade < 1 || grade > 4) throw new ArgumentOutOfRangeException(nameof(grade));

        var last = input.Clone();
        var current = input.Clone();

        // init
        int interval = 0;
        if (current.State != (int)FsrsState.New && current.LastReview.HasValue)
            interval = DateDiffInDays(current.LastReview.Value, now);
        current.LastReview = now;
        current.ElapsedDays = interval;
        current.Reps += 1;
        int elapsedDays = interval;

        FsrsCard next;
        switch (last.State)
        {
            case (int)FsrsState.New:
                next = NextDs(current, elapsedDays, grade);
                ApplyLearningSteps(next, grade, (int)FsrsState.Learning, current, elapsedDays, now);
                break;
            case (int)FsrsState.Learning:
            case (int)FsrsState.Relearning:
                next = NextDs(current, elapsedDays, grade);
                ApplyLearningSteps(next, grade, last.State, current, elapsedDays, now);
                break;
            default: // Review
                next = ReviewStateCard(current, grade, elapsedDays, now);
                break;
        }

        return new FsrsResult
        {
            Card = next,
            Log = new FsrsReviewLog
            {
                Rating = grade,
                State = current.State,
                Due = current.LastReview ?? last.Due,
                Stability = current.Stability,
                Difficulty = current.Difficulty,
                ElapsedDays = elapsedDays,
                LastElapsedDays = last.ElapsedDays,
                ScheduledDays = current.ScheduledDays,
                LearningSteps = current.LearningSteps,
                Review = now
            }
        };
    }

    private FsrsCard ReviewStateCard(FsrsCard current, int grade, int elapsedDays, DateTime now)
    {
        int interval = elapsedDays;
        var r = ForgettingCurve(interval, current.Stability);

        var nextAgain = NextDs(current, interval, (int)FsrsRating.Again, r);
        var nextHard = NextDs(current, interval, (int)FsrsRating.Hard, r);
        var nextGood = NextDs(current, interval, (int)FsrsRating.Good, r);
        var nextEasy = NextDs(current, interval, (int)FsrsRating.Easy, r);

        // بازهٔ مرور برای Hard / Good / Easy (بازه‌ها نباید هم‌پوشان یا نزولی شوند)
        var hardInterval = NextInterval(nextHard.Stability, interval);
        var goodInterval = NextInterval(nextGood.Stability, interval);
        hardInterval = Math.Min(hardInterval, goodInterval);
        goodInterval = Math.Max(goodInterval, hardInterval + 1);
        var easyInterval = Math.Max(NextInterval(nextEasy.Stability, interval), goodInterval + 1);

        nextHard.ScheduledDays = hardInterval; nextHard.Due = now.AddDays(hardInterval);
        nextGood.ScheduledDays = goodInterval; nextGood.Due = now.AddDays(goodInterval);
        nextEasy.ScheduledDays = easyInterval; nextEasy.Due = now.AddDays(easyInterval);

        nextHard.State = (int)FsrsState.Review; nextHard.LearningSteps = 0;
        nextGood.State = (int)FsrsState.Review; nextGood.LearningSteps = 0;
        nextEasy.State = (int)FsrsState.Review; nextEasy.LearningSteps = 0;

        // برای «دوباره»: برگرد به مرحلهٔ یادگیری مجدد
        ApplyLearningSteps(nextAgain, (int)FsrsRating.Again, (int)FsrsState.Relearning, current, elapsedDays, now);
        nextAgain.Lapses += 1;

        return grade switch
        {
            (int)FsrsRating.Again => nextAgain,
            (int)FsrsRating.Hard => nextHard,
            (int)FsrsRating.Good => nextGood,
            _ => nextEasy
        };
    }

    private static FsrsCard NextDs(FsrsCard current, double t, int g, double? r = null)
    {
        var (newD, newS) = NextState(current.Difficulty, current.Stability, t, g, r);
        var card = current.Clone();
        card.Difficulty = newD;
        card.Stability = newS;
        return card;
    }

    /// <summary>اعمال مراحل یادگیری کوتاه‌مدت (1m/10m) روی کارت بعد از امتیازدهی</summary>
    private static void ApplyLearningSteps(FsrsCard nextCard, int grade, int toState, FsrsCard current, int elapsedDays, DateTime now)
    {
        var (scheduledMinutes, nextSteps) = GetLearningInfo(current.State, current.LearningSteps, grade);

        if (scheduledMinutes > 0 && scheduledMinutes < 1440)
        {
            nextCard.LearningSteps = nextSteps;
            nextCard.ScheduledDays = 0;
            nextCard.State = toState;
            nextCard.Due = now.AddMinutes(scheduledMinutes);
        }
        else
        {
            nextCard.State = (int)FsrsState.Review;
            if (scheduledMinutes >= 1440)
            {
                nextCard.LearningSteps = nextSteps;
                nextCard.Due = now.AddMinutes(scheduledMinutes);
                nextCard.ScheduledDays = scheduledMinutes / 1440;
            }
            else
            {
                nextCard.LearningSteps = 0;
                var interval = NextInterval(nextCard.Stability, elapsedDays);
                nextCard.ScheduledDays = interval;
                nextCard.Due = now.AddDays(interval);
            }
        }
    }

    /// <summary>استراتژی مراحل یادگیری (BasicLearningStepsStrategy در ts-fsrs)</summary>
    private static (int scheduledMinutes, int nextStep) GetLearningInfo(int state, int curStep, int grade)
    {
        var steps = state == (int)FsrsState.Relearning || state == (int)FsrsState.Review
            ? RelearningSteps
            : LearningSteps;
        var len = steps.Length;
        if (len == 0 || curStep >= len) return (0, 0);

        if (state == (int)FsrsState.Review)
        {
            // در حالت مرور، فقط «دوباره» به مرحلهٔ یادگیری برمی‌گردد
            return grade == (int)FsrsRating.Again
                ? (steps[Math.Max(0, curStep < len ? curStep : 0)], 0)
                : (0, 0);
        }

        switch (grade)
        {
            case (int)FsrsRating.Again:
                return (steps[0], 0);
            case (int)FsrsRating.Hard:
                {
                    var hard = len == 1 ? RoundJs(steps[0] * 1.5) : RoundJs((steps[0] + steps[1]) / 2.0);
                    return (hard, curStep);
                }
            case (int)FsrsRating.Good:
                {
                    var nextIdx = curStep + 1;
                    return nextIdx < len ? (steps[nextIdx], nextIdx) : (0, 0);
                }
            default:
                return (0, 0);
        }
    }
}

/// <summary>سریال‌سازی/خواندن کارت FSRS با فرمت سازگار با ts-fsrs</summary>
public static class FsrsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true
    };

    static FsrsJson()
    {
        Options.Converters.Add(new FsrsDateTimeConverter());
    }

    public static string Stringify(FsrsCard card) => JsonSerializer.Serialize(card, Options);

    public static FsrsCard Parse(string json)
        => JsonSerializer.Deserialize<FsrsCard>(json, Options)
           ?? throw new FormatException("کارت FSRS نامعتبر است");
}

/// <summary>تاریخ‌ها همیشه به شکل UTC با میلی‌ثانیه («2026-01-20T08:30:00.000Z») مثل Date.toJSON در جاوااسکریپت</summary>
public class FsrsDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString()!;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d))
            {
                if (d.Kind == DateTimeKind.Local) d = d.ToUniversalTime();
                return DateTime.SpecifyKind(d, DateTimeKind.Utc);
            }
        }
        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
    }
}
