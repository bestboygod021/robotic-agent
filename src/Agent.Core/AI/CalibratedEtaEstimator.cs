using System.Text.RegularExpressions;

namespace Agent.Core.AI;

public sealed record TokenAndCostEstimate(
    int PromptTokens,
    int MaxCompletionTokens,
    int TotalTokens,
    decimal EstimatedCostUsd);

/// <summary>
/// Calibrated ETA & Token Estimator (#57): estimates token counts across Latin,
/// Persian/Arabic, and CJK scripts, computes estimated cloud cost in USD, and
/// produces calibrated TaskDurationEstimate objects based on local hardware speed.
/// </summary>
public sealed class CalibratedEtaEstimator
{
    private static readonly Regex CjkCharPattern = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public TokenAndCostEstimate EstimateTokensAndCost(
        string promptText,
        int expectedStepCount = 2,
        decimal inputPricePerMillionTokensUsd = 0.25m,
        decimal outputPricePerMillionTokensUsd = 1.00m)
    {
        var promptTokens = EstimateTokens(promptText);
        var completionTokens = Math.Clamp(expectedStepCount * 85, 64, 1024);
        var totalTokens = promptTokens + completionTokens;

        var cost = ((promptTokens / 1_000_000m) * inputPricePerMillionTokensUsd) +
                   ((completionTokens / 1_000_000m) * outputPricePerMillionTokensUsd);

        return new TokenAndCostEstimate(
            PromptTokens: promptTokens,
            MaxCompletionTokens: completionTokens,
            TotalTokens: totalTokens,
            EstimatedCostUsd: decimal.Round(cost, 6));
    }

    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var cjkCount = CjkCharPattern.Matches(text).Count;
        var nonCjkChars = Math.Max(0, text.Length - cjkCount);
        var approx = (int)Math.Ceiling((nonCjkChars / 3.5) + (cjkCount * 1.2));
        return Math.Max(1, approx);
    }

    public TaskDurationEstimate EstimateLocalModelDuration(
        AiPlanningRequest request,
        int logicalProcessorCount = 4,
        bool isWarmModel = true)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tokens = EstimateTokens(request.UserRequest) + (request.AllowedSkills.Count * 18);
        var effectiveCores = Math.Clamp(logicalProcessorCount, 1, 16);
        var tokensPerSecond = 14.0 * Math.Sqrt(effectiveCores);

        var coldStartSeconds = isWarmModel ? 0.15 : 1.5;
        var typicalSeconds = Math.Max(0.25, coldStartSeconds + (tokens / tokensPerSecond));
        var bestSeconds = Math.Max(0.10, typicalSeconds * 0.65);
        var worstSeconds = Math.Max(typicalSeconds + 0.5, typicalSeconds * 1.85);

        return new TaskDurationEstimate(
            BestCase: TimeSpan.FromSeconds(bestSeconds),
            Typical: TimeSpan.FromSeconds(typicalSeconds),
            WorstCase: TimeSpan.FromSeconds(worstSeconds),
            Confidence: isWarmModel ? 0.85 : 0.65,
            Basis: $"Calibrated local SLM ({effectiveCores} cores, ~{tokensPerSecond:F0} tok/s)");
    }
}
