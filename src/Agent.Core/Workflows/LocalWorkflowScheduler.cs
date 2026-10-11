namespace Agent.Core.Workflows;

public enum ScheduleCadence
{
    IntervalMinutes,
    Daily,
    Weekly,
    Monthly
}

public sealed record ScheduledWorkflowEntry(
    string ScheduleId,
    string WorkflowId,
    WorkflowRuntimeInputs RuntimeInputs,
    ScheduleCadence Cadence,
    int IntervalMinutes,
    TimeOnly TimeOfDay,
    DayOfWeek? DayOfWeek,
    int? DayOfMonth,
    DateTimeOffset NextRunUtc,
    bool IsEnabled = true);

/// <summary>
/// Local Cron / Scheduler (#49) for recurring workflows (Daily, Weekly, Monthly,
/// or fixed minute intervals) with deterministic next-run calculation.
/// </summary>
public sealed class LocalWorkflowScheduler
{
    private readonly Dictionary<string, ScheduledWorkflowEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ScheduledWorkflowEntry> Entries => _entries.Values;

    public ScheduledWorkflowEntry RegisterSchedule(
        string scheduleId,
        string workflowId,
        WorkflowRuntimeInputs runtimeInputs,
        ScheduleCadence cadence,
        DateTimeOffset nowUtc,
        int intervalMinutes = 60,
        TimeOnly? timeOfDay = null,
        DayOfWeek? dayOfWeek = null,
        int? dayOfMonth = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(runtimeInputs);

        var tod = timeOfDay ?? new TimeOnly(9, 0);
        var nextRun = ComputeNextRunUtc(cadence, nowUtc, intervalMinutes, tod, dayOfWeek, dayOfMonth);
        var entry = new ScheduledWorkflowEntry(
            ScheduleId: scheduleId,
            WorkflowId: workflowId,
            RuntimeInputs: runtimeInputs,
            Cadence: cadence,
            IntervalMinutes: Math.Max(5, intervalMinutes),
            TimeOfDay: tod,
            DayOfWeek: dayOfWeek,
            DayOfMonth: dayOfMonth,
            NextRunUtc: nextRun,
            IsEnabled: true);

        _entries[scheduleId] = entry;
        return entry;
    }

    public IReadOnlyList<ScheduledWorkflowEntry> PopDueSchedules(DateTimeOffset nowUtc)
    {
        var due = new List<ScheduledWorkflowEntry>();
        foreach (var (id, entry) in _entries.ToArray())
        {
            if (!entry.IsEnabled || entry.NextRunUtc > nowUtc)
            {
                continue;
            }

            due.Add(entry);
            var next = ComputeNextRunUtc(
                entry.Cadence,
                nowUtc,
                entry.IntervalMinutes,
                entry.TimeOfDay,
                entry.DayOfWeek,
                entry.DayOfMonth);
            _entries[id] = entry with { NextRunUtc = next };
        }

        return due;
    }

    public static DateTimeOffset ComputeNextRunUtc(
        ScheduleCadence cadence,
        DateTimeOffset fromUtc,
        int intervalMinutes,
        TimeOnly timeOfDay,
        DayOfWeek? targetDayOfWeek = null,
        int? targetDayOfMonth = null)
    {
        switch (cadence)
        {
            case ScheduleCadence.IntervalMinutes:
                return fromUtc.AddMinutes(Math.Max(5, intervalMinutes));

            case ScheduleCadence.Daily:
            {
                var candidate = new DateTimeOffset(
                    fromUtc.Year, fromUtc.Month, fromUtc.Day,
                    timeOfDay.Hour, timeOfDay.Minute, 0, TimeSpan.Zero);
                return candidate <= fromUtc ? candidate.AddDays(1) : candidate;
            }

            case ScheduleCadence.Weekly:
            {
                var dow = targetDayOfWeek ?? DayOfWeek.Monday;
                var candidate = new DateTimeOffset(
                    fromUtc.Year, fromUtc.Month, fromUtc.Day,
                    timeOfDay.Hour, timeOfDay.Minute, 0, TimeSpan.Zero);
                var daysUntil = ((int)dow - (int)candidate.DayOfWeek + 7) % 7;
                candidate = candidate.AddDays(daysUntil);
                return candidate <= fromUtc ? candidate.AddDays(7) : candidate;
            }

            case ScheduleCadence.Monthly:
            {
                var day = Math.Clamp(targetDayOfMonth ?? 1, 1, 28);
                var candidate = new DateTimeOffset(
                    fromUtc.Year, fromUtc.Month, day,
                    timeOfDay.Hour, timeOfDay.Minute, 0, TimeSpan.Zero);
                if (candidate <= fromUtc)
                {
                    var nextMonth = fromUtc.AddMonths(1);
                    candidate = new DateTimeOffset(
                        nextMonth.Year, nextMonth.Month, day,
                        timeOfDay.Hour, timeOfDay.Minute, 0, TimeSpan.Zero);
                }

                return candidate;
            }

            default:
                return fromUtc.AddHours(24);
        }
    }
}
