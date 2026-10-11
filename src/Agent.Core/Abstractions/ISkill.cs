using Agent.Core.Localization;

namespace Agent.Core.Abstractions;

public enum SkillRiskLevel
{
    ReadOnly,
    BoundedWriteNoOverwrite,
    ReversibleMutation,
    HighImpactRequiresExtraConfirmation
}

public sealed record SkillDescriptor(
    string Id,
    IReadOnlyDictionary<string, string> DisplayNames,
    IReadOnlyDictionary<string, string> Descriptions,
    SkillRiskLevel RiskLevel,
    bool RequiresPreviewApproval = true,
    bool SupportsRollback = false,
    bool WorksOffline = true)
{
    public string GetDisplayName(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (DisplayNames.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        if (DisplayNames.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        if (DisplayNames.TryGetValue(CultureCatalog.DefaultLanguageCode, out var persian) && !string.IsNullOrWhiteSpace(persian))
        {
            return persian;
        }

        return Id;
    }

    public string GetDescription(string? languageCode = null)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (Descriptions.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        if (Descriptions.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        if (Descriptions.TryGetValue(CultureCatalog.DefaultLanguageCode, out var persian) && !string.IsNullOrWhiteSpace(persian))
        {
            return persian;
        }

        return string.Empty;
    }
}

/// <summary>
/// Non-generic base contract for registered skills in the Robotic Agent ecosystem.
/// Every skill must expose metadata and enforce preview-before-execution semantics.
/// </summary>
public interface ISkill
{
    SkillDescriptor Descriptor { get; }
}

/// <summary>
/// Strongly-typed skill contract enforcing mandatory preview and governed execution.
/// </summary>
public interface ISkill<in TRequest, TPreview, TResult> : ISkill
{
    TPreview Preview(TRequest request);

    Task<TResult> ExecuteAsync(
        TRequest request,
        TPreview approvedPreview,
        ISkillContext context);
}
