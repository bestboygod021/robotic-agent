using Agent.Core.AI;
using Agent.Core.Localization;
using System.Text;

namespace Agent.Core.Tools;

public sealed record CoachedPromptPackage(
    string OriginalGoal,
    string SafeEngineeredPrompt,
    int RedactedSensitiveItemsCount,
    IReadOnlyList<AiToolRecommendation> RecommendedTools);

/// <summary>
/// Interactive Prompt Coach for everyday users (addresses the 76% AI Novice barrier):
/// takes a plain-language goal in Persian or English, scrubs any accidental PII via
/// LocalAnonymizerProxy, constructs a structured professional prompt, and recommends
/// the best matching tools from the 200-tool catalog.
/// </summary>
public static class InteractivePromptCoach
{
    public static CoachedPromptPackage BuildCoachedPrompt(
        string userGoal,
        string rolePersona = "متخصص باتجربه / Expert Specialist",
        string outputFormat = "گام‌به‌گام و جدول‌بندی‌شده / Step-by-step & Structured",
        string languageCode = CultureCatalog.DefaultLanguageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userGoal);
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);

        var anonymizer = new LocalAnonymizerProxy();
        var session = anonymizer.Anonymize(userGoal.Trim());
        var safeGoal = session.AnonymizedText;

        var sb = new StringBuilder();
        if (lang == "fa")
        {
            sb.AppendLine($"# نقش (Role): شما یک {rolePersona} هستید.");
            sb.AppendLine($"# هدف (Objective): {safeGoal}");
            sb.AppendLine("# الزامات کیفیت (Constraints):");
            sb.AppendLine("- پاسخ را دقیق، کاربردی و بدون کلی‌گویی بنویسید.");
            sb.AppendLine("- اگر فرضی وجود دارد در ابتدا شفاف بیان کنید.");
            sb.AppendLine($"# قالب خروجی (Output Format): {outputFormat}");
        }
        else
        {
            sb.AppendLine($"# Role: Act as an {rolePersona}.");
            sb.AppendLine($"# Objective: {safeGoal}");
            sb.AppendLine("# Quality Constraints:");
            sb.AppendLine("- Provide a clear, actionable, and verifiable response.");
            sb.AppendLine("- State any key assumptions explicitly at the top.");
            sb.AppendLine($"# Output Format: {outputFormat}");
        }

        var recommended = AiToolMatchmaker.RecommendTools(userGoal, lang, maxResults: 3);
        return new CoachedPromptPackage(
            OriginalGoal: userGoal,
            SafeEngineeredPrompt: sb.ToString().Trim(),
            RedactedSensitiveItemsCount: session.RedactedTokenCount,
            RecommendedTools: recommended);
    }
}
