using Agent.Core.AI;
using Agent.Core.Resources;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Agent.Desktop;

/// <summary>
/// Reads hardware facts that Windows exposes through standard local APIs.
/// Unsupported measurements are reported as unknown rather than guessed.
/// </summary>
internal sealed class WindowsHardwareProbe : ISystemHardwareProbe
{
    private readonly IAiSystemResourceMonitor _resourceMonitor;

    public WindowsHardwareProbe(IAiSystemResourceMonitor resourceMonitor)
    {
        _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
    }

    public async Task<SystemHardwareProfile> DetectAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("شناسایی سخت‌افزار این نسخه فقط در ویندوز پشتیبانی می‌شود.");
        }

        var resources = await _resourceMonitor.CaptureAsync(cancellationToken).ConfigureAwait(false);
        var (powerSource, hasBattery, batteryPercent) = ReadPowerStatus();

        return new SystemHardwareProfile(
            ProcessorName: ReadProcessorName(),
            ReportedProcessorMHz: ReadReportedProcessorMHz(),
            PhysicalCoreCount: ReadPhysicalCoreCount(),
            LogicalProcessorCount: resources.LogicalProcessorCount,
            TotalPhysicalMemoryBytes: resources.TotalPhysicalMemoryBytes,
            AvailablePhysicalMemoryBytes: resources.AvailablePhysicalMemoryBytes,
            CurrentCpuPercent: resources.CurrentCpuPercent,
            StorageVolumes: ReadLocalVolumes(),
            HasBattery: hasBattery,
            PowerSource: powerSource,
            BatteryPercent: batteryPercent,
            GraphicsAdapterName: null,
            GraphicsMemoryBytes: null,
            PowerSupplyCapacityWatts: null,
            ThermalSensorsAvailable: false,
            DetectedAtUtc: DateTimeOffset.UtcNow);
    }

    private static string? ReadProcessorName()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString",
                defaultValue: null) as string;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int? ReadReportedProcessorMHz()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "~MHz",
                defaultValue: null);
            return value switch
            {
                int frequency when frequency > 0 => frequency,
                long frequency when frequency is > 0 and <= int.MaxValue => (int)frequency,
                _ => null
            };
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int? ReadPhysicalCoreCount()
    {
        uint requiredBytes = 0;
        var firstCallSucceeded = GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref requiredBytes);
        if (firstCallSucceeded || requiredBytes == 0 || Marshal.GetLastWin32Error() != 122)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(checked((int)requiredBytes));
        try
        {
            var actualBytes = requiredBytes;
            if (!GetLogicalProcessorInformationEx(0, buffer, ref actualBytes))
            {
                return null;
            }

            var offset = 0;
            var coreCount = 0;
            while ((long)offset + 8 <= actualBytes)
            {
                var relationship = Marshal.ReadInt32(buffer, offset);
                var entrySize = Marshal.ReadInt32(buffer, offset + 4);
                if (entrySize < 8 || (long)offset + entrySize > actualBytes)
                {
                    return null;
                }

                if (relationship == 0)
                {
                    coreCount++;
                }

                offset += entrySize;
            }

            return coreCount > 0 ? coreCount : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IReadOnlyList<StorageVolumeInfo> ReadLocalVolumes()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
        var volumes = new List<StorageVolumeInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if ((drive.DriveType is not DriveType.Fixed and not DriveType.Removable) || !drive.IsReady)
                {
                    continue;
                }

                volumes.Add(new StorageVolumeInfo(
                    drive.Name,
                    drive.DriveType.ToString(),
                    drive.DriveFormat,
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase)));
            }
            catch (IOException)
            {
                // A volume can disappear or become unavailable during discovery.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep other volume metadata if this one cannot be queried.
            }
        }

        return volumes;
    }

    private static (ResourcePowerSource Source, bool? HasBattery, int? BatteryPercent) ReadPowerStatus()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return (ResourcePowerSource.Unknown, null, null);
        }

        bool? hasBattery = status.BatteryFlag switch
        {
            128 => false,
            255 => null,
            _ => true
        };
        int? batteryPercent = status.BatteryLifePercent <= 100 && hasBattery != false
            ? status.BatteryLifePercent
            : null;

        var source = status.AcLineStatus switch
        {
            1 => ResourcePowerSource.Ac,
            0 when hasBattery == true => ResourcePowerSource.Battery,
            _ => ResourcePowerSource.Unknown
        };

        return (source, hasBattery, batteryPercent);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
