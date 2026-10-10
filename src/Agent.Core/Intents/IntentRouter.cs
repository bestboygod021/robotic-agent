using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Intents;

/// <summary>
/// Routes one supported task through explicit Persian rules and a tiny local
/// classifier. It only proposes file copying; the execution layer still previews,
/// validates, and asks for confirmation.
/// </summary>
public sealed class IntentRouter
{
    private const double MinimumInferredConfidence = 0.82;
    private const double MinimumInferredMargin = 0.20;
    private const int MaximumRequestLength = 512;

    private static readonly Regex ExplicitExtension = new(
        @"(?:پسوند\s+|\.)([a-z0-9]{1,12})(?![a-z0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] KnownExtensions =
    [
        "pdf", "doc", "docx", "xls", "xlsx", "csv", "txt", "rtf", "md",
        "json", "xml", "html", "htm", "png", "jpg", "jpeg", "gif", "bmp",
        "zip", "7z", "mp3", "mp4", "wav", "log"
    ];

    private static readonly string[] UnsupportedVerbs =
    [
        "حذف", "پاک کن", "پاک‌سازی", "پاک سازی", "انتقال", "جابجا", "جابه جا",
        "move", "delete", "remove", "erase"
    ];

    private static readonly string[] NegativeCopyPhrases =
    [
        "کپی نکن", "کپی نکنم", "کپی نکنید", "نسخه نگیر", "نسخه نگیرم",
        "do not copy", "don't copy", "no copy", "never copy",
        "do not make a copy", "don't make a copy", "do not duplicate", "don't duplicate",
        "do not back up", "do not backup", "don't back up", "don't backup", "no backup",
        "کپی نمی خواهم", "کپی نمیخوام", "نسخه تهیه نکن", "نسخه نساز", "تکثیر نکن",
        "بکاپ نگیر", "پشتیبان نگیر"
    ];

    private static readonly string[] CopyActionMarkers =
    [
        "کپی", "copy", "نسخه بگیر", "نسخه بردار", "نسخه تهیه", "نسخه بساز",
        "یک کپی", "یه کپی", "duplicate", "make a copy", "backup", "بکاپ", "تکثیر", "clone"
    ];

    private static readonly string[] FileContextMarkers =
    [
        "فایل", "پوشه", "عکس", "تصویر", "سند", "مدرک", "pdf", "document", "file", "folder", "photo", "picture"
    ];

    private readonly LocalIntentClassifier _classifier;

    public IntentRouter(LocalIntentClassifier? classifier = null)
    {
        _classifier = classifier ?? new LocalIntentClassifier();
    }

    public IntentParseResult Parse(string? request)
    {
        if (request is not null && request.Length > MaximumRequestLength)
        {
            return new(IntentStatus.Unsupported, null, "درخواست خیلی طولانی است؛ لطفاً آن را کوتاه‌تر کنید.");
        }

        var normalized = NormalizePersian(request ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new(IntentStatus.Unsupported, null, "درخواست را بنویسید؛ فعلاً فقط کپی فایل پشتیبانی می‌شود.");
        }

        if (ContainsAny(normalized, UnsupportedVerbs))
        {
            return new(IntentStatus.Unsupported, null, "این نسخه فقط کپی فایل را اجرا می‌کند؛ حذف و جابه‌جایی در دسترس نیست.");
        }

        if (ContainsAny(normalized, NegativeCopyPhrases))
        {
            return new(IntentStatus.Unsupported, null, "درخواست منفی به عملیات تبدیل نمی‌شود.");
        }

        var hasExplicitCopy = ContainsAny(normalized, ["کپی", "copy"]);
        var hasCopyActionMarker = ContainsAny(normalized, CopyActionMarkers);
        if (!hasCopyActionMarker)
        {
            return new(IntentStatus.Unsupported, null, "برای جلوگیری از برداشت اشتباه، کپی فایل را صریح درخواست کنید.");
        }

        if (!hasExplicitCopy && !ContainsAny(normalized, FileContextMarkers))
        {
            return new(IntentStatus.Unsupported, null, "برای تشخیص امن، در درخواست به فایل یا پوشه اشاره کنید.");
        }

        var prediction = _classifier.Classify(normalized);
        if (!hasExplicitCopy &&
            (prediction.Label != IntentLabel.CopyFiles ||
             prediction.Confidence < MinimumInferredConfidence ||
             prediction.Margin < MinimumInferredMargin))
        {
            return new(
                IntentStatus.Unsupported,
                null,
                "دسته‌بند محلی از درخواست مطمئن نیست؛ برای جلوگیری از اجرای اشتباه، آن را با واژهٔ «کپی» روشن‌تر بنویسید.",
                prediction.Confidence,
                Inferred: true);
        }

        var message = hasExplicitCopy
            ? "عمل کپی شناخته شد. پیش‌نمایش فایل‌ها را پیش از اجرا بررسی کنید."
            : "دسته‌بند سبک محلی، درخواست را کپی فایل تشخیص داد؛ پیش‌نمایش را با دقت بررسی کنید.";

        var explicitMatch = ExplicitExtension.Match(normalized);
        if (explicitMatch.Success)
        {
            return new(IntentStatus.Ready, explicitMatch.Groups[1].Value.ToLowerInvariant(), message, prediction.Confidence, !hasExplicitCopy);
        }

        foreach (var extension in KnownExtensions)
        {
            if (Regex.IsMatch(
                normalized,
                $@"(?<![a-z0-9]){Regex.Escape(extension)}(?![a-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return new(IntentStatus.Ready, extension, message, prediction.Confidence, !hasExplicitCopy);
            }
        }

        if (ContainsAny(normalized, ["همه فایل", "تمام فایل", "all files", "all of them", "everything", "*.*", "*"]))
        {
            return new(IntentStatus.Ready, "*", message, prediction.Confidence, !hasExplicitCopy);
        }

        return new(
            IntentStatus.NeedsClarification,
            null,
            "پسوند فایل را مشخص کنید (مثلاً pdf)، یا برای همهٔ فایل‌ها * بنویسید.",
            prediction.Confidence,
            !hasExplicitCopy);
    }

    public static string NormalizePersian(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            // Arabic diacritics (including the hamza in «همهٔ») do not change intent.
            if ((character >= '\u064b' && character <= '\u065f') || character == '\u0670')
            {
                continue;
            }

            var normalized = character switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                '\u0640' => ' ', // tatweel
                '\u200c' or '\u200e' or '\u200f' => ' ',
                '۰' => '0', '۱' => '1', '۲' => '2', '۳' => '3', '۴' => '4',
                '۵' => '5', '۶' => '6', '۷' => '7', '۸' => '8', '۹' => '9',
                '٠' => '0', '١' => '1', '٢' => '2', '٣' => '3', '٤' => '4',
                '٥' => '5', '٦' => '6', '٧' => '7', '٨' => '8', '٩' => '9',
                _ => character
            };
            builder.Append(normalized);
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string value, IEnumerable<string> candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
