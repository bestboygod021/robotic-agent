namespace Agent.Core.Automation;

public enum UiActionKind
{
    FocusWindow,
    HighlightElement,
    ClickElement,
    TypeText,
    ReadElementText
}

public sealed record BoundingBox(int Left, int Top, int Width, int Height);

public sealed record UiTargetElementSelector(
    string WindowClassName,
    string AutomationId,
    string ControlName,
    bool IsPasswordControl = false,
    BoundingBox? HighlightBounds = null);

public sealed record UiAutomationStep(
    int StepNumber,
    UiActionKind ActionKind,
    UiTargetElementSelector Target,
    string? TextPayload = null);

public sealed record UiAutomationPlanPreview(
    string PlanTitle,
    IReadOnlyList<UiAutomationStep> Steps,
    bool IsSafeToRun);

public sealed record UiAutomationExecutionResult(
    int ExecutedStepsCount,
    IReadOnlyList<string> ExecutionLog);

/// <summary>
/// Windows UI Automation Engine (#31): previews and executes step-by-step UI actions
/// with target element bounding-box highlighting, allow-listed window classes,
/// password-field blocking, InputInterferenceGuard (#39), and SafeDialogGuard (#40).
/// </summary>
public sealed class WindowsUiAutomationEngine
{
    private static readonly HashSet<string> AllowedWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Notepad",
        "CalcFrame",
        "ApplicationFrameWindow",
        "MSPaintApp",
        "SnippingTool"
    };

    private readonly InputInterferenceGuard _interferenceGuard;
    private readonly SafeDialogGuard _dialogGuard;

    public WindowsUiAutomationEngine(
        InputInterferenceGuard? interferenceGuard = null,
        SafeDialogGuard? dialogGuard = null)
    {
        _interferenceGuard = interferenceGuard ?? new InputInterferenceGuard();
        _dialogGuard = dialogGuard ?? new SafeDialogGuard();
    }

    public UiAutomationPlanPreview Preview(string planTitle, IReadOnlyList<UiAutomationStep> steps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planTitle);
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("طرح اتوماسیون رابط کاربری باید حداقل یک گام داشته باشد.");
        }

        foreach (var step in steps)
        {
            ValidateTargetSelector(step.Target);
        }

        return new UiAutomationPlanPreview(planTitle, steps, IsSafeToRun: true);
    }

    public Task<UiAutomationExecutionResult> ExecuteAsync(
        UiAutomationPlanPreview approvedPreview,
        Func<int, ActiveWindowSnapshot>? windowStateProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        if (!approvedPreview.IsSafeToRun)
        {
            throw new InvalidOperationException("طرح اتوماسیون رابط کاربری تأیید ایمنی نشده است.");
        }

        var log = new List<string>(approvedPreview.Steps.Count);
        foreach (var step in approvedPreview.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _interferenceGuard.EnsureNoInterference();
            ValidateTargetSelector(step.Target);

            var currentWindow = windowStateProvider?.Invoke(step.StepNumber)
                ?? new ActiveWindowSnapshot(step.Target.WindowClassName, step.Target.ControlName, IsModalDialog: false);
            _dialogGuard.EnsureWindowStateIsSafe(currentWindow, step.Target.WindowClassName);

            var boundsText = step.Target.HighlightBounds is { } b
                ? $"[Highlight {b.Left},{b.Top} {b.Width}x{b.Height}]"
                : "[Highlight Auto]";
            log.Add($"Step {step.StepNumber}: {step.ActionKind} on {step.Target.WindowClassName}#{step.Target.AutomationId} {boundsText}");
        }

        return Task.FromResult(new UiAutomationExecutionResult(log.Count, log));
    }

    public static void ValidateTargetSelector(UiTargetElementSelector target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.IsPasswordControl ||
            target.AutomationId.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            target.ControlName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            target.ControlName.Contains("رمز", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("تعامل اتوماسیون رابط کاربری با فیلدهای رمز عبور یا امنیتی اکیداً ممنوع است.");
        }

        if (!AllowedWindowClasses.Contains(target.WindowClassName))
        {
            throw new UnauthorizedAccessException($"کلاس پنجرهٔ '{target.WindowClassName}' در فهرست پنجره‌های مجاز اتوماسیون نیست.");
        }
    }
}
