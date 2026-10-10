using Agent.Core.AI;
using System.Text.Json.Serialization;

namespace Agent.Core.Resources;

public enum AdjustableResource
{
    Cpu,
    Memory,
    DiskIo
}

/// <summary>
/// Shares describe where the user's resource priority goes. They must total
/// 100; changing one share automatically rebalances the other two.
/// </summary>
public sealed record ResourceMixShares(int Cpu, int Memory, int DiskIo)
{
    public static ResourceMixShares Balanced { get; } = new(40, 40, 20);

    [JsonIgnore]
    public bool IsValid =>
        Cpu is >= 0 and <= 100 &&
        Memory is >= 0 and <= 100 &&
        DiskIo is >= 0 and <= 100 &&
        Cpu + Memory + DiskIo == 100;
}

public sealed record ResourcePreferences(
    int RequestedIntensityPercent,
    ResourceMixShares Mix)
{
    public static ResourcePreferences Balanced { get; } = new(50, ResourceMixShares.Balanced);

    [JsonIgnore]
    public bool IsValid =>
        RequestedIntensityPercent is >= 0 and <= 100 &&
        Mix is not null &&
        Mix.IsValid;
}

public sealed record ResourceHardLimits(
    int MaxCpuPercent,
    long MaxMemoryBytes,
    long MaxDiskIoBytesPerSecond,
    int MaxCpuThreads,
    int MaxParallelTasks,
    int MaxIntensityPercent,
    bool LocalWorkloadPaused,
    string Reason);

public sealed record EffectiveResourceBudget(
    int IntensityPercent,
    ResourceMixShares Mix,
    int CpuPercent,
    long MemoryBytes,
    long DiskIoBytesPerSecond,
    int CpuThreads,
    int MaxParallelTasks,
    int MaxGpuPercent,
    bool WasClamped,
    bool IsPaused,
    string Reason);

/// <summary>
/// Applies user preferences below non-overridable, hardware-aware safety caps.
/// These limits protect responsiveness and preserve system headroom; they are
/// not a guarantee against failures caused by cooling, power-supply, or hardware defects.
/// </summary>
public sealed class ResourceGovernor
{
    private const int AcCpuCeilingPercent = 35;
    private const int BatteryCpuCeilingPercent = 20;
    private const int UnknownPowerCpuCeilingPercent = 15;
    private const int AcIntensityCeilingPercent = 100;
    private const int BatteryIntensityCeilingPercent = 60;
    private const int UnknownPowerIntensityCeilingPercent = 40;
    private const int SystemCpuPauseThresholdPercent = 80;
    private const int CpuHeadroomReservePercent = 15;
    private const int MaximumLocalCpuThreads = 2;
    private const int MaximumParallelTasks = 2;
    private const long MaximumLocalModelMemoryBytes = 1024L * 1024 * 1024;
    private const long MinimumFreeMemoryReserveBytes = 1024L * 1024 * 1024;
    private const long AcDiskIoCeilingBytesPerSecond = 10L * 1024 * 1024;
    private const long BatteryDiskIoCeilingBytesPerSecond = 3L * 1024 * 1024;
    private const long UnknownPowerDiskIoCeilingBytesPerSecond = 2L * 1024 * 1024;
    public const long MinimumFreeDiskReserveBytes = 1024L * 1024 * 1024;

    public ResourceMixShares Rebalance(
        ResourceMixShares current,
        AdjustableResource changedResource,
        int requestedShare)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!current.IsValid)
        {
            current = ResourceMixShares.Balanced;
        }

        requestedShare = Math.Clamp(requestedShare, 0, 100);
        var otherShares = changedResource switch
        {
            AdjustableResource.Cpu => new[] { current.Memory, current.DiskIo },
            AdjustableResource.Memory => new[] { current.Cpu, current.DiskIo },
            AdjustableResource.DiskIo => new[] { current.Cpu, current.Memory },
            _ => throw new ArgumentOutOfRangeException(nameof(changedResource))
        };

        var remainder = 100 - requestedShare;
        var otherTotal = otherShares.Sum();
        int first;
        int second;
        if (otherTotal == 0)
        {
            first = remainder / 2;
            second = remainder - first;
        }
        else
        {
            var firstExact = (double)remainder * otherShares[0] / otherTotal;
            var secondExact = (double)remainder * otherShares[1] / otherTotal;
            first = (int)Math.Floor(firstExact);
            second = (int)Math.Floor(secondExact);
            var unassigned = remainder - first - second;
            if (unassigned > 0)
            {
                if (firstExact - first >= secondExact - second)
                {
                    first += unassigned;
                }
                else
                {
                    second += unassigned;
                }
            }
        }

        return changedResource switch
        {
            AdjustableResource.Cpu => new ResourceMixShares(requestedShare, first, second),
            AdjustableResource.Memory => new ResourceMixShares(first, requestedShare, second),
            AdjustableResource.DiskIo => new ResourceMixShares(first, second, requestedShare),
            _ => throw new ArgumentOutOfRangeException(nameof(changedResource))
        };
    }

    public ResourceHardLimits CalculateHardLimits(SystemHardwareProfile hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        return CalculateHardLimits(hardware, hardware.ToResourceSnapshot());
    }

    public ResourceHardLimits CalculateHardLimits(
        SystemHardwareProfile hardware,
        LocalSystemResources currentResources)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(currentResources);

        if (!hardware.IsValid || !currentResources.IsValid)
        {
            return Paused("مشخصات سخت‌افزار یا وضعیت لحظه‌ای معتبر نیست؛ اجرای محلی متوقف شد.");
        }

        var powerSource = hardware.PowerSource;
        var powerCpuCeiling = powerSource switch
        {
            ResourcePowerSource.Ac => AcCpuCeilingPercent,
            ResourcePowerSource.Battery => BatteryCpuCeilingPercent,
            _ => UnknownPowerCpuCeilingPercent
        };
        var powerIntensityCeiling = powerSource switch
        {
            ResourcePowerSource.Ac => AcIntensityCeilingPercent,
            ResourcePowerSource.Battery => BatteryIntensityCeilingPercent,
            _ => UnknownPowerIntensityCeilingPercent
        };
        var configuredDiskIoCeiling = powerSource switch
        {
            ResourcePowerSource.Ac => AcDiskIoCeilingBytesPerSecond,
            ResourcePowerSource.Battery => BatteryDiskIoCeilingBytesPerSecond,
            _ => UnknownPowerDiskIoCeilingBytesPerSecond
        };
        var hasVolumeHeadroom = hardware.StorageVolumes.Any(volume =>
            volume.TotalBytes > 0 && volume.AvailableBytes > MinimumFreeDiskReserveBytes);
        var diskIoCeiling = hasVolumeHeadroom ? configuredDiskIoCeiling : 0;

        var memoryReserve = Math.Max(
            MinimumFreeMemoryReserveBytes,
            (long)(hardware.TotalPhysicalMemoryBytes * 0.30));
        var memoryBasedCap = Math.Min(
            MaximumLocalModelMemoryBytes,
            (long)(hardware.TotalPhysicalMemoryBytes * 0.20));
        var availableForAgent = Math.Max(0, currentResources.AvailablePhysicalMemoryBytes - memoryReserve);
        var memoryCap = Math.Min(memoryBasedCap, availableForAgent);

        var cpuHeadroom = Math.Max(0, 100 - (int)Math.Ceiling(currentResources.CurrentCpuPercent) - CpuHeadroomReservePercent);
        var cpuCap = Math.Min(powerCpuCeiling, cpuHeadroom);
        var cpuThreads = hardware.LogicalProcessorCount > 1
            ? Math.Min(MaximumLocalCpuThreads, hardware.LogicalProcessorCount - 1)
            : 1;

        var intensityCap = powerIntensityCeiling;
        var reason = powerSource switch
        {
            ResourcePowerSource.Battery => "باتری فعال است؛ سقف مصرف برای کاهش فشار و مصرف انرژی محدود شد.",
            ResourcePowerSource.Unknown => "وضعیت برق نامشخص است؛ سقف محافظه‌کارانه اعمال شد.",
            _ => "سقف سخت‌افزاری محافظه‌کارانه برای حفظ منابع سیستم اعمال شد."
        };

        if (currentResources.CurrentCpuPercent >= SystemCpuPauseThresholdPercent || memoryCap <= 0 || cpuCap <= 0)
        {
            return new ResourceHardLimits(
                0,
                memoryCap,
                0,
                0,
                0,
                0,
                LocalWorkloadPaused: true,
                Reason: currentResources.CurrentCpuPercent >= SystemCpuPauseThresholdPercent
                    ? "مصرف فعلی CPU یا کمبود حافظه بالاست؛ پردازش محلی تا کاهش بار متوقف می‌ماند."
                    : "حافظهٔ آزاد یا headroom پردازنده برای اجرای محلی کافی نیست.");
        }

        return new ResourceHardLimits(
            cpuCap,
            memoryCap,
            diskIoCeiling,
            cpuThreads,
            Math.Min(MaximumParallelTasks, cpuThreads),
            intensityCap,
            LocalWorkloadPaused: false,
            Reason: reason);
    }

    public EffectiveResourceBudget Apply(
        ResourcePreferences preferences,
        ResourceHardLimits hardLimits)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(hardLimits);

        if (!preferences.Mix.IsValid)
        {
            throw new ArgumentException("Resource mix must be valid and total exactly 100.", nameof(preferences));
        }

        if (hardLimits.LocalWorkloadPaused || hardLimits.MaxIntensityPercent <= 0)
        {
            return new EffectiveResourceBudget(
                0,
                preferences.Mix,
                0,
                0,
                0,
                0,
                0,
                0,
                WasClamped: preferences.RequestedIntensityPercent != 0,
                IsPaused: true,
                Reason: hardLimits.Reason);
        }

        var intensity = Math.Clamp(preferences.RequestedIntensityPercent, 0, hardLimits.MaxIntensityPercent);
        var wasClamped = intensity != preferences.RequestedIntensityPercent;
        var cpuShareFactor = preferences.Mix.Cpu / 40d;
        var memoryShareFactor = preferences.Mix.Memory / 40d;
        var diskShareFactor = preferences.Mix.DiskIo / 20d;

        var requestedCpuPercent = (int)Math.Floor(hardLimits.MaxCpuPercent * intensity / 100d * cpuShareFactor);
        var requestedMemoryBytes = (long)Math.Floor(hardLimits.MaxMemoryBytes * intensity / 100d * memoryShareFactor);
        var requestedDiskIo = (long)Math.Floor(hardLimits.MaxDiskIoBytesPerSecond * intensity / 100d * diskShareFactor);
        var cpuPercent = Math.Min(hardLimits.MaxCpuPercent, requestedCpuPercent);
        var memoryBytes = Math.Min(hardLimits.MaxMemoryBytes, requestedMemoryBytes);
        var diskIo = Math.Min(hardLimits.MaxDiskIoBytesPerSecond, requestedDiskIo);
        wasClamped |= cpuPercent != requestedCpuPercent || memoryBytes != requestedMemoryBytes || diskIo != requestedDiskIo;
        var cpuThreads = cpuPercent <= 0 || hardLimits.MaxCpuThreads <= 0
            ? 0
            : Math.Min(hardLimits.MaxCpuThreads, Math.Max(1, (int)Math.Ceiling(hardLimits.MaxCpuThreads * cpuShareFactor * intensity / 100d)));
        var parallelTasks = cpuThreads <= 0
            ? 0
            : Math.Min(hardLimits.MaxParallelTasks, Math.Max(1, cpuThreads));

        return new EffectiveResourceBudget(
            intensity,
            preferences.Mix,
            cpuPercent,
            memoryBytes,
            diskIo,
            cpuThreads,
            parallelTasks,
            MaxGpuPercent: 0,
            WasClamped: wasClamped,
            IsPaused: cpuPercent <= 0 || memoryBytes <= 0,
            Reason: hardLimits.Reason);
    }

    public bool HasWriteHeadroom(StorageVolumeInfo volume, long requiredBytes)
    {
        ArgumentNullException.ThrowIfNull(volume);
        if (requiredBytes < 0 || volume.TotalBytes <= 0 || volume.AvailableBytes < 0 || volume.AvailableBytes > volume.TotalBytes)
        {
            return false;
        }

        var freeAfterReserve = Math.Max(0, volume.AvailableBytes - MinimumFreeDiskReserveBytes);
        return requiredBytes <= freeAfterReserve;
    }

    private static ResourceHardLimits Paused(string reason) => new(
        MaxCpuPercent: 0,
        MaxMemoryBytes: 0,
        MaxDiskIoBytesPerSecond: 0,
        MaxCpuThreads: 0,
        MaxParallelTasks: 0,
        MaxIntensityPercent: 0,
        LocalWorkloadPaused: true,
        Reason: reason);
}
