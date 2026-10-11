using Agent.Core.Skills;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agent.Core.Workflows;

/// <summary>
/// Exports and imports workflows as portable .agentflow.json files (#50)
/// with strict security validation against SkillRegistry and hardcoded path injection.
/// </summary>
public static class WorkflowSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Regex AbsoluteDriveOrRootPattern = new(
        @"^([a-zA-Z]:[\\/]|\\\\|/etc/|/bin/|/usr/|/var/|/proc/|/sys/)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string ExportToJson(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ValidateWorkflowSecurity(workflow, SkillRegistry.CreateDefault());
        return JsonSerializer.Serialize(workflow, JsonOptions);
    }

    public static WorkflowDefinition ImportFromJson(string json, SkillRegistry? registry = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("محتوای JSON گردش‌کار خالی است.", nameof(json));
        }

        var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions)
            ?? throw new InvalidOperationException("ساختار JSON گردش‌کار نامعتبر است.");

        ValidateWorkflowSecurity(workflow, registry ?? SkillRegistry.CreateDefault());
        return workflow;
    }

    public static void ValidateWorkflowSecurity(WorkflowDefinition workflow, SkillRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(registry);

        if (string.IsNullOrWhiteSpace(workflow.Id))
        {
            throw new InvalidOperationException("شناسهٔ گردش‌کار نمی‌تواند خالی باشد.");
        }

        if (workflow.Steps is null || workflow.Steps.Count == 0 || workflow.Steps.Count > WorkflowEngine.MaximumStepsPerWorkflow)
        {
            throw new InvalidOperationException($"تعداد گام‌های گردش‌کار باید بین ۱ تا {WorkflowEngine.MaximumStepsPerWorkflow} باشد.");
        }

        foreach (var step in workflow.Steps)
        {
            if (!registry.IsAllowedSkill(step.SkillId))
            {
                throw new InvalidOperationException($"مهارت '{step.SkillId}' در لیست مجاز رجیستری قرار ندارد.");
            }

            foreach (var (paramKey, paramVal) in step.Parameters)
            {
                if (string.IsNullOrWhiteSpace(paramVal))
                {
                    continue;
                }

                if (paramVal.Contains("..", StringComparison.Ordinal) ||
                    AbsoluteDriveOrRootPattern.IsMatch(paramVal.Trim()))
                {
                    throw new InvalidOperationException(
                        $"پارامتر '{paramKey}' در گام '{step.StepId}' حاوی مسیر مطلق یا پیمایش مسیر غیرمجاز است. در قالب‌های قابل اشتراک‌گذاری فقط از متغیرهای امن مانند ${{sourceDir}} و ${{outputDir}} استفاده کنید.");
                }
            }
        }
    }
}
