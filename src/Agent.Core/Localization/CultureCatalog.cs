using System.Globalization;

namespace Agent.Core.Localization;

public sealed record SupportedLanguage(
    string Code,
    string NativeName,
    string EnglishName,
    string CultureName,
    bool IsRightToLeft,
    string RecommendedFontFamily);

/// <summary>
/// Provides metadata and culture-aware formatting for Persian and the five most
/// widely used international languages (English, Simplified Chinese, Spanish,
/// Arabic, and French).
/// </summary>
public static class CultureCatalog
{
    public const string DefaultLanguageCode = "fa";

    private static readonly SupportedLanguage[] OrderedLanguages =
    [
        new("fa", "فارسی", "Persian", "fa-IR", IsRightToLeft: true, "Vazirmatn, Segoe UI, Tahoma"),
        new("en", "English", "English", "en-US", IsRightToLeft: false, "Segoe UI Variable Text, Segoe UI, Arial"),
        new("zh", "中文 (简体)", "Chinese (Simplified)", "zh-CN", IsRightToLeft: false, "Microsoft YaHei UI, Noto Sans SC, Segoe UI"),
        new("es", "Español", "Spanish", "es-ES", IsRightToLeft: false, "Segoe UI Variable Text, Segoe UI, Arial"),
        new("ar", "العربية", "Arabic", "ar-SA", IsRightToLeft: true, "Segoe UI, Tahoma, Arial"),
        new("fr", "Français", "French", "fr-FR", IsRightToLeft: false, "Segoe UI Variable Text, Segoe UI, Arial")
    ];

    private static readonly Dictionary<string, SupportedLanguage> ByCode =
        OrderedLanguages.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SupportedLanguage> Languages => OrderedLanguages;

    public static SupportedLanguage GetLanguage(string? languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        return ByCode.TryGetValue(normalized, out var language)
            ? language
            : ByCode[DefaultLanguageCode];
    }

    public static bool IsSupported(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        var normalized = NormalizeLanguageCode(languageCode);
        return ByCode.ContainsKey(normalized);
    }

    public static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguageCode;
        }

        var trimmed = languageCode.Trim();
        var dashIndex = trimmed.IndexOfAny(['-', '_']);
        var primary = dashIndex > 0 ? trimmed[..dashIndex] : trimmed;
        return ByCode.ContainsKey(primary) ? primary.ToLowerInvariant() : DefaultLanguageCode;
    }

    public static CultureInfo GetCultureInfo(string? languageCode)
    {
        var language = GetLanguage(languageCode);
        try
        {
            return CultureInfo.GetCultureInfo(language.CultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    public static string FormatNumber(long value, string? languageCode = null)
    {
        var culture = GetCultureInfo(languageCode);
        return value.ToString("N0", culture);
    }

    public static string FormatPercent(double percentValue, string? languageCode = null)
    {
        var culture = GetCultureInfo(languageCode);
        var clamped = Math.Clamp(percentValue, 0d, 100d);
        return $"{clamped.ToString("0.#", culture)}%";
    }

    public static string FormatByteSize(long bytes, string? languageCode = null)
    {
        var lang = NormalizeLanguageCode(languageCode);
        var culture = GetCultureInfo(lang);
        var safeBytes = Math.Max(0L, bytes);

        string[] units = lang switch
        {
            "fa" => ["بایت", "کیلوبایت", "مگابایت", "گیگابایت", "ترابایت"],
            "ar" => ["بايت", "ك.بايت", "م.بايت", "غ.بايت", "ت.بايت"],
            "zh" => ["B", "KiB", "MiB", "GiB", "TiB"],
            "fr" => ["o", "Kio", "Mio", "Gio", "Tio"],
            _ => ["B", "KiB", "MiB", "GiB", "TiB"]
        };

        double value = safeBytes;
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        var formatted = unitIndex == 0
            ? value.ToString("N0", culture)
            : value.ToString("0.##", culture);

        return $"{formatted} {units[unitIndex]}";
    }

    public static string FormatTimestamp(DateTimeOffset timestamp, string? languageCode = null)
    {
        var culture = GetCultureInfo(languageCode);
        return timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm", culture);
    }
}
