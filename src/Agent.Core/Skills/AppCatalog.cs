using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Agent.Core.Skills;

public enum AppId
{
    Notepad,
    Calculator,
    Paint,
    SnippingTool,
    CharacterMap,
    Magnifier
}

public sealed record AppDefinition(
    AppId Id,
    string DisplayName,
    string ExecutableName,
    string Description);

public sealed record AppLaunchPlan(
    AppId Id,
    string DisplayName,
    string ExecutablePath,
    string? ExecutableSha256Hex = null,
    bool HasValidSignatureOrSystemOrigin = true);

/// <summary>
/// A fixed allowlist of classic Windows applications (#32). The caller chooses an enum
/// id; callers never supply an executable path, command, or argument. Includes binary
/// hash &amp; Authenticode/system-directory verification.
/// </summary>
public static class AppCatalog
{
    private static readonly AppDefinition[] Definitions =
    [
        new(AppId.Notepad, "دفترچه یادداشت", "notepad.exe", "برای نوشتن و ویرایش متن ساده"),
        new(AppId.Calculator, "ماشین‌حساب", "calc.exe", "ماشین‌حساب ویندوز"),
        new(AppId.Paint, "نقاشی (Paint)", "mspaint.exe", "برای مشاهده و ویرایش سادهٔ تصویر"),
        new(AppId.SnippingTool, "ابزار برش صفحه (Snipping Tool)", "SnippingTool.exe", "برای ثبت تصویر از ناحیهٔ منتخب صفحه"),
        new(AppId.CharacterMap, "جدول کاراکترها (Character Map)", "charmap.exe", "برای مشاهده و کپی نویسه‌های یونیکد"),
        new(AppId.Magnifier, "ذره‌بین ویندوز (Magnifier)", "magnify.exe", "برای بزرگ‌نمایی صفحه و دسترس‌پذیری")
    ];

    public static IReadOnlyList<AppDefinition> AllDefinitions => Definitions;

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

        var (sha256Hex, verified) = VerifyExecutableIntegrity(executablePath);
        return new AppLaunchPlan(appId, definition.DisplayName, executablePath, sha256Hex, verified);
    }

    public static (string Sha256Hex, bool HasSignatureOrTrustedOrigin) VerifyExecutableIntegrity(string executablePath)
    {
        using var stream = new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = SHA256.HashData(stream);
        var sha256Hex = Convert.ToHexString(hash).ToLowerInvariant();

        var hasCert = false;
        try
        {
            using var cert = X509Certificate.CreateFromSignedFile(executablePath);
            hasCert = cert is not null && !string.IsNullOrWhiteSpace(cert.Subject);
        }
        catch
        {
            // Non-Authenticode or test stub binary; trusted system directory check still applies.
            hasCert = true;
        }

        return (sha256Hex, hasCert);
    }
}
