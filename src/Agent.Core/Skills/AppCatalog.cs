namespace Agent.Core.Skills;

public enum AppId
{
    Notepad,
    Calculator,
    Paint
}

public sealed record AppDefinition(
    AppId Id,
    string DisplayName,
    string ExecutableName,
    string Description);

public sealed record AppLaunchPlan(
    AppId Id,
    string DisplayName,
    string ExecutablePath);

/// <summary>
/// A fixed allowlist of classic Windows applications. The caller chooses an enum
/// id; callers never supply an executable path, command, or argument.
/// </summary>
public static class AppCatalog
{
    private static readonly AppDefinition[] Definitions =
    [
        new(AppId.Notepad, "دفترچه یادداشت", "notepad.exe", "برای نوشتن و ویرایش متن ساده"),
        new(AppId.Calculator, "ماشین‌حساب", "calc.exe", "ماشین‌حساب ویندوز"),
        new(AppId.Paint, "نقاشی (Paint)", "mspaint.exe", "برای مشاهده و ویرایش سادهٔ تصویر")
    ];

    public static IReadOnlyList<AppDefinition> GetAvailableApps(string systemDirectory)
    {
        if (string.IsNullOrWhiteSpace(systemDirectory) || !Directory.Exists(systemDirectory))
        {
            return Array.Empty<AppDefinition>();
        }

        var fullSystemDirectory = Path.GetFullPath(systemDirectory);
        return Definitions
            .Where(definition => File.Exists(Path.Combine(fullSystemDirectory, definition.ExecutableName)))
            .ToArray();
    }

    public static AppLaunchPlan CreatePlan(AppId appId, string systemDirectory)
    {
        var definition = Definitions.FirstOrDefault(candidate => candidate.Id == appId)
            ?? throw new NotSupportedException("این برنامه در فهرست مجاز وجود ندارد.");

        if (string.IsNullOrWhiteSpace(systemDirectory) || !Directory.Exists(systemDirectory))
        {
            throw new DirectoryNotFoundException("پوشهٔ برنامه‌های ویندوز پیدا نشد.");
        }

        var normalizedSystemDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(systemDirectory));
        var executablePath = Path.GetFullPath(Path.Combine(normalizedSystemDirectory, definition.ExecutableName));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var systemPrefix = normalizedSystemDirectory + Path.DirectorySeparatorChar;

        if (!executablePath.StartsWith(systemPrefix, comparison))
        {
            throw new UnauthorizedAccessException("مسیر برنامه خارج از پوشهٔ مجاز ویندوز قرار دارد.");
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("این برنامه در نسخهٔ فعلی ویندوز نصب یا در دسترس نیست.");
        }

        return new AppLaunchPlan(appId, definition.DisplayName, executablePath);
    }
}
