using Agent.Core.Intents;
using Agent.Core.Skills;
using Agent.Core.Resources;
using Agent.Core.Storage;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Agent.Desktop;

public partial class MainWindow : Window
{
    private readonly IntentRouter _intentRouter = new();
    private readonly FileCopySkill _copySkill = new();
    private readonly WindowsAppLauncher _appLauncher = new();
    private readonly WindowsSystemResourceMonitor _systemResourceMonitor = new();
    private readonly WindowsHardwareProbe _hardwareProbe;
    private readonly ResourceGovernor _resourceGovernor = new();
    private readonly ResourcePreferenceStore _resourcePreferenceStore = new();
    private readonly LocalHistoryStore _historyStore = new();
    private readonly DispatcherTimer _resourceRefreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _resourceRefreshLock = new(1, 1);
    private readonly ObservableCollection<PreviewRow> _previewRows = [];
    private readonly ObservableCollection<HistoryEntry> _historyRows = [];

    private string? _sourceDirectory;
    private string? _destinationDirectory;
    private FileCopyRequest? _currentRequest;
    private FileCopyPreview? _currentPreview;
    private CancellationTokenSource? _cancellationTokenSource;
    private SystemHardwareProfile? _hardwareProfile;
    private ResourceHardLimits? _resourceHardLimits;
    private ResourcePreferences _resourcePreferences = ResourcePreferences.Balanced;
    private bool _updatingResourceControls;
    private bool _resourceSettingsDirty;
    private bool _isBusy;
    private bool _isPreparingCopyExecution;
    private bool _closeAfterCancellation;

    public MainWindow()
    {
        _hardwareProbe = new WindowsHardwareProbe(_systemResourceMonitor);
        InitializeComponent();
        PreviewList.ItemsSource = _previewRows;
        HistoryList.ItemsSource = _historyRows;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _resourceRefreshTimer.Tick += ResourceRefreshTimer_Tick;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadAvailableApps();
        }
        catch (Exception exception)
        {
            SetStatus($"فهرست برنامه‌ها بارگذاری نشد؛ کپی فایل همچنان ممکن است. {ToFriendlyMessage(exception)}", isError: true);
        }

        try
        {
            await RefreshHistoryAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"تاریخچهٔ محلی بارگذاری نشد. {ToFriendlyMessage(exception)}", isError: true);
        }

        try
        {
            await RefreshResourceProfileAsync();
            _resourceRefreshTimer.Start();
        }
        catch (Exception exception)
        {
            HardwareSummaryText.Text = "شناسایی سخت‌افزار انجام نشد؛ تنظیمات محافظه‌کارانه فعال نمی‌شوند.";
            ResourceBudgetSummaryText.Text = ToFriendlyMessage(exception);
            SetResourceControlsEnabled(false);
        }
    }

    private void LoadAvailableApps()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var availableApps = AppCatalog.GetAvailableApps(systemDirectory);
        AppPicker.ItemsSource = availableApps;

        if (availableApps.Count == 0)
        {
            AppPicker.IsEnabled = false;
            LaunchAppButton.IsEnabled = false;
            AppAvailabilityText.Text = "برنامهٔ مجازی در پوشهٔ استاندارد ویندوز پیدا نشد.";
            return;
        }

        AppPicker.IsEnabled = true;
        AppPicker.SelectedIndex = 0;
        LaunchAppButton.IsEnabled = AppPicker.SelectedItem is AppDefinition;
        AppAvailabilityText.Text = "برنامهٔ انتخابی با تأیید شما، بدون فرمان یا دسترسی مدیر باز می‌شود.";
    }

    private void AppPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsInitialized && LaunchAppButton is not null)
        {
            LaunchAppButton.IsEnabled = _cancellationTokenSource is null && AppPicker.SelectedItem is AppDefinition;
        }
    }

    private async void LaunchAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppPicker.SelectedItem is not AppDefinition selectedApp)
        {
            SetStatus("ابتدا یکی از برنامه‌های موجود را انتخاب کنید.", isError: true);
            return;
        }

        AppLaunchPlan plan;
        try
        {
            var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            plan = AppCatalog.CreatePlan(selectedApp.Id, systemDirectory);
        }
        catch (Exception exception)
        {
            SetStatus(ToFriendlyMessage(exception), isError: true);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"برنامهٔ «{selectedApp.DisplayName}» از فهرست مجاز ویندوز باز می‌شود. هیچ فرمان، آرگومان یا دسترسی مدیریتی استفاده نمی‌شود. ادامه می‌دهید؟",
            "تأیید بازکردن برنامه",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true);
        try
        {
            _appLauncher.Launch(plan);
            var historySaved = true;
            try
            {
                await _historyStore.AddAsync(new HistoryEntry(
                    DateTimeOffset.UtcNow,
                    $"بازکردن برنامه: {selectedApp.DisplayName}",
                    0,
                    0,
                    0,
                    Cancelled: false,
                    Detail: "از فهرست مجاز ویندوز اجرا شد؛ مسیر اجرایی ذخیره نمی‌شود."));
                await RefreshHistoryAsync();
            }
            catch (Exception historyException)
            {
                historySaved = false;
                SetStatus($"برنامه باز شد، اما تاریخچه ذخیره نشد: {ToFriendlyMessage(historyException)}", isError: true);
            }

            if (historySaved)
            {
                SetStatus($"برنامهٔ «{selectedApp.DisplayName}» باز شد. هیچ فرمان دلخواهی اجرا نشد.");
            }
        }
        catch (Exception exception)
        {
            SetStatus(ToFriendlyMessage(exception), isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_cancellationTokenSource is null)
        {
            if (_isPreparingCopyExecution)
            {
                e.Cancel = true;
                _closeAfterCancellation = true;
                SetStatus("در حال پایان بررسی منابع؛ پنجره پس از لغو امنِ کپی بسته می‌شود.");
                return;
            }

            _resourceRefreshTimer.Stop();
            return;
        }

        e.Cancel = true;
        _closeAfterCancellation = true;
        _cancellationTokenSource.Cancel();
        SetStatus("در حال توقف امنِ کار جاری؛ پنجره پس از پایان بسته می‌شود.");
    }

    private void ParseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _intentRouter.Parse(RequestTextBox.Text);
        switch (result.Status)
        {
            case IntentStatus.Ready:
                ExtensionTextBox.Text = result.Extension ?? string.Empty;
                SetStatus(result.Message);
                InvalidatePreview();
                break;
            case IntentStatus.NeedsClarification:
                SetStatus(result.Message, isError: true);
                InvalidatePreview();
                break;
            default:
                SetStatus(result.Message, isError: true);
                InvalidatePreview();
                break;
        }
    }

    private void SourceButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectFolder("پوشهٔ مبدأ را انتخاب کنید.", _sourceDirectory);
        if (selected is null)
        {
            return;
        }

        _sourceDirectory = selected;
        SourcePathText.Text = selected;
        SourcePathText.ToolTip = selected;
        InvalidatePreview();
    }

    private void DestinationButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectFolder("پوشهٔ مقصد را انتخاب کنید.", _destinationDirectory);
        if (selected is null)
        {
            return;
        }

        _destinationDirectory = selected;
        DestinationPathText.Text = selected;
        DestinationPathText.ToolTip = selected;
        InvalidatePreview();
    }

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateRequest(out var request, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        SetBusy(true);
        SetStatus("در حال بررسی فایل‌ها؛ هنوز چیزی کپی نمی‌شود.");
        try
        {
            var preview = await Task.Run(() => _copySkill.Preview(request));
            _currentRequest = request;
            _currentPreview = preview;
            _previewRows.Clear();

            foreach (var candidate in preview.Candidates)
            {
                _previewRows.Add(new PreviewRow(
                    candidate.Name,
                    FormatBytes(candidate.SizeBytes),
                    candidate.AlreadyExists
                        ? "در مقصد هست؛ رد می‌شود"
                        : !preview.HasDiskHeadroom
                            ? "فضای آزاد دیسک ناکافی؛ اجرا متوقف است"
                            : "آمادهٔ کپی",
                    candidate.AlreadyExists || !preview.HasDiskHeadroom ? Brushes.DarkOrange : Brushes.SeaGreen));
            }

            PreviewSummaryText.Text = preview.Candidates.Count == 0
                ? "فایلی با این پسوند در پوشهٔ مبدأ پیدا نشد."
                : !preview.HasDiskHeadroom
                    ? $"فضای آزاد مقصد ({FormatBytes(preview.DestinationAvailableBytes)}) برای نگه‌داشتن حداقل ۱ گیگابایت فضای رزرو کافی نیست؛ هیچ فایلی کپی نمی‌شود. " +
                      $"نیاز فعلی: {FormatBytes(preview.TotalBytes)}."
                    : $"{preview.ReadyCount} فایل برای کپی آماده است؛ {preview.ExistingCount} فایل به‌دلیل وجود در مقصد رد می‌شود. " +
                      $"حجم قابل کپی: {FormatBytes(preview.TotalBytes)}. فقط فایل‌های همین پوشه بررسی می‌شوند؛ زیرپوشه‌ها وارد نمی‌شوند.";

            ExecuteButton.IsEnabled = preview.ReadyCount > 0;
            SetStatus(preview.ReadyCount > 0
                ? "پیش‌نمایش آماده است. فایل‌ها را بررسی کنید و برای ادامه «تأیید و کپی» را بزنید."
                : "کاری برای کپی وجود ندارد.");
        }
        catch (Exception exception)
        {
            InvalidatePreview();
            SetStatus(ToFriendlyMessage(exception), isError: true);
        }
        finally
        {
            SetBusy(false);
            ExecuteButton.IsEnabled = _currentPreview?.ReadyCount > 0;
        }
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRequest is null || _currentPreview is null || _currentPreview.ReadyCount == 0)
        {
            SetStatus("ابتدا پیش‌نمایش بسازید.", isError: true);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"آیا { _currentPreview.ReadyCount} فایل را از پوشهٔ مبدأ به پوشهٔ مقصد کپی می‌کنید؟\n\n" +
            "فایل‌های هم‌نام بازنویسی نمی‌شوند. هیچ فایلی حذف یا جابه‌جا نمی‌شود.",
            "تأیید کپی فایل‌ها",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _isPreparingCopyExecution = true;
        SetBusy(true);
        try
        {
            await RefreshResourceProfileAsync(loadSavedPreferences: false);
        }
        catch (Exception exception)
        {
            SetStatus($"وضعیت سخت‌افزار تازه نشد؛ برای جلوگیری از اجرای بدون سقف، کپی انجام نشد. {ToFriendlyMessage(exception)}", isError: true);
            FinishCopyPreflight(releaseBusy: true);
            return;
        }

        var effectiveResourceBudget = _resourceHardLimits is null
            ? null
            : _resourceGovernor.Apply(_resourcePreferences, _resourceHardLimits);
        if (effectiveResourceBudget is null || effectiveResourceBudget.IsPaused || effectiveResourceBudget.DiskIoBytesPerSecond <= 0)
        {
            SetStatus("بار یا فضای آزاد دستگاه از سقف امن عبور کرده است؛ کپی تا بهترشدن وضعیت منابع متوقف شد.", isError: true);
            FinishCopyPreflight(releaseBusy: true);
            return;
        }

        if (!FinishCopyPreflight(releaseBusy: false))
        {
            return;
        }

        var maxDiskBytesPerSecond = effectiveResourceBudget.DiskIoBytesPerSecond;
        var request = _currentRequest!;
        var preview = _currentPreview!;
        using var cancellation = new CancellationTokenSource();
        _cancellationTokenSource = cancellation;
        var progress = new Progress<FileCopyProgress>(UpdateProgress);

        SetBusy(true);
        CopyProgressBar.Visibility = Visibility.Visible;
        CopyProgressBar.IsIndeterminate = false;
        CopyProgressBar.Value = 0;
        SetStatus("کپی در حال انجام است؛ می‌توانید آن را متوقف کنید.");

        try
        {
            var result = await Task.Run(() =>
                _copySkill.ExecuteAsync(request, preview, progress, cancellation.Token, maxDiskBytesPerSecond));

            var entry = new HistoryEntry(
                DateTimeOffset.UtcNow,
                "کپی فایل‌ها",
                result.Copied,
                result.Skipped,
                result.Failed,
                result.Cancelled);

            var historySaved = true;
            try
            {
                await _historyStore.AddAsync(entry);
                await RefreshHistoryAsync();
            }
            catch (Exception historyException)
            {
                historySaved = false;
                SetStatus($"عملیات فایل انجام شد، اما تاریخچه ذخیره نشد: {ToFriendlyMessage(historyException)}", isError: true);
            }

            PreviewSummaryText.Text =
                $"پایان اجرا: {result.Copied} کپی شد، {result.Skipped} رد شد، {result.Failed} خطا. " +
                (result.Cancelled ? "اجرا با درخواست شما متوقف شد." : "برای اجرای دوباره، پیش‌نمایش تازه بسازید.");

            if (result.FailedFileNames.Count > 0)
            {
                var names = string.Join("، ", result.FailedFileNames.Take(8));
                SetStatus($"برخی فایل‌ها کپی نشدند: {names}", isError: true);
            }
            else if (result.Cancelled)
            {
                SetStatus("اجرا متوقف شد؛ فایل‌هایی که پیش از توقف کپی شده‌اند در مقصد باقی می‌مانند.");
            }
            else if (historySaved)
            {
                SetStatus("کپی تمام شد. گزارش محلی فقط تعداد نتیجه‌ها را نگه می‌دارد؛ مسیر فایل‌ها ذخیره نمی‌شوند.");
            }

            _currentRequest = null;
            _currentPreview = null;
            ExecuteButton.IsEnabled = false;
        }
        catch (Exception exception)
        {
            SetStatus(ToFriendlyMessage(exception), isError: true);
            _currentRequest = null;
            _currentPreview = null;
            ExecuteButton.IsEnabled = false;
        }
        finally
        {
            _cancellationTokenSource = null;
            CopyProgressBar.Visibility = Visibility.Collapsed;
            SetBusy(false);
            if (_closeAfterCancellation)
            {
                _closeAfterCancellation = false;
                Close();
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        SetStatus("درخواست توقف ثبت شد؛ پس از پایان امن فایل جاری، اجرا متوقف می‌شود.");
    }

    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "تاریخچهٔ محلی پاک شود؟ این کار فایل‌های کپی‌شده را تغییر نمی‌دهد.",
            "پاک‌کردن تاریخچه",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _historyStore.ClearAsync();
            _historyRows.Clear();
            SetStatus("تاریخچهٔ محلی پاک شد.");
        }
        catch (Exception exception)
        {
            SetStatus(ToFriendlyMessage(exception), isError: true);
        }
    }

    private void RequestTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsInitialized || ExecuteButton is null)
        {
            return;
        }

        // A changed natural-language request must not silently reuse an old filter.
        if (!string.IsNullOrWhiteSpace(ExtensionTextBox.Text))
        {
            ExtensionTextBox.Clear();
        }

        InvalidatePreview();
    }

    private void PlanInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsInitialized && ExecuteButton is not null)
        {
            InvalidatePreview();
        }
    }

    private bool TryCreateRequest(out FileCopyRequest request, out string error)
    {
        request = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(_sourceDirectory) || string.IsNullOrWhiteSpace(_destinationDirectory))
        {
            error = "پوشهٔ مبدأ و مقصد را انتخاب کنید.";
            return false;
        }

        var intent = _intentRouter.Parse(RequestTextBox.Text);
        if (intent.Status == IntentStatus.Unsupported)
        {
            error = intent.Message;
            return false;
        }

        var extension = string.IsNullOrWhiteSpace(ExtensionTextBox.Text)
            ? intent.Extension
            : ExtensionTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(extension))
        {
            error = intent.Message;
            return false;
        }

        request = new FileCopyRequest(_sourceDirectory, _destinationDirectory, extension);
        return true;
    }

    private string? SelectFolder(string title, string? currentFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(currentFolder) && Directory.Exists(currentFolder))
        {
            dialog.InitialDirectory = currentFolder;
        }

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private async void ResourceRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_isBusy ||
            ResourceIntensitySlider.IsMouseCaptureWithin || ResourceIntensitySlider.IsKeyboardFocusWithin ||
            CpuShareSlider.IsMouseCaptureWithin || CpuShareSlider.IsKeyboardFocusWithin ||
            MemoryShareSlider.IsMouseCaptureWithin || MemoryShareSlider.IsKeyboardFocusWithin ||
            DiskShareSlider.IsMouseCaptureWithin || DiskShareSlider.IsKeyboardFocusWithin)
        {
            return;
        }

        try
        {
            await RefreshResourceProfileAsync(loadSavedPreferences: false);
        }
        catch (Exception exception)
        {
            _hardwareProfile = null;
            _resourceHardLimits = null;
            HardwareSummaryText.Text = "وضعیت سخت‌افزار فعلاً در دسترس نیست؛ پردازش‌های نیازمند سقف منابع متوقف می‌شوند.";
            ResourceBudgetSummaryText.Text = ToFriendlyMessage(exception);
            UpdateResourceControlAvailability();
        }
    }

    private async Task RefreshResourceProfileAsync(bool loadSavedPreferences = true)
    {
        await _resourceRefreshLock.WaitAsync();
        try
        {
            if (loadSavedPreferences)
            {
                _resourcePreferences = await _resourcePreferenceStore.LoadAsync();
            }

            var profile = await _hardwareProbe.DetectAsync();
            if (!profile.IsValid)
            {
                throw new InvalidOperationException("Windows سخت‌افزار را به‌طور کامل و معتبر گزارش نکرد.");
            }

            _hardwareProfile = profile;
            _resourceHardLimits = _resourceGovernor.CalculateHardLimits(profile);

            _updatingResourceControls = true;
            try
            {
                ResourceIntensitySlider.Maximum = _resourceHardLimits.MaxIntensityPercent;
                ResourceIntensitySlider.Value = Math.Clamp(
                    _resourcePreferences.RequestedIntensityPercent,
                    0,
                    _resourceHardLimits.MaxIntensityPercent);
                CpuShareSlider.Value = _resourcePreferences.Mix.Cpu;
                MemoryShareSlider.Value = _resourcePreferences.Mix.Memory;
                DiskShareSlider.Value = _resourcePreferences.Mix.DiskIo;
            }
            finally
            {
                _updatingResourceControls = false;
            }

            if (loadSavedPreferences)
            {
                _resourceSettingsDirty = false;
            }

            UpdateResourceControlAvailability();
            UpdateResourceSummary();
        }
        finally
        {
            _resourceRefreshLock.Release();
        }
    }

    private void UpdateResourceSummary()
    {
        if (_hardwareProfile is null || _resourceHardLimits is null)
        {
            return;
        }

        var profile = _hardwareProfile;
        var processor = string.IsNullOrWhiteSpace(profile.ProcessorName) ? "CPU نامشخص" : profile.ProcessorName;
        var processorFrequency = profile.ReportedProcessorMHz is int mhz ? $"، {mhz} مگاهرتز گزارش‌شده" : string.Empty;
        var power = profile.PowerSource switch
        {
            ResourcePowerSource.Ac => "برق شهری",
            ResourcePowerSource.Battery => $"باتری{(profile.BatteryPercent is int percent ? $" ({percent}٪)" : string.Empty)}",
            _ => "منبع برق نامشخص"
        };
        var volumes = profile.StorageVolumes.Count == 0
            ? "volume ثابت شناسایی نشد"
            : string.Join("؛ ", profile.StorageVolumes.Take(3).Select(volume =>
                $"{volume.Name} آزاد: {FormatBytes(volume.AvailableBytes)}"));
        HardwareSummaryText.Text =
            $"{processor}{processorFrequency} • {profile.LogicalProcessorCount} پردازندهٔ منطقی • " +
            $"RAM: {FormatBytes(profile.AvailablePhysicalMemoryBytes)} آزاد از {FormatBytes(profile.TotalPhysicalMemoryBytes)} • " +
            $"CPU فعلی: {profile.CurrentCpuPercent.ToString("0", CultureInfo.CurrentCulture)}٪ • {power}\n" +
            $"دیسک‌ها: {volumes}\n" +
            "GPU، دما و توان PSU در این دستگاه/رابط عمومی قابل‌خواندن نیستند یا در MVP استفاده نمی‌شوند.";

        var hard = _resourceHardLimits;
        var effective = _resourceGovernor.Apply(_resourcePreferences, hard);
        ResourceIntensityLabel.Text =
            $"شدت کلی: درخواست {_resourcePreferences.RequestedIntensityPercent}٪؛ سقف خودکار فعلی {hard.MaxIntensityPercent}٪";
        CpuShareLabel.Text = $"سهم CPU: {_resourcePreferences.Mix.Cpu}٪";
        MemoryShareLabel.Text = $"سهم RAM: {_resourcePreferences.Mix.Memory}٪";
        DiskShareLabel.Text = $"سهم دیسک: {_resourcePreferences.Mix.DiskIo}٪";
        ResourceBudgetSummaryText.Text =
            $"سقف سخت عامل: CPU {hard.MaxCpuPercent}٪، RAM {FormatBytes(hard.MaxMemoryBytes)}، " +
            $"دیسک {FormatBytes(hard.MaxDiskIoBytesPerSecond)}/ثانیه، حداکثر {hard.MaxCpuThreads} رشته.\n" +
            $"بودجهٔ مؤثر فعلی: CPU {effective.CpuPercent}٪، RAM {FormatBytes(effective.MemoryBytes)}، " +
            $"دیسک {FormatBytes(effective.DiskIoBytesPerSecond)}/ثانیه، {effective.CpuThreads} رشته.\n" +
            (effective.IsPaused ? $"توقف محافظه‌کارانه: {effective.Reason}" : effective.Reason) +
            " سقف سرعت دیسک روی کپی اعمال می‌شود؛ محدودیت CPU/RAM مدل تا اتصال runtime محلی قابل‌اجرا نمی‌شود.";
    }

    private void ResourceIntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingResourceControls || _resourceHardLimits is null)
        {
            return;
        }

        _resourcePreferences = _resourcePreferences with
        {
            RequestedIntensityPercent = (int)Math.Round(ResourceIntensitySlider.Value, MidpointRounding.AwayFromZero)
        };
        _resourceSettingsDirty = true;
        UpdateResourceSummary();
        UpdateResourceControlAvailability();
    }

    private void ResourceMixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingResourceControls || _resourceHardLimits is null || sender is not System.Windows.Controls.Slider slider)
        {
            return;
        }

        var resource = slider.Tag?.ToString() switch
        {
            "Cpu" => AdjustableResource.Cpu,
            "Memory" => AdjustableResource.Memory,
            "DiskIo" => AdjustableResource.DiskIo,
            _ => (AdjustableResource?)null
        };
        if (resource is null)
        {
            return;
        }

        var updatedMix = _resourceGovernor.Rebalance(
            _resourcePreferences.Mix,
            resource.Value,
            (int)Math.Round(slider.Value, MidpointRounding.AwayFromZero));
        _resourcePreferences = _resourcePreferences with { Mix = updatedMix };

        _updatingResourceControls = true;
        try
        {
            CpuShareSlider.Value = updatedMix.Cpu;
            MemoryShareSlider.Value = updatedMix.Memory;
            DiskShareSlider.Value = updatedMix.DiskIo;
        }
        finally
        {
            _updatingResourceControls = false;
        }

        _resourceSettingsDirty = true;
        UpdateResourceSummary();
        UpdateResourceControlAvailability();
    }

    private async void SaveResourceSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            await _resourcePreferenceStore.SaveAsync(_resourcePreferences);
            _resourceSettingsDirty = false;
            SetStatus("تنظیمات بودجهٔ منابع در دستگاه ذخیره شد؛ سقف‌های سخت همچنان قابل‌عبور نیستند.");
        }
        catch (Exception exception)
        {
            SetStatus($"تنظیمات منابع ذخیره نشد: {ToFriendlyMessage(exception)}", isError: true);
        }
        finally
        {
            SetBusy(false);
            UpdateResourceControlAvailability();
        }
    }

    private void UpdateResourceControlAvailability()
    {
        var available = !_isBusy && _resourceHardLimits is not null;
        ResourceIntensitySlider.IsEnabled = available && _resourceHardLimits!.MaxIntensityPercent > 0;
        CpuShareSlider.IsEnabled = available;
        MemoryShareSlider.IsEnabled = available;
        DiskShareSlider.IsEnabled = available;
        SaveResourceSettingsButton.IsEnabled = available && _resourceSettingsDirty;
    }

    private void SetResourceControlsEnabled(bool enabled)
    {
        ResourceIntensitySlider.IsEnabled = enabled;
        CpuShareSlider.IsEnabled = enabled;
        MemoryShareSlider.IsEnabled = enabled;
        DiskShareSlider.IsEnabled = enabled;
        SaveResourceSettingsButton.IsEnabled = enabled && _resourceSettingsDirty;
    }

    private async Task RefreshHistoryAsync()
    {
        var entries = await _historyStore.LoadAsync();
        _historyRows.Clear();
        foreach (var entry in entries.Take(30))
        {
            _historyRows.Add(entry);
        }
    }

    private void UpdateProgress(FileCopyProgress progress)
    {
        if (progress.Total > 0)
        {
            CopyProgressBar.Value = 100d * progress.Completed / progress.Total;
        }

        SetStatus($"در حال کپی: {progress.FileName} ({progress.Completed} از {progress.Total})");
    }

    private void InvalidatePreview()
    {
        _currentPreview = null;
        _currentRequest = null;
        ExecuteButton.IsEnabled = false;
        _previewRows.Clear();
        PreviewSummaryText.Text = "برای مشاهدهٔ فایل‌های دقیق، پیش‌نمایش تازه بسازید.";
    }

    private bool FinishCopyPreflight(bool releaseBusy)
    {
        _isPreparingCopyExecution = false;
        if (_closeAfterCancellation)
        {
            _closeAfterCancellation = false;
            SetBusy(false);
            Close();
            return false;
        }

        if (releaseBusy)
        {
            SetBusy(false);
        }

        return true;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        RequestTextBox.IsEnabled = !busy;
        ExtensionTextBox.IsEnabled = !busy;
        SourceButton.IsEnabled = !busy;
        DestinationButton.IsEnabled = !busy;
        ParseButton.IsEnabled = !busy;
        PreviewButton.IsEnabled = !busy;
        ClearHistoryButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy && _cancellationTokenSource is not null;
        AppPicker.IsEnabled = !busy && AppPicker.Items.Count > 0;
        LaunchAppButton.IsEnabled = !busy && AppPicker.SelectedItem is AppDefinition;
        ExecuteButton.IsEnabled = !busy && _currentPreview?.ReadyCount > 0;
        UpdateResourceControlAvailability();
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(102, 112, 133));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} بایت";
        }

        var kilobytes = bytes / 1024d;
        if (kilobytes < 1024)
        {
            return $"{kilobytes.ToString("0.#", CultureInfo.CurrentCulture)} کیلوبایت";
        }

        var megabytes = kilobytes / 1024d;
        return $"{megabytes.ToString("0.#", CultureInfo.CurrentCulture)} مگابایت";
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "این مسیر یا برنامه در فهرست مجاز نیست.",
        DirectoryNotFoundException => "یکی از پوشه‌های انتخاب‌شده دیگر در دسترس نیست؛ دوباره انتخاب کنید.",
        FileNotFoundException => "برنامهٔ موردنظر در این ویندوز در دسترس نیست.",
        PlatformNotSupportedException => "این قابلیت فقط در ویندوز پشتیبانی می‌شود.",
        System.ComponentModel.Win32Exception => "ویندوز نتوانست برنامهٔ انتخاب‌شده را باز کند.",
        NotSupportedException => exception.Message,
        ArgumentException => exception.Message,
        InvalidOperationException => exception.Message,
        IOException => "هنگام خواندن یا نوشتن فایل خطایی رخ داد. فایل‌های موجود بازنویسی نمی‌شوند.",
        _ => "عملیات انجام نشد. تنظیمات پوشه و مجوزهای کاربر را بررسی کنید."
    };
}

public sealed class PreviewRow
{
    public PreviewRow(string name, string sizeLabel, string statusLabel, Brush statusColor)
    {
        Name = name;
        SizeLabel = sizeLabel;
        StatusLabel = statusLabel;
        StatusColor = statusColor;
    }

    public string Name { get; }
    public string SizeLabel { get; }
    public string StatusLabel { get; }
    public Brush StatusColor { get; }
}
