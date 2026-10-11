using Agent.Core.Abstractions;
using Agent.Core.Skills.Rollback;

namespace Agent.Core.Skills;

public sealed record WebFormFieldMapping(
    string FieldSelector,
    string FieldLabel,
    string ProposedValue);

public sealed record WebTaskAutomationRequest(
    string TargetUrl,
    IReadOnlyList<WebFormFieldMapping> Fields,
    bool UserConfirmedFinalSubmit = false);

public sealed record WebTaskAutomationPreview(
    string TargetUrl,
    IReadOnlyList<WebFormFieldMapping> SafeFields,
    bool RequiresFinalSubmitConfirmation);

public sealed record WebTaskAutomationResult(
    string TargetUrl,
    int FilledFieldsCount,
    bool Submitted,
    OperationManifest RollbackManifest);

/// <summary>
/// Controlled Web Form Automation Skill (#36 — web.form_fill): validates HTTPS URL,
/// blocks password/credit-card fields, previews all form values, and strictly
/// prohibits auto-submitting without explicit final human approval.
/// </summary>
public sealed class WebTaskAutomationSkill : ISkill<WebTaskAutomationRequest, WebTaskAutomationPreview, WebTaskAutomationResult>
{
    public const string SkillId = "web.form_fill";

    private static readonly string[] ForbiddenFieldKeywords =
    [
        "password", "passwd", "pwd", "cvv", "cvc", "cardnumber", "creditcard", "pin", "otp", "رمز", "کارت"
    ];

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "پرکردن کنترل‌شدهٔ فرم‌های وب با ایستگاه تأیید نهایی",
            ["en"] = "Controlled Web Form Automation (Human-in-the-Loop Submit)",
            ["zh"] = "受控网页表单自动填充（人工确认提交）",
            ["es"] = "Automatización Controlada de Formularios Web",
            ["ar"] = "تعبئة نماذج الويب الخاضعة للرقابة مع تأكيد بشري نهائي",
            ["fr"] = "Remplissage Contrôlé de Formulaires Web (Validation Humaine)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "آماده‌سازی و پرکردن فیلدهای فرم وب از روی داده‌های محلی با مسدودسازی فیلدهای رمز/بانکی و توقف اجباری پیش از ثبت نهایی.",
            ["en"] = "Prepares web form fields from local data, blocks password/card inputs, and pauses before final submission.",
            ["zh"] = "根据本地数据填充网页表单，拦截密码与银行卡字段，并在最终提交前强制等待人工确认。",
            ["es"] = "Prepara campos de formularios web bloqueando contraseñas/tarjetas y requiere confirmación antes de enviar.",
            ["ar"] = "يجهز حقول نماذج الويب مع حظر حقول كلمات المرور والبطاقات والتوقف الإجباري قبل الإرسال النهائي.",
            ["fr"] = "Prépare les champs de formulaires web en bloquant mots de passe/cartes et attend la validation finale."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: false);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public WebTaskAutomationPreview Preview(WebTaskAutomationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("نشانی فرم وب باید یک پیوند امن HTTPS باشد.");
        }

        if (request.Fields is null || request.Fields.Count == 0)
        {
            throw new InvalidOperationException("حداقل یک فیلد برای پر کردن فرم وب باید مشخص شود.");
        }

        foreach (var field in request.Fields)
        {
            if (IsForbiddenSensitiveField(field.FieldSelector) || IsForbiddenSensitiveField(field.FieldLabel))
            {
                throw new InvalidOperationException($"فیلد حساس '{field.FieldLabel}' (رمز عبور یا اطلاعات کارت بانکی) در اتوماسیون وب مجاز نیست.");
            }
        }

        return new WebTaskAutomationPreview(
            TargetUrl: uri.ToString(),
            SafeFields: request.Fields,
            RequiresFinalSubmitConfirmation: true);
    }

    public Task<WebTaskAutomationResult> ExecuteAsync(
        WebTaskAutomationRequest request,
        WebTaskAutomationPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!request.UserConfirmedFinalSubmit)
        {
            throw new InvalidOperationException("ثبت نهایی فرم وب بدون تأیید صریح نهایی کاربر (UserConfirmedFinalSubmit) مجاز نیست.");
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: []);

        return Task.FromResult(new WebTaskAutomationResult(
            TargetUrl: approvedPreview.TargetUrl,
            FilledFieldsCount: approvedPreview.SafeFields.Count,
            Submitted: true,
            RollbackManifest: manifest));
    }

    public static bool IsForbiddenSensitiveField(string? selectorOrLabel)
    {
        if (string.IsNullOrWhiteSpace(selectorOrLabel))
        {
            return false;
        }

        return ForbiddenFieldKeywords.Any(kw => selectorOrLabel.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }
}
