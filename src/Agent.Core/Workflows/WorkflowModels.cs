using Agent.Core.Localization;
using Agent.Core.Skills.Rollback;

namespace Agent.Core.Workflows;

public enum WorkflowConditionKind
{
    Always,
    PreviousCountAtLeast,
    SourceFileCountAtLeast,
    PreviousOutputExists
}

public sealed record WorkflowStepCondition(
    WorkflowConditionKind Kind = WorkflowConditionKind.Always,
    int Threshold = 1);

public sealed record WorkflowStepDefinition(
    string StepId,
    string SkillId,
    IReadOnlyDictionary<string, string> Parameters,
    WorkflowStepCondition? Condition = null,
    IReadOnlyDictionary<string, string>? StepTitles = null)
{
    public string GetTitle(string languageCode, string fallbackTitle)
    {
        var normalized = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (StepTitles is not null)
        {
            if (StepTitles.TryGetValue(normalized, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (StepTitles.TryGetValue(CultureCatalog.DefaultLanguageCode, out var faText) && !string.IsNullOrWhiteSpace(faText))
            {
                return faText;
            }
        }

        return fallbackTitle;
    }
}

public sealed record WorkflowDefinition(
    string Id,
    string Version,
    string Category,
    IReadOnlyDictionary<string, string> Titles,
    IReadOnlyDictionary<string, string> Descriptions,
    IReadOnlyList<WorkflowStepDefinition> Steps)
{
    public string GetTitle(string languageCode)
    {
        var normalized = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (Titles.TryGetValue(normalized, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return Titles.TryGetValue(CultureCatalog.DefaultLanguageCode, out var fallback) ? fallback : Id;
    }

    public string GetDescription(string languageCode)
    {
        var normalized = CultureCatalog.NormalizeLanguageCode(languageCode);
        if (Descriptions.TryGetValue(normalized, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return Descriptions.TryGetValue(CultureCatalog.DefaultLanguageCode, out var fallback) ? fallback : string.Empty;
    }
}

public sealed record WorkflowRuntimeInputs(
    string SourceDirectory,
    string OutputDirectory,
    string WorkingDirectory = "",
    IReadOnlyDictionary<string, string>? CustomVariables = null);

public sealed record WorkflowStepPreview(
    int StepNumber,
    string StepId,
    string SkillId,
    string DisplayTitle,
    bool WillExecute,
    string ConditionSummary,
    int EstimatedItemsCount,
    string PlannedOutputPath,
    IReadOnlyDictionary<string, string> ResolvedParameters);

public sealed record WorkflowPreviewPlan(
    string WorkflowId,
    string WorkflowTitle,
    IReadOnlyList<WorkflowStepPreview> Steps,
    bool IsValid,
    IReadOnlyList<string> Warnings)
{
    public int ExecutableStepCount => Steps.Count(s => s.WillExecute);
    public int TotalEstimatedItems => Steps.Where(s => s.WillExecute).Sum(s => s.EstimatedItemsCount);
}

public sealed record WorkflowStepExecutionRecord(
    int StepNumber,
    string StepId,
    string SkillId,
    bool Executed,
    int ProcessedCount,
    string OutputPath,
    OperationManifest? Manifest);

public sealed record WorkflowExecutionResult(
    string WorkflowId,
    bool Succeeded,
    bool WasRolledBack,
    string? ErrorMessage,
    IReadOnlyList<WorkflowStepExecutionRecord> StepRecords,
    IReadOnlyList<RollbackResult>? RollbackResults = null)
{
    public int ExecutedStepsCount => StepRecords.Count(r => r.Executed);
}
