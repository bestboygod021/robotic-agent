using Agent.Core.AI;
using Agent.Core.Resources;
using Agent.Core.Intents;
using Agent.Core.Skills;
using Agent.Core.Security;
using Agent.Core.Storage;

var classifier = new LocalIntentClassifier();
Check(classifier.Classify("از فایل‌ها یک نسخه بگیر").Label == IntentLabel.CopyFiles, "Bundled tiny model recognizes Persian copy paraphrases");
Check(classifier.Classify("فایل‌ها را منتقل کن").Label == IntentLabel.MoveFiles, "Bundled tiny model distinguishes move requests");
Check(classifier.Classify("فایل‌ها را حذف کن").Label == IntentLabel.DeleteFiles, "Bundled tiny model distinguishes deletion requests");
Check(classifier.Classify("ماشین حساب را باز کن").Label == IntentLabel.OpenApp, "Bundled tiny model distinguishes app-launch requests");

var router = new IntentRouter(classifier);

var routingPolicy = new AiRoutingPolicy();
var localEstimate = new LocalAiWorkloadEstimate(
    512L * 1024 * 1024,
    2,
    15,
    new TaskDurationEstimate(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(25), 0.65, "اندازه‌گیری محلی نمونه"));
var externalEstimate = new TaskDurationEstimate(
    TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(45), 0.45, "میانگین تأخیر ثبت‌شدهٔ ارائه‌دهنده");
var systemResources = new LocalSystemResources(8L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024, 8, 30);
var localBudget = new LocalAiResourceBudget(1024L * 1024 * 1024, 1024L * 1024 * 1024, 2, 25, 80, TimeSpan.FromMinutes(2));
var localOnlyDecision = routingPolicy.Decide(new AiRoutingRequest(
    AiExecutionMode.LocalOnly,
    AiTaskComplexity.Heavy,
    systemResources,
    localBudget,
    localEstimate,
    "provider-demo",
    ExternalProviderConfigured: true,
    ExternalEstimate: externalEstimate,
    ExternalApprovedForThisTask: true));
Check(localOnlyDecision.Action == AiRouteAction.RunLocal, "Local-only mode never routes a task to an external provider");
var lightweightDecision = routingPolicy.Decide(new AiRoutingRequest(
    AiExecutionMode.Hybrid,
    AiTaskComplexity.Lightweight,
    systemResources,
    localBudget,
    localEstimate,
    "provider-demo",
    ExternalProviderConfigured: true,
    ExternalEstimate: externalEstimate,
    ExternalApprovedForThisTask: true));
Check(lightweightDecision.Action == AiRouteAction.RunLocal, "Hybrid mode prefers the local model for lightweight tasks");
var heavyConsentDecision = routingPolicy.Decide(new AiRoutingRequest(
    AiExecutionMode.Hybrid,
    AiTaskComplexity.VeryHeavy,
    systemResources,
    localBudget,
    localEstimate,
    "provider-demo",
    ExternalProviderConfigured: true,
    ExternalEstimate: externalEstimate,
    ExternalApprovedForThisTask: false));
Check(heavyConsentDecision.Action == AiRouteAction.AskExternalConsent && heavyConsentDecision.LocalFallbackAvailable && heavyConsentDecision.DurationEstimate == externalEstimate, "Heavy tasks request explicit external consent and show both estimates");
var heavyApprovedDecision = routingPolicy.Decide(new AiRoutingRequest(
    AiExecutionMode.Hybrid,
    AiTaskComplexity.Heavy,
    systemResources,
    localBudget,
    localEstimate,
    "provider-demo",
    ExternalProviderConfigured: true,
    ExternalEstimate: externalEstimate,
    ExternalApprovedForThisTask: true));
Check(heavyApprovedDecision.Action == AiRouteAction.RunExternal, "External inference is selected for heavy tasks only after task-specific consent");
var constrainedLocalBudget = localBudget with { MaxModelMemoryBytes = 256L * 1024 * 1024 };
var noProviderDecision = routingPolicy.Decide(new AiRoutingRequest(
    AiExecutionMode.Hybrid,
    AiTaskComplexity.Lightweight,
    systemResources,
    constrainedLocalBudget,
    localEstimate,
    ExternalProviderId: null,
    ExternalProviderConfigured: false,
    ExternalEstimate: null,
    ExternalApprovedForThisTask: false));
Check(noProviderDecision.Action == AiRouteAction.CannotRun && noProviderDecision.LocalBudgetFailure == LocalBudgetFailure.ModelMemoryCapExceeded, "Local model resource limits prevent execution when no external provider is configured");
var cpuLimitedDecision = routingPolicy.Decide(new AiRoutingRequest(
    AiExecutionMode.LocalOnly,
    AiTaskComplexity.Lightweight,
    systemResources,
    localBudget,
    localEstimate with { EstimatedCpuPercent = 50 },
    ExternalProviderId: null,
    ExternalProviderConfigured: false,
    ExternalEstimate: null,
    ExternalApprovedForThisTask: false));
Check(cpuLimitedDecision.Action == AiRouteAction.CannotRun && cpuLimitedDecision.LocalBudgetFailure == LocalBudgetFailure.CpuUsageLimitExceeded, "Local model preflight honors the configured inference CPU ceiling");
var busySystemDecision = routingPolicy.Decide(new AiRoutingRequest(
    AiExecutionMode.LocalOnly,
    AiTaskComplexity.Lightweight,
    systemResources with { CurrentCpuPercent = 90 },
    localBudget,
    localEstimate,
    ExternalProviderId: null,
    ExternalProviderConfigured: false,
    ExternalEstimate: null,
    ExternalApprovedForThisTask: false));
Check(busySystemDecision.Action == AiRouteAction.CannotRun && busySystemDecision.LocalBudgetFailure == LocalBudgetFailure.SystemCpuBusy, "Local inference waits when the system CPU is already busy");

var hardwareProfile = new SystemHardwareProfile(
    ProcessorName: "Sample CPU",
    ReportedProcessorMHz: 3600,
    PhysicalCoreCount: null,
    LogicalProcessorCount: 8,
    TotalPhysicalMemoryBytes: 8L * 1024 * 1024 * 1024,
    AvailablePhysicalMemoryBytes: 4L * 1024 * 1024 * 1024,
    CurrentCpuPercent: 30,
    StorageVolumes: [new StorageVolumeInfo(
        Name: "C:\\",
        DriveType: "Fixed",
        FileSystem: "NTFS",
        TotalBytes: 512L * 1024 * 1024 * 1024,
        AvailableBytes: 300L * 1024 * 1024 * 1024,
        IsSystemVolume: true)],
    HasBattery: false,
    PowerSource: ResourcePowerSource.Ac,
    BatteryPercent: null,
    GraphicsAdapterName: null,
    GraphicsMemoryBytes: null,
    PowerSupplyCapacityWatts: null,
    ThermalSensorsAvailable: false,
    DetectedAtUtc: DateTimeOffset.UtcNow);
var resourceGovernor = new ResourceGovernor();
var hardLimits = resourceGovernor.CalculateHardLimits(hardwareProfile, systemResources);
Check(hardLimits.MaxCpuPercent <= 35 && hardLimits.MaxMemoryBytes <= 1024L * 1024 * 1024 && hardLimits.MaxDiskIoBytesPerSecond <= 10L * 1024 * 1024, "Hardware governor applies non-overridable CPU, memory and disk ceilings");
var effectiveBudget = resourceGovernor.Apply(ResourcePreferences.Balanced, hardLimits);
Check(effectiveBudget.CpuPercent <= hardLimits.MaxCpuPercent && effectiveBudget.MemoryBytes <= hardLimits.MaxMemoryBytes && effectiveBudget.DiskIoBytesPerSecond <= hardLimits.MaxDiskIoBytesPerSecond && effectiveBudget.MaxGpuPercent == 0, "Manual intensity remains below hardware-derived limits; the base runtime does not use GPU");
var maximumDiskPreference = resourceGovernor.Apply(
    new ResourcePreferences(100, new ResourceMixShares(30, 30, 40)),
    hardLimits);
Check(maximumDiskPreference.DiskIoBytesPerSecond == hardLimits.MaxDiskIoBytesPerSecond &&
      maximumDiskPreference.DiskIoBytesPerSecond <= FileCopySkill.MaximumCopyBytesPerSecond,
    "Even the highest manual disk priority cannot exceed the fixed copy I/O ceiling");
var raisedCpuShare = resourceGovernor.Rebalance(ResourceMixShares.Balanced, AdjustableResource.Cpu, 60);
Check(raisedCpuShare.IsValid && raisedCpuShare.Cpu == 60 && raisedCpuShare.Memory < 40 && raisedCpuShare.DiskIo < 20, "Increasing CPU priority automatically reallocates memory and disk shares");
var excessivePreference = resourceGovernor.Apply(new ResourcePreferences(200, raisedCpuShare), hardLimits);
Check(excessivePreference.WasClamped && excessivePreference.CpuPercent <= hardLimits.MaxCpuPercent && excessivePreference.MemoryBytes <= hardLimits.MaxMemoryBytes, "User settings cannot override hard safety ceilings");
var batteryProfile = hardwareProfile with { PowerSource = ResourcePowerSource.Battery, HasBattery = true, BatteryPercent = 30 };
var batteryLimits = resourceGovernor.CalculateHardLimits(batteryProfile, systemResources);
Check(batteryLimits.MaxCpuPercent <= 20 && batteryLimits.MaxIntensityPercent <= 60 && batteryLimits.MaxDiskIoBytesPerSecond <= 3L * 1024 * 1024, "Battery operation receives lower non-overridable resource ceilings");
var busyLimits = resourceGovernor.CalculateHardLimits(hardwareProfile, systemResources with { CurrentCpuPercent = 90 });
Check(busyLimits.LocalWorkloadPaused && busyLimits.MaxIntensityPercent == 0, "High system CPU load pauses local workload");
Check(!resourceGovernor.HasWriteHeadroom(
    new StorageVolumeInfo("D:\\", "Fixed", "NTFS", 100L * 1024 * 1024 * 1024, 512L * 1024 * 1024, false),
    100L * 1024 * 1024), "Disk reserve prevents a write that would leave insufficient free space");

var pdfIntent = router.Parse("فایل‌های PDF را کپی کن");
Check(pdfIntent.Status == IntentStatus.Ready && pdfIntent.Extension == "pdf", "Persian PDF intent is recognized");

var arabicCharacters = router.Parse("فایل‌های PDF را با كپی کپی کن");
Check(arabicCharacters.Status == IntentStatus.Ready, "Arabic/Persian character variants are normalized");

var allFiles = router.Parse("همهٔ فایل‌ها را کپی کن");
Check(allFiles.Status == IntentStatus.Ready && allFiles.Extension == "*", "Explicit all-files request is recognized");
var allPdfFiles = router.Parse("همهٔ فایل‌های PDF را کپی کن");
Check(allPdfFiles.Status == IntentStatus.Ready && allPdfFiles.Extension == "pdf", "A stated file extension is honored even with the word all");

var clarification = router.Parse("فایل‌ها را کپی کن");
Check(clarification.Status == IntentStatus.NeedsClarification, "Missing extension requests clarification");

var inferredCopy = router.Parse("از فایل‌ها یک نسخه بگیر");
Check(inferredCopy.Status == IntentStatus.NeedsClarification && inferredCopy.Inferred, "Tiny local classifier recognizes a safe copy paraphrase");
var vagueCopy = router.Parse("یک نسخهٔ جدید بگیر");
Check(vagueCopy.Status == IntentStatus.Unsupported, "A copy-like phrase without file context is rejected");
var inferredPdf = router.Parse("برای فایل‌ها یک نسخه بگیر از نوع PDF");
Check(inferredPdf.Status == IntentStatus.Ready && inferredPdf.Extension == "pdf" && inferredPdf.Inferred, "Classifier maps a Persian paraphrase to a bounded file-copy intent");
var nonAction = router.Parse("همه فایل‌ها را نشان بده");
Check(nonAction.Status == IntentStatus.Unsupported, "Classifier suggestions require an explicit copy-like action phrase");

var deletion = router.Parse("فایل‌ها را حذف کن");
Check(deletion.Status == IntentStatus.Unsupported, "Deletion is not an available skill");
var negativeCopy = router.Parse("فایل‌های PDF را کپی نکن");
Check(negativeCopy.Status == IntentStatus.Unsupported, "Negative copy requests never become actions");

Check(FileCopySkill.NormalizeExtension("*.PDF") == "pdf", "Extension is normalized safely");
ExpectThrows<InvalidOperationException>(() => PathGuard.ValidateSelectedFolder(Path.GetPathRoot(Path.GetTempPath())!, "آزمون"), "Drive root is rejected");
if (OperatingSystem.IsWindows())
{
    ExpectThrows<UnauthorizedAccessException>(
        () => PathGuard.ValidateSelectedFolder(@"\\server\share", "شبکه"),
        "UNC network paths are rejected before checking remote availability");
}
ExpectThrows<ArgumentException>(() => FileCopySkill.NormalizeExtension("../*"), "Unsafe extension is rejected");

var temporaryRoot = Directory.CreateTempSubdirectory("robotic-agent-tests-");
try
{
    var source = Path.Combine(temporaryRoot.FullName, "source");
    var destination = Path.Combine(temporaryRoot.FullName, "destination");
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(destination);
    Directory.CreateDirectory(Path.Combine(source, "nested"));

    await File.WriteAllTextAsync(Path.Combine(source, "one.pdf"), "source-one");
    await File.WriteAllTextAsync(Path.Combine(source, "two.pdf"), "source-two");
    await File.WriteAllTextAsync(Path.Combine(source, "note.txt"), "not a PDF");
    await File.WriteAllTextAsync(Path.Combine(source, "nested", "nested.pdf"), "not recursive");
    await File.WriteAllTextAsync(Path.Combine(destination, "one.pdf"), "keep-existing");

    var skill = new FileCopySkill();
    var request = new FileCopyRequest(source, destination, "pdf");
    var preview = skill.Preview(request);

    Check(preview.Candidates.Count == 2, "Preview includes only top-level matching files");
    Check(preview.ReadyCount == 1 && preview.ExistingCount == 1, "Preview identifies collisions without overwriting");

    var result = await skill.ExecuteAsync(request, preview, progress: null, cancellationToken: CancellationToken.None, maxBytesPerSecond: FileCopySkill.MaximumCopyBytesPerSecond);
    Check(result.Copied == 1 && result.Skipped == 1 && result.Failed == 0, "Execution copies only the non-conflicting file");
    Check(await File.ReadAllTextAsync(Path.Combine(destination, "one.pdf")) == "keep-existing", "Existing destination file remains unchanged");
    Check(await File.ReadAllTextAsync(Path.Combine(destination, "two.pdf")) == "source-two", "Selected file is copied successfully");
    Check(File.Exists(Path.Combine(source, "two.pdf")), "Copy preserves the source file");

    var stalePreview = skill.Preview(request);
    await File.WriteAllTextAsync(Path.Combine(source, "three.pdf"), "new file after preview");
    await ExpectThrowsAsync<InvalidOperationException>(
        () => skill.ExecuteAsync(
            request,
            stalePreview,
            progress: null,
            cancellationToken: CancellationToken.None,
            maxBytesPerSecond: FileCopySkill.MaximumCopyBytesPerSecond),
        "Changed source contents require a new preview");

    var fakeSystemDirectory = Path.Combine(temporaryRoot.FullName, "fake-system");
    Directory.CreateDirectory(fakeSystemDirectory);
    await File.WriteAllTextAsync(Path.Combine(fakeSystemDirectory, "notepad.exe"), "test executable placeholder");
    await File.WriteAllTextAsync(Path.Combine(fakeSystemDirectory, "calc.exe"), "test executable placeholder");

    var availableApps = AppCatalog.GetAvailableApps(fakeSystemDirectory);
    Check(availableApps.Count == 2, "App picker exposes only allowlisted executables present in the system folder");
    var notepadPlan = AppCatalog.CreatePlan(AppId.Notepad, fakeSystemDirectory);
    Check(notepadPlan.ExecutablePath == Path.GetFullPath(Path.Combine(fakeSystemDirectory, "notepad.exe")), "App launch plan resolves to a fixed system-folder path");
    ExpectThrows<FileNotFoundException>(
        () => AppCatalog.CreatePlan(AppId.Paint, fakeSystemDirectory),
        "Unavailable allowlisted apps cannot be launched");
    ExpectThrows<NotSupportedException>(
        () => AppCatalog.CreatePlan((AppId)999, fakeSystemDirectory),
        "Unknown app identifiers are rejected");

    var preferencePath = Path.Combine(temporaryRoot.FullName, "local-data", "resource-preferences.json");
    var preferenceStore = new ResourcePreferenceStore(preferencePath);
    var customPreferences = new ResourcePreferences(70, raisedCpuShare);
    await preferenceStore.SaveAsync(customPreferences);
    var loadedPreferences = await preferenceStore.LoadAsync();
    Check(loadedPreferences == customPreferences, "Manual resource preferences round-trip locally");

    var historyPath = Path.Combine(temporaryRoot.FullName, "local-data", "history.json");
    var history = new LocalHistoryStore(historyPath);
    await history.AddAsync(new HistoryEntry(DateTimeOffset.UtcNow, "کپی فایل‌ها", 1, 1, 0, false));
    var entries = await history.LoadAsync();
    Check(entries.Count == 1 && entries[0].Copied == 1, "Local history round-trips without a database service");
    await history.AddAsync(new HistoryEntry(
        DateTimeOffset.UtcNow,
        "بازکردن برنامه: دفترچه یادداشت",
        0,
        0,
        0,
        Cancelled: false,
        Detail: "از فهرست مجاز اجرا شد؛ مسیر اجرایی ذخیره نمی‌شود."));
    var launchHistory = await history.LoadAsync();
    Check(launchHistory.Count == 2 && launchHistory[0].Summary.Contains("مسیر اجرایی ذخیره نمی‌شود", StringComparison.Ordinal), "Allowlisted app activity can be recorded without executable paths");
    var historyJson = await File.ReadAllTextAsync(historyPath);
    Check(!historyJson.Contains(source, StringComparison.Ordinal), "History does not contain source paths");
    await history.ClearAsync();
    Check((await history.LoadAsync()).Count == 0, "Local history can be cleared");
}
finally
{
    temporaryRoot.Delete(recursive: true);
}

Console.WriteLine("All Agent.Core checks passed.");

static void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException($"FAILED: {description}");
    }

    Console.WriteLine($"PASS: {description}");
}

static async Task ExpectThrowsAsync<TException>(Func<Task> action, string description) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        Console.WriteLine($"PASS: {description}");
        return;
    }

    throw new InvalidOperationException($"FAILED: {description}");
}

static void ExpectThrows<TException>(Action action, string description) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        Console.WriteLine($"PASS: {description}");
        return;
    }

    throw new InvalidOperationException($"FAILED: {description}");
}
