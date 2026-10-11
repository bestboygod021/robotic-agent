using Agent.Core.Storage;
using Agent.Desktop.ViewModels;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Agent.Desktop;

public partial class UserGuideWindow : Window
{
    private readonly UserGuideViewModel _viewModel;
    private readonly LocalHistoryStore _historyStore = new();

    public UserGuideWindow(string initialLanguageCode = "fa")
    {
        InitializeComponent();
        _viewModel = new UserGuideViewModel(initialLanguageCode);
        ApplyViewModelToUi();
    }

    private void ToggleLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.TogglePersianEnglish();
        ApplyViewModelToUi();
    }

    private void SearchGuideBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchQuery = SearchGuideBox.Text ?? string.Empty;
        GuideSectionsList.ItemsSource = null;
        GuideSectionsList.ItemsSource = _viewModel.VisibleSections;
    }

    private void BuildPromptButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PromptGoalInput = PromptGoalTextBox.Text ?? string.Empty;
        _viewModel.RunPromptCoach();
        PromptCoachOutputBox.Text = _viewModel.GeneratedPromptOutput;
    }

    private void CopyPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptCoachOutputBox.Text))
        {
            _viewModel.PromptGoalInput = PromptGoalTextBox.Text ?? string.Empty;
            _viewModel.RunPromptCoach();
            PromptCoachOutputBox.Text = _viewModel.GeneratedPromptOutput;
        }

        Clipboard.SetText(PromptCoachOutputBox.Text);
    }

    private void ActivateLicenseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LicenseKeyInput = LicenseKeyTextBox.Text ?? string.Empty;
        _viewModel.ActivateLicense();
        LicenseStatusText.Text = _viewModel.LicenseStatusMessage;
    }

    private async void ExportImpactCardButton_Click(object sender, RoutedEventArgs e)
    {
        var entries = await _historyStore.LoadAsync();
        var svgContent = _viewModel.ExportViralImpactCardSvg(entries);
        var targetPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "RoboticAgent-Productivity-Card.svg");

        await File.WriteAllTextAsync(targetPath, svgContent, new UTF8Encoding(false));
        LicenseStatusText.Text = _viewModel.IsRightToLeft
            ? $"کارت گرافیکی کارنامهٔ بهره‌وری در دسکتاپ ذخیره شد: {targetPath}"
            : $"Shareable SVG Impact Card saved to Desktop: {targetPath}";
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            _viewModel.HandleDroppedFiles(files);
            var lines = _viewModel.SuggestedDropActions
                .Select(a => _viewModel.IsRightToLeft ? $"• {a.TitleFa} ({a.SkillId})" : $"• {a.TitleEn} ({a.SkillId})");
            DropZoneStatusText.Text = _viewModel.DropZoneSummary + Environment.NewLine + string.Join(Environment.NewLine, lines);
        }
    }

    private void ApplyViewModelToUi()
    {
        FlowDirection = _viewModel.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        HeaderTitleText.Text = _viewModel.WindowHeaderTitle;
        HeaderSubtitleText.Text = _viewModel.WindowHeaderSubtitle;
        LicenseStatusText.Text = _viewModel.LicenseStatusMessage;
        GuideSectionsList.ItemsSource = null;
        GuideSectionsList.ItemsSource = _viewModel.VisibleSections;
    }
}
