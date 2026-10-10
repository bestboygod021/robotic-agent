namespace Agent.Core.Security;

/// <summary>
/// Guards user-selected folders before any file operation. The MVP only copies files;
/// it never deletes or overwrites existing content.
/// </summary>
public static class PathGuard
{
    private static readonly Environment.SpecialFolder[] ProtectedFolders =
    [
        Environment.SpecialFolder.Windows,
        Environment.SpecialFolder.ProgramFiles,
        Environment.SpecialFolder.ProgramFilesX86,
        Environment.SpecialFolder.CommonApplicationData
    ];

    public static string ValidateSelectedFolder(string path, string role)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"پوشهٔ {role} را انتخاب کنید.", nameof(path));
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"مسیر پوشهٔ {role} معتبر نیست.", nameof(path), exception);
        }

        // Reject UNC/network paths before even probing whether the directory exists,
        // so validation itself cannot trigger a remote file-system request.
        EnsureLocalPath(fullPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"پوشهٔ {role} پیدا نشد.");
        }

        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) && PathsEqual(fullPath, root))
        {
            throw new InvalidOperationException("انتخاب ریشهٔ درایو مجاز نیست؛ یک پوشهٔ مشخص انتخاب کنید.");
        }

        RejectReparsePointsInPath(fullPath);

        foreach (var specialFolder in ProtectedFolders)
        {
            var protectedPath = Environment.GetFolderPath(specialFolder);
            if (!string.IsNullOrWhiteSpace(protectedPath) && IsSameOrChild(fullPath, protectedPath))
            {
                throw new UnauthorizedAccessException("کار روی پوشه‌های سیستمی ویندوز و برنامه‌ها مجاز نیست.");
            }
        }

        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    public static bool PathsEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            comparison);
    }


    private static void EnsureLocalPath(string fullPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (fullPath.StartsWith("\\\\", StringComparison.Ordinal) || fullPath.StartsWith("//", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("نسخهٔ MVP فقط با دیسک محلی کار می‌کند؛ مسیر شبکه‌ای پشتیبانی نمی‌شود.");
        }

        var driveRoot = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(driveRoot) && new DriveInfo(driveRoot).DriveType == DriveType.Network)
        {
            throw new UnauthorizedAccessException("درایو شبکه‌ای مجاز نیست؛ یک پوشه روی همین رایانه انتخاب کنید.");
        }
    }

    private static void RejectReparsePointsInPath(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            throw new ArgumentException("مسیر پوشه معتبر نیست.", nameof(fullPath));
        }

        var current = root;
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var segments = fullPath[root.Length..].Split(separators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("برای ایمنی، مسیرهای دارای symlink یا junction پشتیبانی نمی‌شوند.");
            }
        }
    }

    private static bool IsSameOrChild(string candidate, string parent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));

        if (string.Equals(normalizedCandidate, normalizedParent, comparison))
        {
            return true;
        }

        var parentWithSeparator = normalizedParent + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(parentWithSeparator, comparison);
    }
}
