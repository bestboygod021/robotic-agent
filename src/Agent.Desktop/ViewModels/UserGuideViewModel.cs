using Agent.Core.Monetization;
using Agent.Core.Storage;
using Agent.Core.Tools;
using Agent.Core.UX;
using System.Collections.ObjectModel;

namespace Agent.Desktop.ViewModels;

public sealed record LocalizedGuideSectionCard(
    string SectionId,
    string BadgeIcon,
    string Title,
    string Summary,
    IReadOnlyList<string> Steps,
    string ProTip);

/// <summary>
/// ViewModel for the Bilingual (Persian &amp; English) User Guide Window,
/// Interactive Prompt Coach, Smart Drop-Zone, and Pro/ROI Monetization Center.
/// </summary>
public sealed class UserGuideViewModel : ObservableObject
{
    private readonly LicenseAndPlanManager _licenseManager = new();
    private string _languageCode = "fa";
    private string _searchQuery = string.Empty;
    private string _promptGoalInput = string.Empty;
    private string _generatedPromptOutput = string.Empty;
    private string _licenseKeyInput = string.Empty;
    private string _licenseStatusMessage = string.Empty;
    private string _dropZoneSummary = string.Empty;

    public UserGuideViewModel(string initialLanguageCode = "fa")
    {
        _languageCode = string.Equals(initialLanguageCode, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fa";
        RefreshSections();
        UpdateLicenseStatusText();
    }

    public ObservableCollection<LocalizedGuideSectionCard> VisibleSections { get; } = [];
    public ObservableCollection<DropZoneQuickAction> SuggestedDropActions { get; } = [];

    public bool IsRightToLeft => !string.Equals(_languageCode, "en", StringComparison.OrdinalIgnoreCase);

    public string LanguageCode
    {
        get => _languageCode;
        set
        {
            var normalized = string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fa";
            if (SetProperty(ref _languageCode, normalized))
            {
                RaisePropertyChanged(nameof(IsRightToLeft));
                RaisePropertyChanged(nameof(WindowHeaderTitle));
                RaisePropertyChanged(nameof(WindowHeaderSubtitle));
                RefreshSections();
                UpdateLicenseStatusText();
            }
        }
    }

    public string WindowHeaderTitle =>
        IsRightToLeft
            ? "📖 راهنمای جامع کار با نرم‌افزار، پرامپت‌ساز هوشمند و مرکز نسخهٔ حرفه‌ای (FA / EN)"
            : "📖 Complete User Guide, Interactive Prompt Coach & Pro Center (FA / EN)";

    public string WindowHeaderSubtitle =>
        IsRightToLeft
            ? "آموزش گام‌به‌گام ۲۱ مهارت آفلاین، ۲۰ قالب شغلی، ۲۰۰ ابزار هوش مصنوعی، حباب Drag & Drop و مدیریت لایسنس"
            : "Step-by-step tutorial for 21 offline skills, 20 SMB playbooks, 200 AI tools, Magic Drop-Zone & Pro Licensing";

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshSections();
            }
        }
    }

    public string PromptGoalInput
    {
        get => _promptGoalInput;
        set => SetProperty(ref _promptGoalInput, value);
    }

    public string GeneratedPromptOutput
    {
        get => _generatedPromptOutput;
        private set => SetProperty(ref _generatedPromptOutput, value);
    }

    public string LicenseKeyInput
    {
        get => _licenseKeyInput;
        set => SetProperty(ref _licenseKeyInput, value);
    }

    public string LicenseStatusMessage
    {
        get => _licenseStatusMessage;
        private set => SetProperty(ref _licenseStatusMessage, value);
    }

    public string DropZoneSummary
    {
        get => _dropZoneSummary;
        private set => SetProperty(ref _dropZoneSummary, value);
    }

    public void TogglePersianEnglish()
    {
        LanguageCode = string.Equals(LanguageCode, "fa", StringComparison.OrdinalIgnoreCase) ? "en" : "fa";
    }

    public void RefreshSections()
    {
        var matches = BilingualUserGuideCatalog.SearchSections(SearchQuery, LanguageCode);
        VisibleSections.Clear();
        foreach (var sec in matches)
        {
            VisibleSections.Add(new LocalizedGuideSectionCard(
                sec.SectionId,
                sec.BadgeIcon,
                sec.GetTitle(LanguageCode),
                sec.GetSummary(LanguageCode),
                sec.GetSteps(LanguageCode),
                sec.GetProTip(LanguageCode)));
        }
    }

    public CoachedPromptPackage RunPromptCoach()
    {
        var goal = string.IsNullOrWhiteSpace(PromptGoalInput)
            ? (IsRightToLeft ? "نوشتن ایمیل رسمی خلاصهٔ فاکتورهای ماهانه برای مشتری" : "Write a professional monthly invoice summary email for a client")
            : PromptGoalInput;

        var pkg = InteractivePromptCoach.BuildCoachedPrompt(goal, languageCode: LanguageCode);
        var toolsLine = string.Join(" | ", pkg.RecommendedTools.Select(t => $"{t.Tool.Name} ({t.Tool.LaunchUrl})"));
        GeneratedPromptOutput = pkg.SafeEngineeredPrompt +
            Environment.NewLine + Environment.NewLine +
            (IsRightToLeft ? $"🔹 ابزارهای پیشنهادی کاتالوگ: {toolsLine}" : $"🔹 Recommended Catalog Tools: {toolsLine}");
        return pkg;
    }

    public void HandleDroppedFiles(IReadOnlyList<string> paths)
    {
        SuggestedDropActions.Clear();
        var actions = WindowsShellIntegrationService.AnalyzeDroppedItems(paths);
        foreach (var act in actions)
        {
            SuggestedDropActions.Add(act);
        }

        DropZoneSummary = IsRightToLeft
            ? $"{paths.Count} مورد دریافت شد — {actions.Count} اقدام هوشمند پیشنهاد گردید."
            : $"{paths.Count} item(s) dropped — {actions.Count} smart quick-actions suggested.";
    }

    public bool ActivateLicense()
    {
        var ok = _licenseManager.TryActivateLicenseKey(LicenseKeyInput);
        UpdateLicenseStatusText(ok);
        return ok;
    }

    public string ExportViralImpactCardSvg(IReadOnlyList<HistoryEntry> historyEntries)
    {
        var roi = RoiTelemetryDashboard.ComputeSummary(historyEntries);
        return ViralImpactCardGenerator.GenerateShareableSvgCard(roi);
    }

    private void UpdateLicenseStatusText(bool? lastAttemptSucceeded = null)
    {
        var ent = _licenseManager.CurrentEntitlements;
        if (lastAttemptSucceeded == false)
        {
            LicenseStatusMessage = IsRightToLeft
                ? "کلید لایسنس واردشده معتبر نیست."
                : "The entered license key is invalid.";
            return;
        }

        LicenseStatusMessage = IsRightToLeft
            ? $"سطح فعلی: {ent.TierDisplayNameFa} (سقف هر نوبت: {ent.MaxFilesPerBatch} فایل)"
            : $"Current Plan: {ent.TierDisplayNameEn} (Batch cap: {ent.MaxFilesPerBatch} files)";
    }
}
