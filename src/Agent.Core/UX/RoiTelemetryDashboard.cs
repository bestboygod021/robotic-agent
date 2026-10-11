using Agent.Core.Storage;

namespace Agent.Core.UX;

public sealed record RoiDashboardSummary(
    int TotalCompletedOperations,
    int TotalItemsAutomated,
    double EstimatedMinutesSaved,
    double EstimatedHoursSaved,
    decimal EstimatedMonetaryValueSavedUsd);

/// <summary>
/// 100% Local ROI &amp; Time-Saved Telemetry Dashboard (#84): calculates total
/// administrative time and monetary value saved from LocalHistoryStore entries
/// (calibrated against SBA/McKinsey 3.1 hrs/day repetitive task benchmarks).
/// </summary>
public static class RoiTelemetryDashboard
{
    public const double DefaultSecondsSavedPerAutomatedFile = 28.0; // ~28s saved per file/invoice/doc
    public const decimal DefaultHourlyLaborRateUsd = 25.0m;

    public static RoiDashboardSummary ComputeSummary(
        IReadOnlyList<HistoryEntry> historyEntries,
        double secondsSavedPerItem = DefaultSecondsSavedPerAutomatedFile,
        decimal hourlyRateUsd = DefaultHourlyLaborRateUsd)
    {
        ArgumentNullException.ThrowIfNull(historyEntries);

        var completedOps = historyEntries.Where(e => !e.Cancelled).ToArray();
        var totalItems = completedOps.Sum(e => Math.Max(0, e.Copied));
        var totalSeconds = totalItems * Math.Max(1.0, secondsSavedPerItem);
        var minutesSaved = Math.Round(totalSeconds / 60.0, 2);
        var hoursSaved = Math.Round(totalSeconds / 3600.0, 2);
        var valueUsd = decimal.Round((decimal)(totalSeconds / 3600.0) * hourlyRateUsd, 2);

        return new RoiDashboardSummary(
            TotalCompletedOperations: completedOps.Length,
            TotalItemsAutomated: totalItems,
            EstimatedMinutesSaved: minutesSaved,
            EstimatedHoursSaved: hoursSaved,
            EstimatedMonetaryValueSavedUsd: valueUsd);
    }
}
