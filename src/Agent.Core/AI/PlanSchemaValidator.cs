using Agent.Core.Skills;
using System.Text.Json;

namespace Agent.Core.AI;

public sealed record PlanValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

/// <summary>
/// Strict Grammar/Schema-Constrained Plan Validator (#53): verifies that any
/// AiPlanProposal returned by a local or external LLM only invokes allowed
/// skills in SkillRegistry, satisfies required JSON schema properties, and
/// contains no shell/path injection.
/// </summary>
public sealed class PlanSchemaValidator
{
    public const int MaxAllowedSteps = 12;

    private static readonly string[] ForbiddenTokens =
    [
        "..", "cmd.exe", "powershell", "pwsh", "/bin/sh", "/bin/bash", "reg.exe", "rundll32"
    ];

    private readonly SkillRegistry _registry;

    public PlanSchemaValidator(SkillRegistry? registry = null)
    {
        _registry = registry ?? SkillRegistry.CreateDefault();
    }

    public PlanValidationResult Validate(
        AiPlanProposal proposal,
        IReadOnlyList<AiAllowedSkill>? allowedSkills = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(proposal.Summary))
        {
            errors.Add("خلاصهٔ طرح پیشنهادی هوش مصنوعی نمی‌تواند خالی باشد.");
        }

        if (proposal.Steps is null || proposal.Steps.Count == 0)
        {
            errors.Add("طرح پیشنهادی باید حداقل یک گام اجرایی داشته باشد.");
            return new PlanValidationResult(false, errors);
        }

        if (proposal.Steps.Count > MaxAllowedSteps)
        {
            errors.Add($"تعداد گام‌های طرح پیشنهادی ({proposal.Steps.Count}) بیش از سقف مجاز ({MaxAllowedSteps}) است.");
        }

        var allowedMap = allowedSkills?.ToDictionary(s => s.SkillId, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < proposal.Steps.Count; i++)
        {
            var step = proposal.Steps[i];
            if (string.IsNullOrWhiteSpace(step.SkillId) || !_registry.IsAllowedSkill(step.SkillId))
            {
                errors.Add($"گام {i + 1}: مهارت '{step.SkillId}' در SkillRegistry مجاز نیست.");
                continue;
            }

            if (allowedMap is not null && !allowedMap.ContainsKey(step.SkillId))
            {
                errors.Add($"گام {i + 1}: مهارت '{step.SkillId}' در لیست مهارت‌های مجاز این درخواست نیست.");
                continue;
            }

            if (step.Arguments.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"گام {i + 1}: آرگومان‌های مهارت '{step.SkillId}' باید یک شیء JSON معتبر باشند.");
                continue;
            }

            foreach (var prop in step.Arguments.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString() ?? string.Empty;
                    if (ForbiddenTokens.Any(token => val.Contains(token, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add($"گام {i + 1}: مقدار پارامتر '{prop.Name}' حاوی الگوی غیرمجاز یا پیمایش مسیر است.");
                    }
                }
            }

            if (allowedMap is not null &&
                allowedMap.TryGetValue(step.SkillId, out var skillSpec) &&
                !string.IsNullOrWhiteSpace(skillSpec.ArgumentsJsonSchema))
            {
                ValidateRequiredPropertiesFromSchema(i + 1, step, skillSpec.ArgumentsJsonSchema, errors);
            }
        }

        return new PlanValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateRequiredPropertiesFromSchema(
        int stepNumber,
        AiProposedStep step,
        string schemaJson,
        List<string> errors)
    {
        try
        {
            using var schemaDoc = JsonDocument.Parse(schemaJson);
            if (schemaDoc.RootElement.TryGetProperty("required", out var reqArray) &&
                reqArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var reqItem in reqArray.EnumerateArray())
                {
                    var propName = reqItem.GetString();
                    if (!string.IsNullOrWhiteSpace(propName) &&
                        !step.Arguments.TryGetProperty(propName, out _))
                    {
                        errors.Add($"گام {stepNumber}: ویژگی اجباری '{propName}' در آرگومان‌های '{step.SkillId}' وجود ندارد.");
                    }
                }
            }
        }
        catch (JsonException)
        {
            errors.Add($"گام {stepNumber}: اسکیما JSON برای '{step.SkillId}' نامعتبر است.");
        }
    }
}
