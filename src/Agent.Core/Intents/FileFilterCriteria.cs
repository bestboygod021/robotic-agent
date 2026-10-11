namespace Agent.Core.Intents;

/// <summary>
/// Multi-attribute file filter entities (#60) extracted from a natural-language
/// request (multiple extensions, min/max byte size, recency window, and subfolder flag).
/// </summary>
public sealed record FileFilterCriteria(
    IReadOnlyList<string> Extensions,
    long? MinSizeBytes = null,
    long? MaxSizeBytes = null,
    int? ModifiedWithinDays = null,
    bool IncludeSubdirectories = false)
{
    public static FileFilterCriteria Empty { get; } = new([]);

    public bool MatchesFile(FileInfo fileInfo, DateTimeOffset? referenceNowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);

        if (Extensions.Count > 0 && !Extensions.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            var ext = fileInfo.Extension.TrimStart('.');
            if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (MinSizeBytes.HasValue && fileInfo.Length < MinSizeBytes.Value)
        {
            return false;
        }

        if (MaxSizeBytes.HasValue && fileInfo.Length > MaxSizeBytes.Value)
        {
            return false;
        }

        if (ModifiedWithinDays.HasValue && ModifiedWithinDays.Value > 0)
        {
            var now = referenceNowUtc ?? DateTimeOffset.UtcNow;
            var cutoff = now.AddDays(-ModifiedWithinDays.Value);
            if (new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero) < cutoff)
            {
                return false;
            }
        }

        return true;
    }
}
