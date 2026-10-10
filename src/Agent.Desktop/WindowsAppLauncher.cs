using Agent.Core.Skills;
using System.Diagnostics;

namespace Agent.Desktop;

/// <summary>
/// Starts only a revalidated AppCatalog entry, directly and without a shell,
/// arguments, elevation, or a user-provided executable path.
/// </summary>
internal sealed class WindowsAppLauncher
{
    public void Launch(AppLaunchPlan approvedPlan)
    {
        ArgumentNullException.ThrowIfNull(approvedPlan);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("بازکردن برنامه فقط در ویندوز پشتیبانی می‌شود.");
        }

        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var currentPlan = AppCatalog.CreatePlan(approvedPlan.Id, systemDirectory);
        if (!string.Equals(
                currentPlan.ExecutablePath,
                approvedPlan.ExecutablePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("برنامه از زمان پیش‌نمایش تغییر کرده است؛ دوباره انتخاب کنید.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = currentPlan.ExecutablePath,
            WorkingDirectory = systemDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        // No arguments are accepted, constructed, or forwarded.
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("ویندوز نتوانست برنامهٔ انتخاب‌شده را باز کند.");
        }
    }
}
