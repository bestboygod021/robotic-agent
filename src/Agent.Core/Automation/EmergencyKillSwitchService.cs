namespace Agent.Core.Automation;

public sealed record KillSwitchEvent(
    string Reason,
    int CancelledOperationsCount,
    DateTimeOffset TriggeredAtUtc);

/// <summary>
/// Global Kill-Switch Coordinator (#38): bound to emergency hotkeys (Ctrl+Shift+Esc / Pause).
/// Immediately cancels all active skill, workflow, and UI automation tokens when triggered.
/// </summary>
public sealed class EmergencyKillSwitchService
{
    public const int VirtualKeyPause = 0x13;
    public const string DefaultHotkeyGesture = "Ctrl+Shift+Pause";

    private readonly object _sync = new();
    private readonly List<CancellationTokenSource> _activeSources = [];
    private KillSwitchEvent? _lastTriggeredEvent;

    public KillSwitchEvent? LastTriggeredEvent => _lastTriggeredEvent;

    public event EventHandler<KillSwitchEvent>? KillSwitchTriggered;

    public CancellationTokenSource RegisterOperation(CancellationToken parentToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        lock (_sync)
        {
            _activeSources.RemoveAll(s => s.IsCancellationRequested);
            _activeSources.Add(cts);
        }

        return cts;
    }

    public KillSwitchEvent TriggerKillSwitch(string reason = "فشردن کلید توقف اضطراری توسط کاربر")
    {
        List<CancellationTokenSource> toCancel;
        lock (_sync)
        {
            toCancel = [.. _activeSources];
            _activeSources.Clear();
        }

        var cancelledCount = 0;
        foreach (var cts in toCancel)
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
                cancelledCount++;
            }
        }

        var evt = new KillSwitchEvent(reason, cancelledCount, DateTimeOffset.UtcNow);
        _lastTriggeredEvent = evt;
        KillSwitchTriggered?.Invoke(this, evt);
        return evt;
    }
}
