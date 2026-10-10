using Agent.Core.AI;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Agent.Desktop;

/// <summary>
/// Captures physical-memory availability and a short system-wide CPU sample.
/// This is a preflight signal, not a runtime enforcement mechanism.
/// </summary>
internal sealed class WindowsSystemResourceMonitor : IAiSystemResourceMonitor
{
    private readonly TimeSpan _cpuSamplingWindow;

    public WindowsSystemResourceMonitor(TimeSpan? cpuSamplingWindow = null)
    {
        _cpuSamplingWindow = cpuSamplingWindow ?? TimeSpan.FromMilliseconds(250);
        if (_cpuSamplingWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cpuSamplingWindow));
        }
    }

    public async Task<LocalSystemResources> CaptureAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("پایش منابع این نسخه فقط در ویندوز پشتیبانی می‌شود.");
        }

        var cpuBefore = ReadCpuTimes();
        await Task.Delay(_cpuSamplingWindow, cancellationToken).ConfigureAwait(false);
        var cpuAfter = ReadCpuTimes();
        var cpuPercent = CalculateCpuPercent(cpuBefore, cpuAfter);
        var memory = ReadMemoryStatus();

        return new LocalSystemResources(
            checked((long)memory.TotalPhysicalBytes),
            checked((long)memory.AvailablePhysicalBytes),
            Environment.ProcessorCount,
            cpuPercent);
    }

    private static MemoryStatusEx ReadMemoryStatus()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "خواندن وضعیت حافظهٔ ویندوز ناموفق بود.");
        }

        return status;
    }

    private static CpuTimes ReadCpuTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "خواندن زمان CPU ویندوز ناموفق بود.");
        }

        return new CpuTimes(ToUInt64(idle), ToUInt64(kernel), ToUInt64(user));
    }

    private static double CalculateCpuPercent(CpuTimes before, CpuTimes after)
    {
        if (after.Kernel < before.Kernel || after.User < before.User || after.Idle < before.Idle)
        {
            throw new InvalidOperationException("شمارندهٔ CPU ویندوز در بازهٔ نمونه‌گیری تغییر نامعتبر داشت.");
        }

        var kernelDelta = after.Kernel - before.Kernel;
        var userDelta = after.User - before.User;
        var idleDelta = after.Idle - before.Idle;
        var totalDelta = kernelDelta + userDelta;
        if (totalDelta == 0)
        {
            throw new InvalidOperationException("نمونهٔ CPU ویندوز برای برآورد مصرف کافی نیست.");
        }

        // The kernel counter includes idle time, so subtract idle from total.
        var busyDelta = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
        return Math.Clamp(100d * busyDelta / totalDelta, 0d, 100d);
    }

    private static ulong ToUInt64(FileTime value) =>
        ((ulong)value.HighDateTime << 32) | value.LowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
}
