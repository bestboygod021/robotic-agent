using Agent.Core.AI;

namespace Agent.Core.Resources;

public enum ResourcePowerSource
{
    Ac,
    Battery,
    Unknown
}

public sealed record StorageVolumeInfo(
    string Name,
    string DriveType,
    string FileSystem,
    long TotalBytes,
    long AvailableBytes,
    bool IsSystemVolume);

/// <summary>
/// Hardware facts available from standard Windows interfaces. Optional values
/// remain null when Windows does not expose them reliably on this device.
/// </summary>
public sealed record SystemHardwareProfile(
    string? ProcessorName,
    int? ReportedProcessorMHz,
    int? PhysicalCoreCount,
    int LogicalProcessorCount,
    long TotalPhysicalMemoryBytes,
    long AvailablePhysicalMemoryBytes,
    double CurrentCpuPercent,
    IReadOnlyList<StorageVolumeInfo> StorageVolumes,
    bool? HasBattery,
    ResourcePowerSource PowerSource,
    int? BatteryPercent,
    string? GraphicsAdapterName,
    long? GraphicsMemoryBytes,
    long? PowerSupplyCapacityWatts,
    bool ThermalSensorsAvailable,
    DateTimeOffset DetectedAtUtc)
{
    public LocalSystemResources ToResourceSnapshot() => new(
        TotalPhysicalMemoryBytes,
        AvailablePhysicalMemoryBytes,
        LogicalProcessorCount,
        CurrentCpuPercent);

    public bool IsValid =>
        LogicalProcessorCount > 0 &&
        TotalPhysicalMemoryBytes > 0 &&
        AvailablePhysicalMemoryBytes >= 0 &&
        AvailablePhysicalMemoryBytes <= TotalPhysicalMemoryBytes &&
        double.IsFinite(CurrentCpuPercent) && CurrentCpuPercent is >= 0 and <= 100 &&
        StorageVolumes is not null &&
        StorageVolumes.All(volume => volume is not null && !string.IsNullOrWhiteSpace(volume.Name) &&
            volume.TotalBytes >= 0 && volume.AvailableBytes >= 0 && volume.AvailableBytes <= volume.TotalBytes) &&
        (PhysicalCoreCount is null or > 0) &&
        (ReportedProcessorMHz is null or > 0) &&
        (BatteryPercent is null or >= 0 and <= 100) &&
        (GraphicsMemoryBytes is null or >= 0) &&
        (PowerSupplyCapacityWatts is null or > 0);
}

public interface ISystemHardwareProbe
{
    Task<SystemHardwareProfile> DetectAsync(CancellationToken cancellationToken = default);
}
