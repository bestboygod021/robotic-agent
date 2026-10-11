namespace Agent.Core.Security;

public sealed record ResourceBudgetPolicy(
    int MaxCpuPercent = 40,
    long MaxWorkingSetBytes = 512L * 1024 * 1024,
    long MaxDiskBytesPerSecond = 50L * 1024 * 1024);

/// <summary>
/// Hardware/OS Resource Governor (#71): enforces CPU rate caps, working-set memory
/// limits, and I/O byte-rate throttling so background file/document jobs never
/// freeze the user's system.
/// </summary>
public sealed class ResourceGovernor
{
    private readonly SemaphoreSlim _concurrencyGate;

    public ResourceGovernor(ResourceBudgetPolicy? policy = null, int maxConcurrentTasks = 2)
    {
        Policy = policy ?? new ResourceBudgetPolicy();
        MaxConcurrentTasks = Math.Clamp(maxConcurrentTasks, 1, 8);
        _concurrencyGate = new SemaphoreSlim(MaxConcurrentTasks, MaxConcurrentTasks);
    }

    public ResourceBudgetPolicy Policy { get; }
    public int MaxConcurrentTasks { get; }

    public void EnsureWithinMemoryCap(long currentWorkingSetBytes)
    {
        if (currentWorkingSetBytes > Policy.MaxWorkingSetBytes)
        {
            throw new InvalidOperationException(
                $"مصرف حافظهٔ رم ({currentWorkingSetBytes / (1024 * 1024)} مگابایت) از سقف مجاز ({Policy.MaxWorkingSetBytes / (1024 * 1024)} مگابایت) فراتر رفت.");
        }
    }

    public TimeSpan CalculateIoThrottleDelay(long bytesProcessedInWindow, TimeSpan elapsedWindow)
    {
        if (bytesProcessedInWindow <= 0 || Policy.MaxDiskBytesPerSecond <= 0)
        {
            return TimeSpan.Zero;
        }

        var requiredSeconds = (double)bytesProcessedInWindow / Policy.MaxDiskBytesPerSecond;
        var requiredSpan = TimeSpan.FromSeconds(requiredSeconds);
        return requiredSpan > elapsedWindow ? requiredSpan - elapsedWindow : TimeSpan.Zero;
    }

    public async Task<T> RunGovernedAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }
}
