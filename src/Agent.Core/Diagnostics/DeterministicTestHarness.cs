using Agent.Core.Abstractions;

namespace Agent.Core.Diagnostics;

/// <summary>
/// Deterministic Virtual Clock (#93) for testing time-sensitive leases, caches,
/// and schedulers without wall-clock sleeps.
/// </summary>
public sealed class VirtualClock : IClock
{
    public VirtualClock(DateTimeOffset? initialUtc = null)
    {
        UtcNow = initialUtc ?? new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);

    public void SetUtcNow(DateTimeOffset value) => UtcNow = value;
}

public sealed class DeterministicSkillContext : ISkillContext
{
    public DeterministicSkillContext(
        string languageCode = "fa",
        VirtualClock? clock = null,
        CancellationToken cancellationToken = default)
    {
        LanguageCode = languageCode;
        Clock = clock ?? new VirtualClock();
        CancellationToken = cancellationToken;
    }

    public string LanguageCode { get; }
    public IClock Clock { get; }
    public CancellationToken CancellationToken { get; }
    public List<SkillProgressUpdate> RecordedProgress { get; } = [];

    public void ReportProgress(SkillProgressUpdate progress) => RecordedProgress.Add(progress);
}
