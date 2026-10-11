using Agent.Core.Localization;
using Agent.Core.Tools;
using Agent.Desktop.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Agent.Desktop;

public partial class AiToolsWindow : Window
{
    private readonly AiToolsViewModel _viewModel;
    private bool _suppressSelectionEvents;

    public AiToolsWindow(AiToolsViewModel? viewModel = null)
    {
        _viewModel = viewModel ?? new AiToolsViewModel();
        InitializeComponent();

        DataContext = _viewModel;
        ToolList.ItemsSource = _viewModel.VisibleTools;
        CategoryPicker.ItemsSource = _viewModel.Categories;
        LanguagePicker.ItemsSource = _viewModel.Languages;

        SearchBox.TextChanged += SearchBox_TextChanged;
        CategoryPicker.SelectionChanged += CategoryPicker_SelectionChanged;
        Loaded += AiToolsWindow_Loaded;

        ApplyLocalizedChrome();
        UpdateResultsSummary();
    }

    private async void AiToolsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
            SyncPickersFromViewModel();
            ApplyLocalizedChrome();
            UpdateResultsSummary();
        }
        catch (Exception)
        {
            SyncPickersFromViewModel();
            ApplyLocalizedChrome();
            UpdateResultsSummary();
        }
    }

    private void SyncPickersFromViewModel()
    {
        _suppressSelectionEvents = true;
        try
        {
            LanguagePicker.SelectedValue = _viewModel.LanguageCode;
            CategoryPicker.SelectedValue = _viewModel.SelectedCategoryId;
            FavoritesOnlyCheck.IsChecked = _viewModel.OnlyFavorites;
            FreeTierOnlyCheck.IsChecked = _viewModel.OnlyFreeTier;
        }
        finally
        {
            _suppressSelectionEvents = false;
        }
    }

    private void ApplyLocalizedChrome()
    {
        var lang = _viewModel.LanguageCode;
        var localizer = Localizer.Shared;
        var langMeta = CultureCatalog.GetLanguage(lang);

        FlowDirection = langMeta.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        FontFamily = new FontFamily(langMeta.RecommendedFontFamily);

        Title = localizer.Get("catalog.windowTitle", lang);
        HeaderTitleText.Text = localizer.Get("catalog.windowTitle", lang);
        HeaderSubtitleText.Text = localizer.Get("catalog.headerSubtitle", lang);
        OfflineBadgeText.Text = localizer.Get("catalog.offlineBadge", lang);
        LanguageLabelText.Text = localizer.Get("app.languageLabel", lang);
        SearchLabelText.Text = localizer.Get("catalog.searchLabel", lang);
        ProblemBox.ToolTip = localizer.Get("catalog.matchmakerPlaceholder", lang);
        MatchmakerButton.Content = localizer.Get("catalog.matchmakerButton", lang);
        ClearMatchmakerButton.Content = localizer.Get("catalog.clearMatchmakerButton", lang);
        FavoritesOnlyCheck.Content = localizer.Get("catalog.favoritesOnly", lang);
        FreeTierOnlyCheck.Content = localizer.Get("catalog.freeTierOnly", lang);
        EmptyStateText.Text = localizer.Get("catalog.emptyState", lang);
        FooterDisclaimerText.Text = localizer.Get("catalog.footerDisclaimer", lang);
        UpdateCompareButtonLabel();
    }

    private void UpdateCompareButtonLabel()
    {
        var baseLabel = Localizer.Shared.Get("catalog.compareButton", _viewModel.LanguageCode);
        var countFormatted = CultureCatalog.FormatNumber(_viewModel.SelectedComparisonCount, _viewModel.LanguageCode);
        CompareToolsButton.Content = $"{baseLabel} ({countFormatted})";
    }

    private void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvents || LanguagePicker.SelectedValue is not string langCode)
        {
            return;
        }

        _viewModel.SetLanguage(langCode);
        SyncPickersFromViewModel();
        ApplyLocalizedChrome();
        UpdateResultsSummary();
    }

    private void CategoryPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvents)
        {
            return;
        }

        _viewModel.SelectedCategoryId = CategoryPicker.SelectedValue as string ?? "all";
        UpdateResultsSummary();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchQuery = SearchBox.Text;
        UpdateResultsSummary();
    }

    private void FilterOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectionEvents)
        {
            return;
        }

        _viewModel.OnlyFavorites = FavoritesOnlyCheck.IsChecked == true;
        _viewModel.OnlyFreeTier = FreeTierOnlyCheck.IsChecked == true;
        UpdateResultsSummary();
    }

    private void MatchmakerButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ProblemQuery = ProblemBox.Text;
        _viewModel.RunMatchmaker();
        UpdateResultsSummary();
    }

    private void ClearMatchmakerButton_Click(object sender, RoutedEventArgs e)
    {
        ProblemBox.Text = string.Empty;
        _viewModel.ClearMatchmaker();
        UpdateResultsSummary();
    }

    private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AiToolCardItem card })
        {
            return;
        }

        await _viewModel.ToggleFavoriteAsync(card);
        UpdateResultsSummary();
    }

    private void ComparisonCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: AiToolCardItem card })
        {
            return;
        }

        if (!_viewModel.ToggleComparisonSelection(card, out var limitMessage) && !string.IsNullOrWhiteSpace(limitMessage))
        {
            ShowInfoMessage(limitMessage);
        }

        UpdateCompareButtonLabel();
    }

    private void CompareToolsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var matrix = _viewModel.BuildComparisonMatrix();
            ComparisonList.ItemsSource = matrix.Entries;
            ComparisonPanel.Visibility = Visibility.Visible;
        }
        catch (InvalidOperationException ex)
        {
            ShowInfoMessage(ex.Message);
        }
    }

    private void CloseComparisonButton_Click(object sender, RoutedEventArgs e)
    {
        ComparisonPanel.Visibility = Visibility.Collapsed;
    }

    private void CopyPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AiToolCardItem card })
        {
            return;
        }

        try
        {
            Clipboard.SetText(card.StarterPrompt);
            ShowInfoMessage(_viewModel.LanguageCode == "fa"
                ? $"پرامپت آمادهٔ «{card.Name}» در کلیپ‌بورد کپی شد."
                : $"Starter prompt for \"{card.Name}\" copied to clipboard.");
        }
        catch (Exception)
        {
            ShowInfoMessage(card.StarterPrompt);
        }
    }

    private async void SafeHandoffButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AiToolCardItem card })
        {
            return;
        }

        try
        {
            var package = _viewModel.PrepareSafeHandoff(card, ProblemBox.Text);
            try
            {
                Clipboard.SetText(package.SanitizedPromptText);
            }
            catch (Exception)
            {
                // Clipboard access can fail if locked by another desktop process; continue launching.
            }

            await _viewModel.RecordToolOpenedAsync(card);
            LaunchBrowserUri(package.LaunchUri, card.LoginUrl);
        }
        catch (Exception ex)
        {
            ShowInfoMessage(ex.Message);
        }
    }

    private async void OpenToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AiToolCardItem card })
        {
            return;
        }

        if (!AiToolCatalog.TryGetSafeWebUri(card.LoginUrl, out var uri) || uri is null)
        {
            ShowInfoMessage("پیوند این ابزار معتبر نیست.");
            return;
        }

        await _viewModel.RecordToolOpenedAsync(card);
        LaunchBrowserUri(uri, card.LoginUrl);
    }

    private void LaunchBrowserUri(Uri uri, string fallbackDisplayUrl)
    {
        try
        {
            // The catalog contains only fixed HTTPS links. Navigation is user-initiated
            // and delegated to the OS browser; no prompt or file is sent by this app.
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
        {
            ShowInfoMessage($"مرورگر نتوانست پیوند را باز کند. نشانی ابزار: {fallbackDisplayUrl}");
        }
    }

    private void UpdateResultsSummary()
    {
        var visibleCount = _viewModel.VisibleTools.Count;
        var selectedCategory = CategoryPicker.SelectedItem as LocalizedCategoryOption;
        var categoryLabel = selectedCategory?.DisplayName ?? Localizer.Shared.Get("catalog.allCategories", _viewModel.LanguageCode);
        var visibleText = CultureCatalog.FormatNumber(visibleCount, _viewModel.LanguageCode);
        var totalText = CultureCatalog.FormatNumber(_viewModel.TotalToolCount, _viewModel.LanguageCode);

        ResultsText.Text = $"{categoryLabel}  •  {visibleText} / {totalText}";
        EmptyStateText.Visibility = visibleCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Layout-independent shortcuts using virtual Key + ModifierKeys (#10)
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key == Key.F)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.M)
            {
                ProblemBox.Focus();
                ProblemBox.SelectAll();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape && ComparisonPanel.Visibility == Visibility.Visible)
        {
            ComparisonPanel.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void ShowInfoMessage(string message)
    {
        var options = _viewModel.IsRightToLeft
            ? MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
            : MessageBoxOptions.None;

        MessageBox.Show(
            this,
            message,
            Title,
            MessageBoxButton.OK,
            MessageBoxImage.Information,
            MessageBoxResult.OK,
            options);
    }
}
