using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Skills;
using Agent.Core.Workflows;
using System.Collections.ObjectModel;

namespace Agent.Desktop.ViewModels;

/// <summary>
/// No-Code Workflow Composer ViewModel (#44) enabling users to compose multi-step
/// pipelines from the 18 skills or pick from the 20 SMB Playbooks (#47), preview
/// the End-to-End Dry-Run (#42), execute with transactional rollback (#43), and
/// export/import .agentflow.json (#50).
/// </summary>
public sealed class WorkflowComposerViewModel : ObservableObject
{
    private readonly SkillRegistry _registry;
    private readonly WorkflowEngine _engine;

    private string _languageCode = CultureCatalog.DefaultLanguageCode;
    private string _workflowId = "custom.workflow";
    private string _workflowTitle = "گردش‌کار سفارشی";
    private string _sourceDirectory = string.Empty;
    private string _outputDirectory = string.Empty;
    private string _statusMessage = string.Empty;
    private WorkflowPreviewPlan? _currentPreviewPlan;
    private WorkflowExecutionResult? _lastExecutionResult;

    public WorkflowComposerViewModel(SkillRegistry? registry = null, WorkflowEngine? engine = null)
    {
        _registry = registry ?? SkillRegistry.CreateDefault();
        _engine = engine ?? new WorkflowEngine(_registry);
    }

    public ObservableCollection<WorkflowStepDefinition> Steps { get; } = [];

    public IReadOnlyList<SkillDescriptor> AvailableSkills => _registry.Descriptors;
    public IReadOnlyList<WorkflowDefinition> BuiltInPlaybooks => PlaybookCatalog.All;

    public string LanguageCode
    {
        get => _languageCode;
        set => SetProperty(ref _languageCode, CultureCatalog.NormalizeLanguageCode(value));
    }

    public string WorkflowId
    {
        get => _workflowId;
        set => SetProperty(ref _workflowId, value);
    }

    public string WorkflowTitle
    {
        get => _workflowTitle;
        set => SetProperty(ref _workflowTitle, value);
    }

    public string SourceDirectory
    {
        get => _sourceDirectory;
        set => SetProperty(ref _sourceDirectory, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public WorkflowPreviewPlan? CurrentPreviewPlan
    {
        get => _currentPreviewPlan;
        private set => SetProperty(ref _currentPreviewPlan, value);
    }

    public WorkflowExecutionResult? LastExecutionResult
    {
        get => _lastExecutionResult;
        private set => SetProperty(ref _lastExecutionResult, value);
    }

    public void LoadPlaybook(string playbookId)
    {
        var pb = PlaybookCatalog.GetById(playbookId);
        WorkflowId = pb.Id;
        WorkflowTitle = pb.GetTitle(LanguageCode);
        Steps.Clear();
        foreach (var step in pb.Steps)
        {
            Steps.Add(step);
        }

        CurrentPreviewPlan = null;
        StatusMessage = $"قالب آمادهٔ «{WorkflowTitle}» با {Steps.Count} گام بارگذاری شد.";
    }

    public void AddStep(string skillId, IReadOnlyDictionary<string, string>? parameters = null, WorkflowStepCondition? condition = null)
    {
        if (!_registry.IsAllowedSkill(skillId))
        {
            throw new InvalidOperationException($"مهارت '{skillId}' در رجیستری مجاز نیست.");
        }

        var stepNumber = Steps.Count + 1;
        var defaultParams = parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceDir"] = "${sourceDir}",
            ["outputDir"] = "${outputDir}",
            ["extension"] = "*"
        };

        Steps.Add(new WorkflowStepDefinition($"s{stepNumber}", skillId, defaultParams, condition));
        CurrentPreviewPlan = null;
    }

    public void MoveStepUp(int index)
    {
        if (index <= 0 || index >= Steps.Count)
        {
            return;
        }

        var item = Steps[index];
        Steps.RemoveAt(index);
        Steps.Insert(index - 1, item);
        CurrentPreviewPlan = null;
    }

    public WorkflowDefinition BuildDefinition() =>
        new(
            Id: WorkflowId,
            Version: "1.0",
            Category: "Custom",
            Titles: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [LanguageCode] = WorkflowTitle, ["fa"] = WorkflowTitle },
            Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [LanguageCode] = WorkflowTitle, ["fa"] = WorkflowTitle },
            Steps: Steps.ToArray());

    public WorkflowPreviewPlan RunDryRunPreview()
    {
        var def = BuildDefinition();
        var inputs = new WorkflowRuntimeInputs(SourceDirectory, OutputDirectory);
        CurrentPreviewPlan = _engine.PreviewWorkflow(def, inputs, LanguageCode);
        StatusMessage = $"پیش‌نمایش یکپارچه آماده شد: {CurrentPreviewPlan.ExecutableStepCount} گام فعال.";
        return CurrentPreviewPlan;
    }

    public async Task<WorkflowExecutionResult> ExecuteApprovedWorkflowAsync(ISkillContext context)
    {
        if (CurrentPreviewPlan is null)
        {
            throw new InvalidOperationException("ابتدا پیش‌نمایش یکپارچهٔ گردش‌کار (Dry-Run) را اجرا کنید.");
        }

        var def = BuildDefinition();
        var inputs = new WorkflowRuntimeInputs(SourceDirectory, OutputDirectory);
        LastExecutionResult = await _engine.ExecuteWorkflowAsync(def, CurrentPreviewPlan, inputs, context).ConfigureAwait(false);
        StatusMessage = LastExecutionResult.Succeeded
            ? $"گردش‌کار با موفقیت در {LastExecutionResult.ExecutedStepsCount} گام اجرا شد."
            : $"خطا در گردش‌کار ({LastExecutionResult.ErrorMessage}) — واگرد خودکار انجام شد.";
        return LastExecutionResult;
    }

    public string ExportAsJson() => WorkflowSerializer.ExportToJson(BuildDefinition());

    public void ImportFromJson(string json)
    {
        var imported = WorkflowSerializer.ImportFromJson(json, _registry);
        WorkflowId = imported.Id;
        WorkflowTitle = imported.GetTitle(LanguageCode);
        Steps.Clear();
        foreach (var step in imported.Steps)
        {
            Steps.Add(step);
        }

        CurrentPreviewPlan = null;
    }
}
