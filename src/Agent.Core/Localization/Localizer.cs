using System.Text.Json;

namespace Agent.Core.Localization;

/// <summary>
/// Thread-safe localization provider supporting Persian and the top five global
/// languages (English, Simplified Chinese, Spanish, Arabic, and French) with
/// automatic fallback and live change notification.
/// </summary>
public sealed class Localizer
{
    private const string StringsResource = "Agent.Core.Localization.Data.ui-strings.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ResourceTable = LoadResourceTable();

    private readonly object _sync = new();
    private string _currentLanguageCode;

    public Localizer(string initialLanguageCode = CultureCatalog.DefaultLanguageCode)
    {
        _currentLanguageCode = CultureCatalog.NormalizeLanguageCode(initialLanguageCode);
    }

    public static Localizer Shared { get; } = new();

    public event EventHandler<string>? LanguageChanged;

    public string CurrentLanguageCode
    {
        get
        {
            lock (_sync)
            {
                return _currentLanguageCode;
            }
        }
    }

    public SupportedLanguage CurrentLanguage => CultureCatalog.GetLanguage(CurrentLanguageCode);

    public static IReadOnlyList<string> Keys => ResourceTable.Keys.ToArray();

    public void SetLanguage(string? languageCode)
    {
        var normalized = CultureCatalog.NormalizeLanguageCode(languageCode);
        bool changed;
        lock (_sync)
        {
            changed = !string.Equals(_currentLanguageCode, normalized, StringComparison.OrdinalIgnoreCase);
            _currentLanguageCode = normalized;
        }

        if (changed)
        {
            LanguageChanged?.Invoke(this, normalized);
        }
    }

    public string Get(string key, string? languageCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode ?? CurrentLanguageCode);

        if (ResourceTable.TryGetValue(key, out var translations))
        {
            if (translations.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }

            if (translations.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
            {
                return english;
            }

            if (translations.TryGetValue(CultureCatalog.DefaultLanguageCode, out var persian) && !string.IsNullOrWhiteSpace(persian))
            {
                return persian;
            }
        }

        return key;
    }

    public static bool HasAllSupportedLanguagesForEveryKey()
    {
        foreach (var (_, translations) in ResourceTable)
        {
            foreach (var language in CultureCatalog.Languages)
            {
                if (!translations.TryGetValue(language.Code, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }
            }
        }

        return ResourceTable.Count > 0;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadResourceTable()
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream(StringsResource)
            ?? throw new InvalidOperationException("فایل رشته‌های چندزبانه در بستهٔ برنامه پیدا نشد.");

        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("جدول رشته‌های چندزبانه خالی است.");

        var normalized = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, map) in raw)
        {
            normalized[key] = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }

        return normalized;
    }
}
