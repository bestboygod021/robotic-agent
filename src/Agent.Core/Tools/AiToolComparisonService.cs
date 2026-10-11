using Agent.Core.Localization;

namespace Agent.Core.Tools;

public sealed record AiToolComparisonEntry(
    string ToolId,
    string Name,
    string CategoryName,
    string Description,
    string FreeTierStatus,
    string PrivacyPosture,
    string OpenWeightsStatus,
    string AccessUrlKind,
    string StarterPrompt,
    string LoginUrl);

public sealed record AiToolComparisonMatrix(
    string LanguageCode,
    IReadOnlyList<AiToolComparisonEntry> Entries);

/// <summary>
/// Builds a structured side-by-side comparison matrix for 2 to 4 tools from the
/// AI catalog in any of the 6 supported languages.
/// </summary>
public sealed class AiToolComparisonService
{
    public const int MinimumComparisonCount = 2;
    public const int MaximumComparisonCount = 4;

    public AiToolComparisonMatrix Compare(
        IEnumerable<AiTool> selectedTools,
        string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(selectedTools);

        var distinctTools = selectedTools
            .Where(tool => tool is not null)
            .DistinctBy(tool => tool.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctTools.Length < MinimumComparisonCount || distinctTools.Length > MaximumComparisonCount)
        {
            throw new InvalidOperationException(
                $"برای مقایسهٔ رودررو، بین {MinimumComparisonCount} تا {MaximumComparisonCount} ابزار متفاوت انتخاب کنید.");
        }

        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        var entries = distinctTools
            .Select(tool => new AiToolComparisonEntry(
                ToolId: tool.Id,
                Name: tool.Name,
                CategoryName: tool.GetLocalizedCategoryName(lang),
                Description: tool.GetLocalizedDescription(lang),
                FreeTierStatus: FormatYesNo(tool.HasFreeTier, lang),
                PrivacyPosture: tool.GetPrivacyBadgeLabel(lang),
                OpenWeightsStatus: FormatYesNo(tool.SupportsOfflineOrOpenWeights, lang),
                AccessUrlKind: tool.GetLaunchButtonLabel(lang),
                StarterPrompt: tool.GetPromptTemplate(lang),
                LoginUrl: tool.LoginUrl))
            .ToArray();

        return new AiToolComparisonMatrix(lang, entries);
    }

    private static string FormatYesNo(bool value, string languageCode) =>
        (value, languageCode) switch
        {
            (true, "fa") => "دارد ✓",
            (false, "fa") => "ندارد / پولی",
            (true, "ar") => "متاح ✓",
            (false, "ar") => "غير متاح / مدفوع",
            (true, "zh") => "支持 ✓",
            (false, "zh") => "仅付费 / 不支持",
            (true, "es") => "Sí ✓",
            (false, "es") => "No / De pago",
            (true, "fr") => "Oui ✓",
            (false, "fr") => "Non / Payant",
            (true, _) => "Yes ✓",
            (false, _) => "No / Paid Only"
        };
}
