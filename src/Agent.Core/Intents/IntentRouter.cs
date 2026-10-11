using Agent.Core.Localization;
using System.Text.RegularExpressions;

namespace Agent.Core.Intents;

/// <summary>
/// Routes one supported task through explicit multilingual rules (Persian,
/// English, Simplified Chinese, Spanish, Arabic, and French) and a tiny local
/// classifier. It only proposes file copying; the execution layer still previews,
/// validates, and asks for confirmation.
/// </summary>
public sealed class IntentRouter
{
    private const double MinimumInferredConfidence = 0.82;
    private const double MinimumInferredMargin = 0.20;
    private const int MaximumRequestLength = 512;

    private static readonly Regex ExplicitExtension = new(
        @"(?:پسوند\s+|extension\s+|extensión\s+|extension\s+|امتداد\s+|后缀\s*|\.)([a-z0-9]{1,12})(?![a-z0-9])",
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
        "move", "delete", "remove", "erase",
        "احذف", "امسح", "انقل",
        "eliminar", "borrar", "mover", "trasladar",
        "supprimer", "effacer", "deplacer", "déplacer",
        "删除", "清空", "移除", "移动", "转移"
    ];

    private static readonly string[] NegativeCopyPhrases =
    [
        "کپی نکن", "کپی نکنم", "کپی نکنید", "نسخه نگیر", "نسخه نگیرم",
        "do not copy", "don't copy", "no copy", "never copy",
        "do not make a copy", "don't make a copy", "do not duplicate", "don't duplicate",
        "do not back up", "do not backup", "don't back up", "don't backup", "no backup",
        "کپی نمی خواهم", "کپی نمیخوام", "نسخه تهیه نکن", "نسخه نساز", "تکثیر نکن",
        "بکاپ نگیر", "پشتیبان نگیر",
        "لا تنسخ", "عدم نسخ",
        "no copiar", "no copies", "no duplicar", "no hagas copia",
        "ne pas copier", "ne copie pas", "ne duplique pas",
        "不要复制", "别复制", "不复制", "不要备份"
    ];

    private static readonly string[] ExplicitCopyKeywords =
    [
        "کپی", "copy", "انسخ", "نسخ", "copiar", "copie", "copier", "复制", "拷贝"
    ];

    private static readonly string[] CopyActionMarkers =
    [
        "کپی", "copy", "نسخه بگیر", "نسخه بردار", "نسخه تهیه", "نسخه بساز",
        "یک کپی", "یه کپی", "duplicate", "make a copy", "backup", "بکاپ", "تکثیر", "clone",
        "انسخ", "نسخ", "نسخه احتیاطیه", "نسخ احتیاطی",
        "copiar", "duplicar", "haz una copia", "respaldar",
        "copier", "dupliquer", "fais une copie", "sauvegarder",
        "复制", "拷贝", "备份"
    ];

    private static readonly string[] FileContextMarkers =
    [
        "فایل", "پوشه", "عکس", "تصویر", "سند", "مدرک", "pdf", "document", "file", "folder", "photo", "picture",
        "ملف", "ملفات", "مجلد", "مستند", "صور",
        "archivo", "archivos", "carpeta", "documento", "documentos", "foto",
        "fichier", "fichiers", "dossier", "document", "documents",
        "文件", "文件夹", "文档", "图片", "照片"
    ];

    private static readonly string[] AllFilesMarkers =
    [
        "همه فایل", "تمام فایل", "all files", "all of them", "everything", "*.*", "*",
        "جمیع الملفات", "کل الملفات",
        "todos los archivos", "todo",
        "tous les fichiers", "tout",
        "所有文件", "全部文件"
    ];

    private static readonly string[] SubfolderMarkers =
    [
        "زیرپوشه", "زیر پوشه", "subfolder", "subfolders", "subdirectory", "subdirectories", "recursive",
        "مجلدات فرعیه", "المجلدات الفرعیه",
        "subcarpeta", "subcarpetas", "subdirectorio",
        "sous-dossier", "sous-dossiers", "sous dossier",
        "子文件夹", "子目录"
    ];

    private static readonly Regex MinSizePattern = new(
        @"(?:بزرگتر از|بزرگ تر از|بیشتر از|حداقل|larger than|greater than|more than|over|min|اكبر من|أكبر من|mayor de|mas de|plus de|大于|超过)\s*(\d+(?:\.\d+)?)\s*(kb|kib|کیلوبایت|كيلوبايت|ko|mb|mib|مگابایت|ميجابايت|mo|gb|gib|گیگابایت|جيجابايت|go)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MaxSizePattern = new(
        @"(?:کوچکتر از|کوچک تر از|کمتر از|حداکثر|smaller than|less than|under|max|اصغر من|أصغر من|اقل من|menor de|menos de|moins de|小于|低于)\s*(\d+(?:\.\d+)?)\s*(kb|kib|کیلوبایت|كيلوبايت|ko|mb|mib|مگابایت|ميجابايت|mo|gb|gib|گیگابایت|جيجابايت|go)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

        if (ContainsAny(normalized, NegativeCopyPhrases))
        {
            return new(IntentStatus.Unsupported, null, "درخواست منفی به عملیات تبدیل نمی‌شود.");
        }

        if (ContainsAny(normalized, UnsupportedVerbs))
        {
            return new(IntentStatus.Unsupported, null, "این نسخه فقط کپی فایل را اجرا می‌کند؛ حذف و جابه‌جایی در دسترس نیست.");
        }

        var hasExplicitCopy = ContainsAny(normalized, ExplicitCopyKeywords);
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

        var criteria = ExtractFilterCriteria(normalized);

        var explicitMatch = ExplicitExtension.Match(normalized);
        if (explicitMatch.Success)
        {
            var ext = explicitMatch.Groups[1].Value.ToLowerInvariant();
            return new(IntentStatus.Ready, ext, message, prediction.Confidence, !hasExplicitCopy, criteria);
        }

        if (criteria.Extensions.Count > 0)
        {
            return new(IntentStatus.Ready, criteria.Extensions[0], message, prediction.Confidence, !hasExplicitCopy, criteria);
        }

        if (ContainsAny(normalized, AllFilesMarkers))
        {
            return new(IntentStatus.Ready, "*", message, prediction.Confidence, !hasExplicitCopy, criteria with { Extensions = ["*"] });
        }

        return new(
            IntentStatus.NeedsClarification,
            null,
            "پسوند فایل را مشخص کنید (مثلاً pdf)، یا برای همهٔ فایل‌ها * بنویسید.",
            prediction.Confidence,
            !hasExplicitCopy,
            criteria);
    }

    public FileFilterCriteria ExtractFilterCriteria(string? request)
    {
        var normalized = NormalizePersian(request ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return FileFilterCriteria.Empty;
        }

        var matchedExtensions = new List<string>();
        foreach (Match match in ExplicitExtension.Matches(normalized))
        {
            var ext = match.Groups[1].Value.ToLowerInvariant();
            if (!matchedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                matchedExtensions.Add(ext);
            }
        }

        foreach (var extension in KnownExtensions)
        {
            if (Regex.IsMatch(
                normalized,
                $@"(?<![a-z0-9]){Regex.Escape(extension)}(?![a-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (!matchedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    matchedExtensions.Add(extension);
                }
            }
        }

        long? minSize = ParseSizeMatch(MinSizePattern.Match(normalized));
        long? maxSize = ParseSizeMatch(MaxSizePattern.Match(normalized));
        int? modifiedDays = ParseModifiedDays(normalized);
        var includeSubfolders = ContainsAny(normalized, SubfolderMarkers);

        return new FileFilterCriteria(
            Extensions: matchedExtensions,
            MinSizeBytes: minSize,
            MaxSizeBytes: maxSize,
            ModifiedWithinDays: modifiedDays,
            IncludeSubdirectories: includeSubfolders);
    }

    private static long? ParseSizeMatch(Match match)
    {
        if (!match.Success ||
            !double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var unit = match.Groups[2].Value.ToLowerInvariant();
        var multiplier = unit switch
        {
            "kb" or "kib" or "کیلوبایت" or "كيلوبايت" or "ko" => 1024L,
            "mb" or "mib" or "مگابایت" or "ميجابايت" or "mo" => 1024L * 1024L,
            "gb" or "gib" or "گیگابایت" or "جيجابايت" or "go" => 1024L * 1024L * 1024L,
            _ => 1L
        };

        return (long)Math.Round(amount * multiplier);
    }

    private static int? ParseModifiedDays(string normalized)
    {
        if (ContainsAny(normalized, ["امروز", "today", "الیوم", "hoy", "aujourd'hui", "今天"]))
        {
            return 1;
        }

        if (ContainsAny(normalized, ["هفته گذشته", "هفته پیش", "last week", "past week", "7 days", "الاسبوع الماضی", "semana pasada", "semaine derniere", "上周", "最近7天"]))
        {
            return 7;
        }

        if (ContainsAny(normalized, ["ماه گذشته", "ماه پیش", "last month", "past month", "30 days", "الشهر الماضی", "mes pasado", "mois dernier", "上个月", "最近30天"]))
        {
            return 30;
        }

        return null;
    }

    public static string NormalizePersian(string value) =>
        TextNormalizer.NormalizeMultilingual(value);

    private static bool ContainsAny(string value, IEnumerable<string> candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
