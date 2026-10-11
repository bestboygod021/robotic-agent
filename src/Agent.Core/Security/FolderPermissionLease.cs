namespace Agent.Core.Security;

public enum FolderAccessScope
{
    SingleExecution,
    TimeBounded
}

public sealed record FolderPermissionLease(
    string LeaseId,
    string ValidatedFolderPath,
    string Role,
    FolderAccessScope Scope,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int RemainingRuns)
{
    public bool IsExpired(DateTimeOffset nowUtc) =>
        nowUtc >= ExpiresAtUtc || RemainingRuns <= 0;
}

/// <summary>
/// Manages temporary, revocable folder access leases (#73) so skills only access
/// user-approved directories within the granted time window or run count.
/// </summary>
public sealed class FolderPermissionManager
{
    public static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaximumLeaseDuration = TimeSpan.FromHours(8);

    private readonly Dictionary<string, FolderPermissionLease> _leases = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public FolderPermissionLease GrantLease(
        string folderPath,
        string role,
        FolderAccessScope scope = FolderAccessScope.TimeBounded,
        TimeSpan? duration = null,
        DateTimeOffset? nowUtc = null)
    {
        var validatedPath = PathGuard.ValidateSelectedFolder(folderPath, role);
        var issuedAt = nowUtc ?? DateTimeOffset.UtcNow;
        var effectiveDuration = duration ?? DefaultLeaseDuration;
        if (effectiveDuration <= TimeSpan.Zero || effectiveDuration > MaximumLeaseDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "مدت مجوز پوشه باید بین ۱ ثانیه تا حداکثر ۸ ساعت باشد.");
        }

        var maxRuns = scope == FolderAccessScope.SingleExecution ? 1 : int.MaxValue;
        var lease = new FolderPermissionLease(
            LeaseId: Guid.NewGuid().ToString("N"),
            ValidatedFolderPath: validatedPath,
            Role: role,
            Scope: scope,
            GrantedAtUtc: issuedAt,
            ExpiresAtUtc: issuedAt.Add(effectiveDuration),
            RemainingRuns: maxRuns);

        lock (_sync)
        {
            _leases[lease.LeaseId] = lease;
        }

        return lease;
    }

    public FolderPermissionLease ConsumeLease(string leaseId, DateTimeOffset? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        var currentTime = nowUtc ?? DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (!_leases.TryGetValue(leaseId, out var lease))
            {
                throw new UnauthorizedAccessException("مجوز دسترسی به پوشه یافت نشد یا لغو شده است.");
            }

            if (lease.IsExpired(currentTime))
            {
                _leases.Remove(leaseId);
                throw new UnauthorizedAccessException($"مهلت مجوز دسترسی به پوشهٔ «{lease.Role}» به پایان رسیده است؛ پوشه را دوباره تأیید کنید.");
            }

            var decrementedRuns = lease.Scope == FolderAccessScope.SingleExecution
                ? lease.RemainingRuns - 1
                : lease.RemainingRuns;

            var updated = lease with { RemainingRuns = decrementedRuns };
            if (updated.RemainingRuns <= 0)
            {
                _leases.Remove(leaseId);
            }
            else
            {
                _leases[leaseId] = updated;
            }

            return updated;
        }
    }

    public bool RevokeLease(string leaseId)
    {
        if (string.IsNullOrWhiteSpace(leaseId))
        {
            return false;
        }

        lock (_sync)
        {
            return _leases.Remove(leaseId);
        }
    }

    public void RevokeAll()
    {
        lock (_sync)
        {
            _leases.Clear();
        }
    }
}
