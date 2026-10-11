using Agent.Core.Abstractions;
using Agent.Core.Diagnostics;
using Agent.Core.Security;
using Agent.Core.Skills;
using Agent.Core.Workflows;
using Agent.Desktop.ViewModels;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Agent.Desktop;

public partial class WorkflowStudioWindow : Window
{
    private readonly WorkflowComposerViewModel _viewModel;
    private readonly WorkflowEngine _engine;
    private readonly PrivacyLeakRiskScanner _privacyScanner = new();
    private WorkflowExecutionResult? _lastResult;

    public WorkflowStudioWindow(string languageCode = "fa")
    {
        InitializeComponent();
        var registry = SkillRegistry.CreateDefault();
        _engine = new WorkflowEngine(registry);
        _viewModel = new WorkflowComposerViewModel(registry, _engine)
        {
            LanguageCode = languageCode
        };

        PopulatePlaybooksList();
    }

    private void PopulatePlaybooksList()
    {
        PlaybooksListBox.Items.Clear();
        foreach (var pb in PlaybookCatalog.All)
        {
            PlaybooksListBox.Items.Add(new ListBoxItem
            {
                Tag = pb.Id,
                Content = $"[{pb.Category}] {pb.GetTitle(_viewModel.LanguageCode)} ({pb.Steps.Count} گام)"
            });
        }

        if (PlaybooksListBox.Items.Count > 0)
        {
            PlaybooksListBox.SelectedIndex = 0;
        }
    }

    private void PlaybooksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaybooksListBox.SelectedItem is not ListBoxItem item || item.Tag is not string playbookId)
        {
            return;
        }

        _viewModel.LoadPlaybook(playbookId);
        SelectedPlaybookTitleText.Text = _viewModel.WorkflowTitle;
        ExecuteWorkflowButton.IsEnabled = false;
        StepPreviewListBox.Items.Clear();
        foreach (var s in _viewModel.Steps)
        {
            StepPreviewListBox.Items.Add($"گام {s.StepId}: مهارت [{s.SkillId}]");
        }

        StudioStatusText.Text = _viewModel.StatusMessage;
    }

    private void RunPrivacyScanButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var src = SourceDirTextBox.Text?.Trim() ?? string.Empty;
            var report = _privacyScanner.ScanFolder(src);
            PrivacyReportBanner.Visibility = Visibility.Visible;
            PrivacyReportText.Text = report.SummaryFa;

            if (report.RiskScoreZeroToHundred > 0)
            {
                _viewModel.LoadPlaybook("playbook.legal.gdpr_sanitization");
                SelectedPlaybookTitleText.Text = _viewModel.WorkflowTitle;
                StudioStatusText.Text = "قالب «پاک‌سازی کامل اطلاعات هویتی (PII) و فرادادهٔ مخفی» به‌صورت خودکار برای رفع ریسک بارگذاری شد.";
            }
        }
        catch (Exception ex)
        {
            StudioStatusText.Text = $"خطا در اسکن حریم خصوصی: {ex.Message}";
        }
    }

    private void DryRunPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.SourceDirectory = SourceDirTextBox.Text?.Trim() ?? string.Empty;
            _viewModel.OutputDirectory = OutputDirTextBox.Text?.Trim() ?? string.Empty;
            var plan = _viewModel.RunDryRunPreview();

            StepPreviewListBox.Items.Clear();
            foreach (var step in plan.Steps)
            {
                StepPreviewListBox.Items.Add(
                    $"گام {step.StepNumber}: {step.DisplayTitle} [{step.SkillId}] | آیتم‌های تخمینی: {step.EstimatedItemsCount} | خروجی: {step.PlannedOutputPath}");
            }

            ExecuteWorkflowButton.IsEnabled = plan.IsValid && plan.ExecutableStepCount > 0;
            StudioStatusText.Text = _viewModel.StatusMessage;
        }
        catch (Exception ex)
        {
            ExecuteWorkflowButton.IsEnabled = false;
            StudioStatusText.Text = $"خطا در پیش‌نمایش گردش‌کار: {ex.Message}";
        }
    }

    private async void ExecuteWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExecuteWorkflowButton.IsEnabled = false;
            var context = new DeterministicSkillContext(_viewModel.LanguageCode);
            _lastResult = await _viewModel.ExecuteApprovedWorkflowAsync(context);
            RollbackWorkflowButton.IsEnabled = _lastResult.Succeeded && _lastResult.ExecutedStepsCount > 0;
            StudioStatusText.Text = _viewModel.StatusMessage;
        }
        catch (Exception ex)
        {
            StudioStatusText.Text = $"خطا در اجرای گردش‌کار: {ex.Message}";
        }
    }

    private async void RollbackWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            return;
        }

        try
        {
            var rbResults = await _engine.RollbackWorkflowAsync(_lastResult);
            RollbackWorkflowButton.IsEnabled = false;
            StudioStatusText.Text = $"واگرد معکوس تمام {rbResults.Count} گام گردش‌کار با موفقیت انجام شد.";
        }
        catch (Exception ex)
        {
            StudioStatusText.Text = $"خطا در واگرد گردش‌کار: {ex.Message}";
        }
    }

    private async void ExportJsonButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var json = _viewModel.ExportAsJson();
            var outPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{_viewModel.WorkflowId}.agentflow.json");
            await File.WriteAllTextAsync(outPath, json, new UTF8Encoding(false));
            StudioStatusText.Text = $"فایل گردش‌کار در دسکتاپ ذخیره شد: {outPath}";
        }
        catch (Exception ex)
        {
            StudioStatusText.Text = $"خطا در برون‌ریزی JSON: {ex.Message}";
        }
    }
}
