namespace Agent.Core.Abstractions;

/// <summary>
/// Execution context passed to skills so they honor user cancellation, disk I/O
/// ceilings from ResourceGovernor, and current language preferences.
/// </summary>
public interface ISkillContext
{
    string LanguageCode { get; }

    long MaxDiskIoBytesPerSecond { get; }

    CancellationToken CancellationToken { get; }
}

public sealed record SkillExecutionContext(
    string LanguageCode,
    long MaxDiskIoBytesPerSecond,
    CancellationToken CancellationToken) : ISkillContext;
