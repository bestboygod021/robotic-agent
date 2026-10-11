using Agent.Core.Security;
using System.Globalization;

namespace Agent.Core.UX;

public enum AppThemeKind
{
    Light,
    Dark,
    HighContrast
}

public sealed record ThemePalette(
    AppThemeKind Kind,
    string BackgroundHex,
    string SurfaceHex,
    string PrimaryTextHex,
    string SecondaryTextHex,
    string AccentHex,
    double PrimaryContrastRatio);

public sealed record CompanionWindowBounds(int Left, int Top, int Width, int Height, bool DockedRight);

/// <summary>
/// Theme Manager (#82), WCAG 2.2 AA Accessibility Helper (#83), and Compact
/// Companion Mode / Drag-and-Drop Validator (#88).
/// </summary>
public static class ThemeAndAccessibilityService
{
    public static ThemePalette GetThemePalette(AppThemeKind kind) => kind switch
    {
        AppThemeKind.Dark => BuildPalette(AppThemeKind.Dark, "#0C111D", "#161B26", "#F5F5F6", "#CECFD2", "#53B1FD"),
        AppThemeKind.HighContrast => BuildPalette(AppThemeKind.HighContrast, "#000000", "#000000", "#FFFFFF", "#FFFF00", "#00FFFF"),
        _ => BuildPalette(AppThemeKind.Light, "#F4F6FA", "#FFFFFF", "#182230", "#475467", "#1570EF")
    };

    public static double ComputeContrastRatio(string foregroundHex, string backgroundHex)
    {
        var l1 = ComputeRelativeLuminance(foregroundHex);
        var l2 = ComputeRelativeLuminance(backgroundHex);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return Math.Round((lighter + 0.05) / (darker + 0.05), 2);
    }

    public static string BuildAccessibleScreenReaderLabel(string controlTitle, string actionHint, string shortcutKey = "")
    {
        var baseText = $"{controlTitle.Trim()} — {actionHint.Trim()}";
        return string.IsNullOrWhiteSpace(shortcutKey) ? baseText : $"{baseText} ({shortcutKey.Trim()})";
    }

    public static CompanionWindowBounds CalculateCompanionBounds(int screenWidth, int screenHeight, bool dockRight = true)
    {
        var width = Math.Clamp(screenWidth / 4, 340, 440);
        var height = Math.Max(480, screenHeight - 80);
        var left = dockRight ? Math.Max(0, screenWidth - width - 16) : 16;
        return new CompanionWindowBounds(left, Top: 40, width, height, dockRight);
    }

    public static IReadOnlyList<string> ValidateDroppedFolders(IEnumerable<string> droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);
        var valid = new List<string>();
        foreach (var path in droppedPaths)
        {
            if (Directory.Exists(path))
            {
                valid.Add(PathGuard.ValidateSelectedFolder(path, "پوشهٔ رهاشده (Drag & Drop)"));
            }
        }

        return valid;
    }

    private static ThemePalette BuildPalette(
        AppThemeKind kind,
        string bg,
        string surface,
        string primaryText,
        string secondaryText,
        string accent) =>
        new(kind, bg, surface, primaryText, secondaryText, accent, ComputeContrastRatio(primaryText, surface));

    private static double ComputeRelativeLuminance(string hexColor)
    {
        var clean = hexColor.Trim().TrimStart('#');
        if (clean.Length != 6)
        {
            return 0;
        }

        var r = int.Parse(clean[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

        static double Channel(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
    }
}
