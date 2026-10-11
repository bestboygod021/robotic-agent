namespace Agent.Core.Automation;

public sealed record ActiveWindowSnapshot(
    string WindowClassName,
    string WindowTitle,
    bool IsModalDialog);

/// <summary>
/// Safe Dialog Guard (#40): detects unexpected modal dialogs, OS credential prompts,
/// or error popups in the target application and halts UI automation safely.
/// </summary>
public sealed class SafeDialogGuard
{
    private static readonly string[] BlockedDialogClasses =
    [
        "#32770", // Standard Win32 modal dialog / MessageBox
        "Credential Dialog Xaml Host",
        "ConsentUIWndClass"
    ];

    private static readonly string[] BlockedTitleKeywords =
    [
        "user account control", "windows security", "error", "warning", "delete",
        "خطا", "هشدار", "حذف", "امنیت ویندوز", "رمز عبور"
    ];

    public void EnsureWindowStateIsSafe(ActiveWindowSnapshot snapshot, string expectedWindowClass)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.IsModalDialog ||
            BlockedDialogClasses.Any(c => string.Equals(c, snapshot.WindowClassName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"پنجرهٔ دیالوگ غیرمنتظره («{snapshot.WindowTitle}» — کلاس {snapshot.WindowClassName}) ظاهر شد؛ اتوماسیون به صورت ایمن متوقف گردید.");
        }

        if (BlockedTitleKeywords.Any(kw => snapshot.WindowTitle.Contains(kw, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"پنجرهٔ هشدار یا امنیتی («{snapshot.WindowTitle}») تشخیص داده شد؛ اتوماسیون متوقف گردید.");
        }

        if (!string.IsNullOrWhiteSpace(expectedWindowClass) &&
            !string.Equals(snapshot.WindowClassName, expectedWindowClass, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"پنجرهٔ فعال فعلی ({snapshot.WindowClassName}) با پنجرهٔ هدف مجاز ({expectedWindowClass}) مطابقت ندارد.");
        }
    }
}
