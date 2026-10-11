using Agent.Core.Skills;
using Agent.Core.Workflows;

namespace Agent.Core.Automation;

/// <summary>
/// Teach-by-Demonstration Recorder (#33): records demonstrated user actions,
/// abstracts concrete local directory paths into portable ${sourceDir} / ${outputDir}
/// workflow variables, and compiles them into a reusable WorkflowDefinition.
/// </summary>
public sealed class WorkflowActionRecorder
{
    private readonly SkillRegistry _registry;
    private readonly List<WorkflowStepDefinition> _recordedSteps = [];
    private bool _isRecording;

    public WorkflowActionRecorder(SkillRegistry? registry = null)
    {
        _registry = registry ?? SkillRegistry.CreateDefault();
    }

    public bool IsRecording => _isRecording;
    public IReadOnlyList<WorkflowStepDefinition> RecordedSteps => _recordedSteps;

    public void StartRecording()
    {
        _recordedSteps.Clear();
        _isRecording = true;
    }

    public void StopRecording() => _isRecording = false;

    public void RecordSkillDemonstration(
        string skillId,
        IReadOnlyDictionary<string, string> observedParameters,
        string? baselineSourceDir = null,
        string? baselineOutputDir = null)
    {
        if (!_isRecording)
        {
            throw new InvalidOperationException("ضبط‌کنندهٔ مراحل کار در حال حاضر فعال نیست.");
        }

        if (!_registry.IsAllowedSkill(skillId))
        {
            throw new InvalidOperationException($"مهارت '{skillId}' در رجیستری مجاز نیست.");
        }

        var parameterized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in observedParameters)
        {
            var val = v ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(baselineSourceDir))
            {
                val = val.Replace(baselineSourceDir, "${sourceDir}", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(baselineOutputDir))
            {
                val = val.Replace(baselineOutputDir, "${outputDir}", StringComparison.OrdinalIgnoreCase);
            }

            parameterized[k] = val;
        }

        var stepNumber = _recordedSteps.Count + 1;
        _recordedSteps.Add(new WorkflowStepDefinition($"s{stepNumber}", skillId, parameterized));
    }

    public WorkflowDefinition CompileWorkflow(string workflowId, string title)
    {
        if (_recordedSteps.Count == 0)
        {
            throw new InvalidOperationException("هیچ گامی برای تبدیل به گردش‌کار ضبط نشده است.");
        }

        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = title,
            ["en"] = title,
            ["zh"] = title,
            ["es"] = title,
            ["ar"] = title,
            ["fr"] = title
        };

        return new WorkflowDefinition(
            Id: workflowId,
            Version: "1.0",
            Category: "Recorded",
            Titles: titles,
            Descriptions: titles,
            Steps: _recordedSteps.ToArray());
    }
}
