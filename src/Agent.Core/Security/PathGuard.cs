namespace Agent.Core.Security;

/// <summary>
/// Guards user-selected folders before any file operation. The MVP only copies files;
/// it never deletes or overwrites existing content. Supports international Unicode
/// directory names and local Windows extended-length paths while strictly rejecting
/// UNC/network shares, drive roots, system folders, and reparse points.
/// </summary>
public static class PathGuard
{
    private const string ExtendedPathPrefix = @"\\?\";
    private const string ExtendedUncPrefix = @"\\?\UNC\";

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

        var candidate = StripLocalExtendedPrefix(path.Trim());

        // Reject UNC/network paths before calling Path.GetFullPath or Directory.Exists
        // so validation itself never triggers a remote SMB request.
        EnsureLocalPathSyntax(candidate);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"مسیر پوشهٔ {role} معتبر نیست.", nameof(path), exception);
        }

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

    private static readonly HashSet<string> DangerousExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".scr", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".js", ".jse",
        ".wsf", ".wsh", ".com", ".msi", ".msp", ".hta", ".pif", ".reg", ".jar"
    };

    private static readonly HashSet<string> DecoyDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf",
        ".csv", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".mp3", ".mp4", ".zip"
    };

    public static bool IsSuspiciousDoubleExtension(string? fileNameOrPath, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return false;
        }

        var fileName = Path.GetFileName(fileNameOrPath.Trim());
        var outerExtension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(outerExtension) || !DangerousExecutableExtensions.Contains(outerExtension))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var innerExtension = Path.GetExtension(stem);
        if (!string.IsNullOrWhiteSpace(innerExtension) && DecoyDocumentExtensions.Contains(innerExtension))
        {
            reason = $"فایل «{fileName}» دارای پسوند دوگانهٔ فریبنده ({innerExtension}{outerExtension}) است و به‌دلیل ریسک امنیتی مسدود می‌شود.";
            return true;
        }

        return false;
    }

    public static bool IsReparsePoint(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        var attributes = File.GetAttributes(path);
        return (attributes & FileAttributes.ReparsePoint) != 0;
    }

    public static bool PathsEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(StripLocalExtendedPrefix(first))),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(StripLocalExtendedPrefix(second))),
            comparison);
    }

    public static string StripLocalExtendedPrefix(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (path.StartsWith(ExtendedUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[ExtendedUncPrefix.Length..];
        }

        if (path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal) &&
            path.Length >= ExtendedPathPrefix.Length + 2 &&
            char.IsLetter(path[ExtendedPathPrefix.Length]) &&
            path[ExtendedPathPrefix.Length + 1] == ':')
        {
            return path[ExtendedPathPrefix.Length..];
        }

        return path;
    }

    private static void EnsureLocalPathSyntax(string candidate)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
            candidate.StartsWith("//", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("نسخهٔ MVP فقط با دیسک محلی کار می‌کند؛ مسیر شبکه‌ای پشتیبانی نمی‌شود.");
        }
    }

    private static void EnsureLocalPath(string fullPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        EnsureLocalPathSyntax(fullPath);

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
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(StripLocalExtendedPrefix(candidate)));
        var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(StripLocalExtendedPrefix(parent)));

        if (string.Equals(normalizedCandidate, normalizedParent, comparison))
        {
            return true;
        }

        var parentWithSeparator = normalizedParent + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(parentWithSeparator, comparison);
    }
}
