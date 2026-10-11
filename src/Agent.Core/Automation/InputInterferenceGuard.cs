namespace Agent.Core.Automation;

/// <summary>
/// Input Interference Guard (#39): detects physical user mouse movement or keystrokes
/// while desktop automation is active and immediately pauses automation so human agency
/// is never overridden.
/// </summary>
public sealed class InputInterferenceGuard
{
    public const double DefaultMaxMouseDeltaPixels = 25.0;

    private (int X, int Y) _baselineCursorPosition;
    private bool _interferenceDetected;
    private string? _interferenceReason;

    public bool InterferenceDetected => _interferenceDetected;
    public string? InterferenceReason => _interferenceReason;

    public void BeginAutomationSession(int startCursorX = 0, int startCursorY = 0)
    {
        _baselineCursorPosition = (startCursorX, startCursorY);
        _interferenceDetected = false;
        _interferenceReason = null;
    }

    public bool ReportPhysicalMousePosition(int currentX, int currentY, double maxDeltaPixels = DefaultMaxMouseDeltaPixels)
    {
        var dx = currentX - _baselineCursorPosition.X;
        var dy = currentY - _baselineCursorPosition.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance > maxDeltaPixels)
        {
            _interferenceDetected = true;
            _interferenceReason = $"حرکت فیزیکی ماوس توسط کاربر ({distance:F0} پیکسل) تشخیص داده شد؛ اتوماسیون متوقف شد.";
            return true;
        }

        return false;
    }

    public void ReportPhysicalKeystroke(string keyName)
    {
        _interferenceDetected = true;
        _interferenceReason = $"فشرده شدن کلید «{keyName}» توسط کاربر تشخیص داده شد؛ اتوماسیون متوقف شد.";
    }

    public void EnsureNoInterference()
    {
        if (_interferenceDetected)
        {
            throw new InvalidOperationException(_interferenceReason ?? "تداخل ورودی کاربر تشخیص داده شد.");
        }
    }
}
