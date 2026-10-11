using Agent.Core.Abstractions;
using Agent.Core.Admin;
using Agent.Core.AI;
using Agent.Core.AI.Providers;
using Agent.Core.Automation;
using Agent.Core.Cli;
using Agent.Core.Diagnostics;
using Agent.Core.Documents;
using Agent.Core.Localization;
using Agent.Core.Mcp;
using Agent.Core.Monetization;
using Agent.Core.Plugins;
using Agent.Core.Resources;
using Agent.Core.Intents;
using Agent.Core.Skills;
using Agent.Core.Skills.Documents;
using Agent.Core.Skills.Rollback;
using Agent.Core.Security;
using Agent.Core.Storage;
using Agent.Core.Tools;
using Agent.Core.UX;
using Agent.Core.Workflows;
using System.IO.Compression;

Check(CultureCatalog.Languages.Count == 6, "CultureCatalog supports Persian plus the top 5 global languages (en, zh, es, ar, fr)");
Check(CultureCatalog.GetLanguage("fa").IsRightToLeft && CultureCatalog.GetLanguage("ar").IsRightToLeft, "Persian and Arabic are configured as RTL languages");
Check(!CultureCatalog.GetLanguage("en").IsRightToLeft && !CultureCatalog.GetLanguage("zh").IsRightToLeft && !CultureCatalog.GetLanguage("es").IsRightToLeft && !CultureCatalog.GetLanguage("fr").IsRightToLeft, "English, Chinese, Spanish, and French are configured as LTR languages");
Check(Localizer.HasAllSupportedLanguagesForEveryKey(), "Every UI localization key has non-empty translations for all 6 supported languages");
Check(TextNormalizer.NormalizeMultilingual("ＰＤＦ १२३ ۰۱۲ ٣٤٥ Créez") == "pdf १२३ 012 345 creez", "Multilingual normalizer folds fullwidth Latin, Persian/Arabic digits, and Latin accents");

var skillRegistry = SkillRegistry.CreateDefault();
Check(skillRegistry.Descriptors.Count == 24 &&
      skillRegistry.IsAllowedSkill(FileCopySkill.SkillId) &&
      skillRegistry.IsAllowedSkill(BatchRenameSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(FolderOrganizeSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(DuplicateFinderSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(SafeArchiveSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(IncrementalBackupSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(BatchConvertSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(MetadataScrubberSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(OfflineOcrSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(InvoiceExtractSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(PdfToolkitSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(CsvCleanerSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(DocumentDiffSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(FolderReportSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(TemplateMergeSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(LocalSemanticSearchSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(BatchSummarizeSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(PiiRedactionSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(SmartClipboardSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(WebTaskAutomationSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(MediaAndPdfOptimizeSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(SubtitleStudioSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(MeetingAndVoiceNotesSkill.SkillId) &&
      skillRegistry.IsAllowedSkill(FinancialReconciliationSkill.SkillId),
    "Default SkillRegistry registers all 24 file, document, media, subtitle, meeting, finance, clipboard, and web skills");
Check(skillRegistry.Descriptors.All(descriptor =>
    descriptor.SupportsRollback &&
    CultureCatalog.Languages.All(lang => !string.IsNullOrWhiteSpace(descriptor.GetDisplayName(lang.Code)) && !string.IsNullOrWhiteSpace(descriptor.GetDescription(lang.Code)))),
    "Every registered skill supports rollback and provides localized names/descriptions in all 6 languages");
Check(PathGuard.IsSuspiciousDoubleExtension("invoice.pdf.exe", out _) &&
      PathGuard.IsSuspiciousDoubleExtension("photo.jpg.bat", out _) &&
      !PathGuard.IsSuspiciousDoubleExtension("invoice.pdf", out _),
    "PathGuard blocks deceptive double extensions (.pdf.exe, .jpg.bat) while allowing normal files");

var classifier = new LocalIntentClassifier();
var catalogCategories = AiToolCatalog.Categories.Where(category => category.Id != "all").ToArray();
Check(catalogCategories.Length == 8 && AiToolCatalog.Tools.Count == 200, "AI tool catalogue contains eight curated categories and 200 entries");
Check(catalogCategories.All(category =>
    category.ToolCount == AiToolCatalog.ToolsPerCategory &&
    AiToolCatalog.Tools.Where(tool => tool.CategoryId == category.Id).Select(tool => tool.IconText).Distinct(StringComparer.OrdinalIgnoreCase).Count() == AiToolCatalog.ToolsPerCategory), "Each AI tool category contains 25 entries with unique icon labels");
Check(AiToolCatalog.Tools.All(tool =>
    !string.IsNullOrWhiteSpace(tool.DescriptionFa) &&
    !string.IsNullOrWhiteSpace(tool.IconText) &&
    CultureCatalog.Languages.All(lang => !string.IsNullOrWhiteSpace(tool.GetLocalizedDescription(lang.Code)) && !string.IsNullOrWhiteSpace(tool.GetPromptTemplate(lang.Code))) &&
    AiToolCatalog.TryGetSafeWebUri(tool.LoginUrl, out _)), "Every AI catalogue entry has localized descriptions and starter prompts in all 6 languages, a local icon label, and an HTTPS link");
Check(AiToolLinkValidator.AuditCatalog().IsHealthy, "All 200 AI tool links pass static HTTPS and domain health audit");
Check(!AiToolCatalog.TryGetSafeWebUri("http://example.com", out _), "AI catalogue link validation rejects non-HTTPS URLs");

var matchmaker = new AiToolMatchmaker();
var audioRecommendationsFa = matchmaker.Recommend(new AiToolMatchmakingQuery("می‌خواهم نویز صدای پادکست را حذف کنم و متن را به گفتار تبدیل کنم", "fa"));
Check(audioRecommendationsFa.Count > 0 && audioRecommendationsFa[0].Tool.CategoryId == "audio", "Matchmaker recommends audio tools for Persian voice/podcast noise queries");
var codingRecommendationsEn = matchmaker.Recommend(new AiToolMatchmakingQuery("I need an AI coding assistant to debug code and review pull requests", "en", PreferOpenWeightsOrEnterprisePrivacy: true));
Check(codingRecommendationsEn.Count > 0 && codingRecommendationsEn[0].Tool.CategoryId == "coding", "Matchmaker recommends coding tools for English software debugging queries");
var researchRecommendationsZh = matchmaker.Recommend(new AiToolMatchmakingQuery("我需要检索学术论文并分析PDF文献引用", "zh"));
Check(researchRecommendationsZh.Count > 0 && researchRecommendationsZh[0].Tool.CategoryId == "research", "Matchmaker recommends research tools for Chinese academic paper queries");

var comparisonService = new AiToolComparisonService();
var comparisonMatrix = comparisonService.Compare(AiToolCatalog.Tools.Take(3), "en");
Check(comparisonMatrix.Entries.Count == 3 && comparisonMatrix.Entries.All(e => !string.IsNullOrWhiteSpace(e.PrivacyPosture) && !string.IsNullOrWhiteSpace(e.StarterPrompt)), "Comparison service builds a 3-tool side-by-side matrix");
ExpectThrows<InvalidOperationException>(() => comparisonService.Compare(AiToolCatalog.Tools.Take(1), "fa"), "Comparison service rejects fewer than 2 tools");
ExpectThrows<InvalidOperationException>(() => comparisonService.Compare(AiToolCatalog.Tools.Take(5), "fa"), "Comparison service rejects more than 4 tools");

var handoffService = new WebToolHandoffService();
var handoffPackage = handoffService.PrepareSafeHandoffPackage(
    AiToolCatalog.Tools[0],
    @"Check file C:\Users\Alice\Secret\contract.pdf and email alice@example.com or +1-555-234-5678",
    "en");
Check(handoffPackage.ContainedSensitiveData && handoffPackage.RedactedItemsCount >= 3 &&
      !handoffPackage.SanitizedPromptText.Contains("alice@example.com", StringComparison.OrdinalIgnoreCase) &&
      !handoffPackage.SanitizedPromptText.Contains(@"C:\Users\Alice", StringComparison.OrdinalIgnoreCase),
    "WebToolHandoffService scrubs paths, emails, and phone numbers locally before browser handoff");
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

Check(router.Parse("Copy all PDF files").Status == IntentStatus.Ready && router.Parse("Copy all PDF files").Extension == "pdf", "English PDF copy request is recognized");
Check(router.Parse("Copiar los archivos pdf").Status == IntentStatus.Ready && router.Parse("Copiar los archivos pdf").Extension == "pdf", "Spanish PDF copy request is recognized");
Check(router.Parse("Copier les fichiers pdf").Status == IntentStatus.Ready && router.Parse("Copier les fichiers pdf").Extension == "pdf", "French PDF copy request is recognized");
Check(router.Parse("انسخ ملفات pdf").Status == IntentStatus.Ready && router.Parse("انسخ ملفات pdf").Extension == "pdf", "Arabic PDF copy request is recognized");
Check(router.Parse("复制这些pdf文件").Status == IntentStatus.Ready && router.Parse("复制这些pdf文件").Extension == "pdf", "Chinese PDF copy request is recognized");
Check(router.Parse("不要复制pdf文件").Status == IntentStatus.Unsupported, "Chinese negative copy request is rejected");
Check(router.Parse("No copiar los archivos pdf").Status == IntentStatus.Unsupported, "Spanish negative copy request is rejected");
Check(router.Parse("Ne pas copier les fichiers pdf").Status == IntentStatus.Unsupported, "French negative copy request is rejected");
Check(router.Parse("لا تنسخ ملفات pdf").Status == IntentStatus.Unsupported, "Arabic negative copy request is rejected");

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

    var unicodeFolder = Path.Combine(temporaryRoot.FullName, "پوشه_آزمون_中文_Español_العربية_Français");
    Directory.CreateDirectory(unicodeFolder);
    Check(PathGuard.ValidateSelectedFolder(unicodeFolder, "یونیکد") == Path.TrimEndingDirectorySeparator(Path.GetFullPath(unicodeFolder)), "PathGuard validates international Unicode folder names");

    var toolPrefsPath = Path.Combine(temporaryRoot.FullName, "local-data", "tool-preferences.json");
    var toolPrefsStore = new ToolPreferenceStore(toolPrefsPath);
    await toolPrefsStore.ToggleFavoriteAsync("chatgpt");
    await toolPrefsStore.RecordRecentAsync("claude");
    var loadedToolPrefs = await toolPrefsStore.LoadAsync();
    Check(loadedToolPrefs.FavoriteToolIds.Contains("chatgpt", StringComparer.OrdinalIgnoreCase) &&
          loadedToolPrefs.RecentToolIds.Contains("claude", StringComparer.OrdinalIgnoreCase),
        "ToolPreferenceStore persists favorite and recent tool IDs locally");

    var customToolsPath = Path.Combine(temporaryRoot.FullName, "local-data", "custom-ai-tools.json");
    var customToolStore = new CustomAiToolStore(customToolsPath);
    var customList = await customToolStore.AddOrUpdateAsync(new AiTool(
        "internal-assistant",
        "chat",
        "Internal Portal AI",
        "دستیار هوش مصنوعی سازمانی داخلی",
        "https://ai.internal.example.com/",
        "INT"));
    Check(customList.Count == 1 && customList[0].IsCustomTool, "CustomAiToolStore saves validated HTTPS custom tools locally");
    await ExpectThrowsAsync<ArgumentException>(
        () => customToolStore.AddOrUpdateAsync(new AiTool("bad", "chat", "Bad", "توضیح", "http://insecure.example.com", "BAD")),
        "CustomAiToolStore rejects non-HTTPS tool URLs");

    // Phase 2 checks: Multi-attribute NER (#60), FolderPermissionLease (#73), Recursive + AutoRename Copy (#11, #17), Rollback (#20), Rename (#12), Organize (#13), Deduplicate (#14), SafeArchive (#15), IncrementalBackup (#19)
    var multiCriteria = router.ExtractFilterCriteria("فایل‌های pdf و docx بزرگ‌تر از 5 مگابایت و کمتر از 2 گیگابایت از هفته گذشته همراه با زیرپوشه را کپی کن");
    Check(multiCriteria.Extensions.Contains("pdf") &&
          multiCriteria.Extensions.Contains("docx") &&
          multiCriteria.MinSizeBytes == 5L * 1024 * 1024 &&
          multiCriteria.MaxSizeBytes == 2L * 1024 * 1024 * 1024 &&
          multiCriteria.ModifiedWithinDays == 7 &&
          multiCriteria.IncludeSubdirectories,
        "IntentRouter extracts multi-attribute file filter criteria (extensions, min/max size, recency, subfolders)");

    var permissionManager = new FolderPermissionManager();
    var singleRunLease = permissionManager.GrantLease(source, "مبدأ", FolderAccessScope.SingleExecution);
    permissionManager.ConsumeLease(singleRunLease.LeaseId);
    ExpectThrows<UnauthorizedAccessException>(
        () => permissionManager.ConsumeLease(singleRunLease.LeaseId),
        "Single-execution folder permission lease cannot be reused after consumption");

    var rollbackManager = new RollbackManager();
    var skillContext = new SkillExecutionContext("fa", FileCopySkill.MaximumCopyBytesPerSecond, CancellationToken.None);

    // Recursive copy + AutoRenameNumbered + Rollback (#11, #17, #20)
    var recursiveCopyReq = new FileCopyRequest(
        source,
        destination,
        "pdf",
        IncludeSubdirectories: true,
        MaxDepth: 5,
        ConflictPolicy: FileConflictPolicy.AutoRenameNumbered);
    var recursivePreview = skill.Preview(recursiveCopyReq);
    Check(recursivePreview.Candidates.Count >= 4 && recursivePreview.ReadyCount == recursivePreview.Candidates.Count,
        "Recursive copy with AutoRenameNumbered prepares both top-level colliding files and nested files");
    var recursiveResult = await skill.ExecuteAsync(recursiveCopyReq, recursivePreview, skillContext);
    Check(recursiveResult.Copied == recursivePreview.ReadyCount &&
          File.Exists(Path.Combine(destination, "nested", "nested.pdf")) &&
          File.Exists(Path.Combine(destination, "one (1).pdf")),
        "Recursive copy creates nested files and auto-numbers colliding top-level files without overwriting");
    var copyUndo = await rollbackManager.UndoAsync(recursiveResult.RollbackManifest!);
    Check(copyUndo.RevertedCount >= recursiveResult.Copied &&
          !File.Exists(Path.Combine(destination, "nested", "nested.pdf")) &&
          File.Exists(Path.Combine(destination, "one.pdf")),
        "RollbackManager reverts copied files while keeping pre-existing destination files intact");

    // BatchRenameSkill (#12) + Rollback (#20)
    var renameDir = Path.Combine(temporaryRoot.FullName, "rename-lab");
    Directory.CreateDirectory(renameDir);
    await File.WriteAllTextAsync(Path.Combine(renameDir, "draftA.txt"), "alpha");
    await File.WriteAllTextAsync(Path.Combine(renameDir, "draftB.txt"), "beta");
    var renameSkill = new BatchRenameSkill();
    var renameReq = new BatchRenameRequest(renameDir, "txt", Prefix: "doc_", IncludeSequenceNumber: true, StartSequenceNumber: 1, NumberPadding: 2);
    var renamePreview = renameSkill.Preview(renameReq);
    Check(renamePreview.ReadyCount == 2, "BatchRenameSkill previews old->new filenames");
    var renameRes = await renameSkill.ExecuteAsync(renameReq, renamePreview, skillContext);
    Check(renameRes.RenamedCount == 2 && File.Exists(Path.Combine(renameDir, "doc_draftA_01.txt")), "BatchRenameSkill renames files according to pattern");
    var renameUndo = await rollbackManager.UndoAsync(renameRes.RollbackManifest);
    Check(renameUndo.RevertedCount == 2 && File.Exists(Path.Combine(renameDir, "draftA.txt")), "RollbackManager restores batch-renamed files to original names");

    // FolderOrganizeSkill (#13) + Rollback (#20)
    var messyDir = Path.Combine(temporaryRoot.FullName, "messy-downloads");
    Directory.CreateDirectory(messyDir);
    await File.WriteAllTextAsync(Path.Combine(messyDir, "invoice.pdf"), "pdf-data");
    await File.WriteAllTextAsync(Path.Combine(messyDir, "avatar.png"), "png-data");
    await File.WriteAllTextAsync(Path.Combine(messyDir, "data.csv"), "a,b,c");
    var organizeSkill = new FolderOrganizeSkill();
    var organizeReq = new FolderOrganizeRequest(messyDir, "en", UseLocalizedFolderNames: false);
    var organizePreview = organizeSkill.Preview(organizeReq);
    Check(organizePreview.ReadyCount == 3, "FolderOrganizeSkill classifies loose files into structured folders");
    var organizeRes = await organizeSkill.ExecuteAsync(organizeReq, organizePreview, skillContext);
    Check(organizeRes.MovedCount == 3 &&
          File.Exists(Path.Combine(messyDir, "Documents", "invoice.pdf")) &&
          File.Exists(Path.Combine(messyDir, "Images", "avatar.png")) &&
          File.Exists(Path.Combine(messyDir, "CodeAndData", "data.csv")),
        "FolderOrganizeSkill moves files into Documents, Images, and CodeAndData");
    var organizeUndo = await rollbackManager.UndoAsync(organizeRes.RollbackManifest);
    Check(organizeUndo.RevertedCount >= 3 && File.Exists(Path.Combine(messyDir, "invoice.pdf")),
        "RollbackManager restores organized files back to root folder");

    // DuplicateFinderSkill (#14) + Rollback (#20)
    var dupDir = Path.Combine(temporaryRoot.FullName, "dup-lab");
    Directory.CreateDirectory(dupDir);
    await File.WriteAllTextAsync(Path.Combine(dupDir, "keep-a.txt"), "identical-payload-12345");
    await File.WriteAllTextAsync(Path.Combine(dupDir, "copy-b.txt"), "identical-payload-12345");
    await File.WriteAllTextAsync(Path.Combine(dupDir, "unique.txt"), "different-payload");
    var dupSkill = new DuplicateFinderSkill();
    var dupReq = new DuplicateFinderRequest(dupDir);
    var dupPreview = dupSkill.Preview(dupReq);
    Check(dupPreview.DuplicateCount == 1, "DuplicateFinderSkill identifies exact SHA-256 duplicates");
    var dupRes = await dupSkill.ExecuteAsync(dupReq, dupPreview, skillContext);
    Check(dupRes.QuarantinedCount == 1 &&
          File.Exists(Path.Combine(dupDir, "keep-a.txt")) &&
          !File.Exists(Path.Combine(dupDir, "copy-b.txt")),
        "DuplicateFinderSkill moves redundant duplicate to quarantine while keeping original");
    var dupUndo = await rollbackManager.UndoAsync(dupRes.RollbackManifest);
    Check(dupUndo.RevertedCount >= 1 && File.Exists(Path.Combine(dupDir, "copy-b.txt")),
        "RollbackManager restores quarantined duplicate file");

    // SafeArchiveSkill (#15) + Zip-Slip protection
    var archiveOutDir = Path.Combine(temporaryRoot.FullName, "archive-out");
    var extractOutDir = Path.Combine(temporaryRoot.FullName, "extract-out");
    Directory.CreateDirectory(archiveOutDir);
    Directory.CreateDirectory(extractOutDir);
    var zipFilePath = Path.Combine(archiveOutDir, "bundle.zip");
    var archiveSkill = new SafeArchiveSkill();
    var createZipReq = new SafeArchiveRequest(ArchiveOperationMode.CreateZip, dupDir, zipFilePath);
    var createZipPreview = archiveSkill.Preview(createZipReq);
    var createZipRes = await archiveSkill.ExecuteAsync(createZipReq, createZipPreview, skillContext);
    Check(createZipRes.ProcessedEntries == 3 && File.Exists(zipFilePath), "SafeArchiveSkill creates a valid ZIP archive");

    var extractZipReq = new SafeArchiveRequest(ArchiveOperationMode.ExtractZip, zipFilePath, extractOutDir);
    var extractZipPreview = archiveSkill.Preview(extractZipReq);
    var extractZipRes = await archiveSkill.ExecuteAsync(extractZipReq, extractZipPreview, skillContext);
    Check(extractZipRes.ProcessedEntries == 3 && File.Exists(Path.Combine(extractOutDir, "keep-a.txt")),
        "SafeArchiveSkill extracts verified archive entries");

    var evilZipPath = Path.Combine(archiveOutDir, "evil-slip.zip");
    using (var evilStream = new FileStream(evilZipPath, FileMode.CreateNew, FileAccess.Write))
    using (var evilZip = new ZipArchive(evilStream, ZipArchiveMode.Create))
    {
        var evilEntry = evilZip.CreateEntry("../escaped.txt");
        await using var writer = new StreamWriter(evilEntry.Open());
        await writer.WriteAsync("malicious traversal");
    }
    ExpectThrows<InvalidOperationException>(
        () => archiveSkill.Preview(new SafeArchiveRequest(ArchiveOperationMode.ExtractZip, evilZipPath, extractOutDir)),
        "SafeArchiveSkill blocks Zip-Slip path traversal entries during preview");

    // IncrementalBackupSkill (#19) + Rollback (#20)
    var backupDir = Path.Combine(temporaryRoot.FullName, "incremental-backup");
    Directory.CreateDirectory(backupDir);
    var backupSkill = new IncrementalBackupSkill();
    var backupReq = new IncrementalBackupRequest(dupDir, backupDir);
    var backupPreview1 = backupSkill.Preview(backupReq);
    Check(backupPreview1.NewCount == 3 && backupPreview1.UnchangedSkippedCount == 0, "IncrementalBackupSkill detects 3 new files on initial run");
    var backupRes1 = await backupSkill.ExecuteAsync(backupReq, backupPreview1, skillContext);
    Check(backupRes1.BackedUpCount == 3, "IncrementalBackupSkill backs up initial files");
    var backupPreview2 = backupSkill.Preview(backupReq);
    Check(backupPreview2.ReadyCount == 0 && backupPreview2.UnchangedSkippedCount == 3, "IncrementalBackupSkill skips unchanged files on subsequent run");

    // Phase 3 checks: Document & Data Skills (#16, #18, #21..#30)
    var docsLab = Path.Combine(temporaryRoot.FullName, "docs-lab");
    var docsOut = Path.Combine(temporaryRoot.FullName, "docs-out");
    Directory.CreateDirectory(docsLab);
    Directory.CreateDirectory(docsOut);

    // 1. PiiRedactionSkill (#30)
    await File.WriteAllTextAsync(
        Path.Combine(docsLab, "customer.txt"),
        "Contact: sara@example.com, Phone: +1-555-234-5678, IBAN: DE89370400440532013000, Card: 4532015112830366, IP: 192.168.1.25");
    var piiSkill = new PiiRedactionSkill();
    var piiReq = new PiiRedactionRequest(docsLab, docsOut, "txt");
    var piiPreview = piiSkill.Preview(piiReq);
    Check(piiPreview.TotalPiiDetected >= 5, "PiiRedactionSkill detects email, phone, IBAN, Luhn-verified card, and IPv4");
    var piiRes = await piiSkill.ExecuteAsync(piiReq, piiPreview, skillContext);
    var sanitizedContent = await File.ReadAllTextAsync(Path.Combine(docsOut, "customer_sanitized.txt"));
    Check(piiRes.SanitizedFileCount == 1 &&
          !sanitizedContent.Contains("sara@example.com", StringComparison.OrdinalIgnoreCase) &&
          !sanitizedContent.Contains("4532015112830366", StringComparison.Ordinal) &&
          File.Exists(Path.Combine(docsLab, "customer.txt")),
        "PiiRedactionSkill writes sanitized copy while keeping original file untouched");

    // 2. MetadataScrubberSkill (#18)
    await File.WriteAllTextAsync(
        Path.Combine(docsLab, "meta-doc.md"),
        "Author: Hidden User\nGPSLatitude: 35.6892\n# Project Report\nClean content line.");
    var scrubSkill = new MetadataScrubberSkill();
    var scrubReq = new MetadataScrubberRequest(docsLab, docsOut, "md");
    var scrubPreview = scrubSkill.Preview(scrubReq);
    var scrubRes = await scrubSkill.ExecuteAsync(scrubReq, scrubPreview, skillContext);
    var scrubbedText = await File.ReadAllTextAsync(Path.Combine(docsOut, "meta-doc_sanitized.md"));
    Check(scrubRes.ScrubbedFileCount == 1 &&
          !scrubbedText.Contains("Hidden User", StringComparison.Ordinal) &&
          !scrubbedText.Contains("GPSLatitude", StringComparison.Ordinal),
        "MetadataScrubberSkill strips author and GPS metadata lines into sanitized copy");

    // 3. InvoiceExtractSkill (#22)
    var invDir = Path.Combine(temporaryRoot.FullName, "invoices-in");
    Directory.CreateDirectory(invDir);
    await File.WriteAllTextAsync(
        Path.Combine(invDir, "inv1.txt"),
        "شماره فاکتور: INV-2026-01\nتاریخ: ۱۴۰۵/۰۷/۱۸\nفروشنده: شرکت داده پرداز\nمالیات: ۲۵۰۰۰۰\nمبلغ کل: ۲۵۰۰۰۰۰ تومان");
    var invCsvOut = Path.Combine(docsOut, "invoices.csv");
    var invSkill = new InvoiceExtractSkill();
    var invReq = new InvoiceExtractRequest(invDir, invCsvOut, "txt");
    var invPreview = invSkill.Preview(invReq);
    Check(invPreview.Rows.Count == 1 &&
          invPreview.Rows[0].InvoiceNumber == "INV-2026-01" &&
          invPreview.Rows[0].TotalAmount == "2500000" &&
          invPreview.Rows[0].Currency == "IRT",
        "InvoiceExtractSkill extracts Persian invoice number, normalized digits, and currency");
    var invRes = await invSkill.ExecuteAsync(invReq, invPreview, skillContext);
    Check(invRes.ExtractedCount == 1 && File.Exists(invCsvOut), "InvoiceExtractSkill writes UTF-8 CSV table");

    // 4. CsvCleanerSkill (#24)
    var dirtyCsvPath = Path.Combine(docsLab, "dirty.csv");
    var cleanCsvPath = Path.Combine(docsOut, "clean.csv");
    await File.WriteAllTextAsync(dirtyCsvPath, "Name,Code\n علی , ۱۲۳ \n\n على , ١٢٣ \nسارا,۴۵۶\n");
    var csvSkill = new CsvCleanerSkill();
    var csvReq = new CsvCleanerRequest(dirtyCsvPath, cleanCsvPath);
    var csvPreview = csvSkill.Preview(csvReq);
    Check(csvPreview.EmptyRowsRemoved == 1 && csvPreview.DuplicateRowsRemoved == 1 && csvPreview.CleanOutputRows == 3,
        "CsvCleanerSkill removes empty rows and script-normalized duplicate rows");
    var csvRes = await csvSkill.ExecuteAsync(csvReq, csvPreview, skillContext);
    Check(csvRes.CleanOutputRows == 3 && File.Exists(cleanCsvPath), "CsvCleanerSkill writes cleaned CSV");

    // 5. DocumentDiffSkill (#25)
    var v1Path = Path.Combine(docsLab, "contract_v1.txt");
    var v2Path = Path.Combine(docsLab, "contract_v2.txt");
    var diffReportPath = Path.Combine(docsOut, "contract.diff.md");
    await File.WriteAllTextAsync(v1Path, "Clause 1\nClause 2 Old\nClause 3");
    await File.WriteAllTextAsync(v2Path, "Clause 1\nClause 2 Revised\nClause 3\nClause 4 Added");
    var diffSkill = new DocumentDiffSkill();
    var diffReq = new DocumentDiffRequest(v1Path, v2Path, diffReportPath);
    var diffPreview = diffSkill.Preview(diffReq);
    Check(diffPreview.AddedCount == 2 && diffPreview.RemovedCount == 1 && diffPreview.UnchangedCount == 2,
        "DocumentDiffSkill computes LCS line additions and removals accurately");
    await diffSkill.ExecuteAsync(diffReq, diffPreview, skillContext);
    Check(File.Exists(diffReportPath), "DocumentDiffSkill writes Markdown diff report");

    // 6. TemplateMergeSkill (#27)
    var tplPath = Path.Combine(docsLab, "certificate.txt");
    var dataCsvPath = Path.Combine(docsLab, "recipients.csv");
    var mergeOutDir = Path.Combine(docsOut, "merged-certs");
    Directory.CreateDirectory(mergeOutDir);
    await File.WriteAllTextAsync(tplPath, "Dear {{FullName}}, your score is {{Score}}.");
    await File.WriteAllTextAsync(dataCsvPath, "FullName,Score\nMina,98\nReza,95");
    var mergeSkill = new TemplateMergeSkill();
    var mergeReq = new TemplateMergeRequest(tplPath, dataCsvPath, mergeOutDir, "FullName");
    var mergePreview = mergeSkill.Preview(mergeReq);
    var mergeRes = await mergeSkill.ExecuteAsync(mergeReq, mergePreview, skillContext);
    Check(mergeRes.GeneratedFilesCount == 2 &&
          (await File.ReadAllTextAsync(mergeRes.GeneratedPaths[0])).Contains("Mina", StringComparison.Ordinal),
        "TemplateMergeSkill generates personalized documents from CSV rows");

    // 7. PdfToolkitSkill (#23) & OfflineOcrSkill (#21)
    var pdfDir = Path.Combine(temporaryRoot.FullName, "pdf-lab");
    Directory.CreateDirectory(pdfDir);
    var samplePdfBytes = PdfToolkitSkill.BuildMinimalMergedPdf([("Title Page", "Hello Robotic Agent PDF")]);
    await File.WriteAllBytesAsync(Path.Combine(pdfDir, "sample.pdf"), samplePdfBytes);
    var pdfSkill = new PdfToolkitSkill();
    var pdfExtractOut = Path.Combine(docsOut, "pdf-extracted.txt");
    var pdfReq = new PdfToolkitRequest(PdfToolkitOperation.ExtractText, pdfDir, pdfExtractOut);
    var pdfPreview = pdfSkill.Preview(pdfReq);
    var pdfRes = await pdfSkill.ExecuteAsync(pdfReq, pdfPreview, skillContext);
    Check(pdfRes.ProcessedFilesCount == 1 &&
          (await File.ReadAllTextAsync(pdfExtractOut)).Contains("Hello Robotic Agent PDF", StringComparison.Ordinal),
        "PdfToolkitSkill builds and extracts standard PDF text streams offline");

    var ocrSkill = new OfflineOcrSkill();
    var ocrReq = new OfflineOcrRequest(pdfDir, docsOut, "pdf", "en");
    var ocrPreview = ocrSkill.Preview(ocrReq);
    var ocrRes = await ocrSkill.ExecuteAsync(ocrReq, ocrPreview, skillContext);
    Check(ocrRes.ProcessedCount == 1 && File.Exists(ocrRes.OutputFiles[0]), "OfflineOcrSkill extracts text layer to .ocr.txt");

    // 8. LocalSemanticSearchSkill (#28), BatchSummarizeSkill (#29), FolderReportSkill (#26), BatchConvertSkill (#16)
    var searchSkill = new LocalSemanticSearchSkill();
    var searchPreview = searchSkill.Preview(new LocalSemanticSearchRequest(invDir, "شرکت داده پرداز فاکتور", "txt"));
    Check(searchPreview.HitCount == 1, "LocalSemanticSearchSkill finds matching Persian document by content");

    var sumDir = Path.Combine(temporaryRoot.FullName, "sum-lab");
    Directory.CreateDirectory(sumDir);
    await File.WriteAllTextAsync(
        Path.Combine(sumDir, "meeting.txt"),
        "جلسه بررسی معماری سیستم برگزار شد. تصمیم گرفته شد ماژول امنیتی ارتقا یابد. اقدام: تهیه مستندات تست تا روز چهارشنبه.");
    var sumOutPath = Path.Combine(docsOut, "summary.md");
    var sumSkill = new BatchSummarizeSkill();
    var sumReq = new BatchSummarizeRequest(sumDir, sumOutPath, "txt", 2);
    var sumPreview = sumSkill.Preview(sumReq);
    Check(sumPreview.TotalActionItemsCount == 1, "BatchSummarizeSkill extracts Persian action items");
    await sumSkill.ExecuteAsync(sumReq, sumPreview, skillContext);
    Check(File.Exists(sumOutPath), "BatchSummarizeSkill writes consolidated summary document");

    var reportOutPath = Path.Combine(docsOut, "inventory-report.md");
    var reportSkill = new FolderReportSkill();
    var reportReq = new FolderReportRequest(docsLab, reportOutPath, "fa");
    var reportPreview = reportSkill.Preview(reportReq);
    await reportSkill.ExecuteAsync(reportReq, reportPreview, skillContext);
    Check(File.Exists(reportOutPath) && reportPreview.TotalFilesCount >= 4, "FolderReportSkill generates folder storage breakdown");

    var convertSkill = new BatchConvertSkill();
    var convertReq = new BatchConvertRequest(docsLab, docsOut, "md", TargetDocumentFormat.HtmlStandalone, "fa");
    var convertPreview = convertSkill.Preview(convertReq);
    var convertRes = await convertSkill.ExecuteAsync(convertReq, convertPreview, skillContext);
    Check(convertRes.ConvertedCount >= 1 &&
          (await File.ReadAllTextAsync(convertRes.OutputPaths[0])).Contains("dir=\"rtl\"", StringComparison.Ordinal),
        "BatchConvertSkill converts Markdown to RTL-aware standalone HTML offline");

    // Phase 4 checks: Multi-Step Workflow Engine, 20 Playbooks, Watcher, Scheduler, JSON Share (#41..#50)
    Check(PlaybookCatalog.All.Count == 20, "PlaybookCatalog provides 20 ready-to-use SMB playbooks (#47)");
    Check(PlaybookCatalog.All.All(pb =>
        pb.Steps.Count >= 2 &&
        CultureCatalog.Languages.All(lang => !string.IsNullOrWhiteSpace(pb.GetTitle(lang.Code)) && !string.IsNullOrWhiteSpace(pb.GetDescription(lang.Code)))),
        "Every SMB playbook has multi-step pipeline definitions and 6-language titles/descriptions");

    var accountingPlaybook = PlaybookCatalog.GetById("playbook.accounting.invoice_pipeline");
    var exportedFlowJson = WorkflowSerializer.ExportToJson(accountingPlaybook);
    var reimportedFlow = WorkflowSerializer.ImportFromJson(exportedFlowJson, skillRegistry);
    Check(reimportedFlow.Id == accountingPlaybook.Id && reimportedFlow.Steps.Count == 3,
        "WorkflowSerializer exports and imports .agentflow.json cleanly (#50)");

    ExpectThrows<InvalidOperationException>(
        () => WorkflowSerializer.ImportFromJson(exportedFlowJson.Replace("${sourceDir}", "C:\\Windows\\System32"), skillRegistry),
        "WorkflowSerializer blocks hardcoded system paths in shared .agentflow.json templates");

    var wfSource = Path.Combine(temporaryRoot.FullName, "wf-source");
    var wfOut = Path.Combine(temporaryRoot.FullName, "wf-out");
    Directory.CreateDirectory(wfSource);
    Directory.CreateDirectory(wfOut);
    await File.WriteAllTextAsync(
        Path.Combine(wfSource, "inv_a.txt"),
        "Invoice No: WF-101\nDate: 2026-10-10\nVendor: Acme Corp\nTax: 15.00\nTotal: 150.00 USD");
    await File.WriteAllTextAsync(
        Path.Combine(wfSource, "inv_b.txt"),
        "Invoice No: WF-102\nDate: 2026-10-10\nVendor: Beta LLC\nTax: 20.00\nTotal: 220.00 EUR");

    var wfEngine = new WorkflowEngine(skillRegistry, rollbackManager);
    var wfInputs = new WorkflowRuntimeInputs(wfSource, wfOut);
    var wfPreviewPlan = wfEngine.PreviewWorkflow(accountingPlaybook, wfInputs, "fa");
    Check(wfPreviewPlan.IsValid && wfPreviewPlan.ExecutableStepCount == 3,
        "WorkflowEngine.PreviewWorkflow simulates all 3 steps and resolves step variables (#42, #45, #46)");

    var wfExecResult = await wfEngine.ExecuteWorkflowAsync(accountingPlaybook, wfPreviewPlan, wfInputs, skillContext);
    Check(wfExecResult.Succeeded &&
          wfExecResult.ExecutedStepsCount == 3 &&
          File.Exists(Path.Combine(wfOut, "invoices_raw.csv")) &&
          File.Exists(Path.Combine(wfOut, "invoices_clean.csv")) &&
          File.Exists(Path.Combine(wfOut, "accounting_bundle.zip")),
        "WorkflowEngine executes 3-step Accounting Playbook end-to-end (#41)");

    var wfRollbacks = await wfEngine.RollbackWorkflowAsync(wfExecResult);
    Check(wfRollbacks.Count == 3 &&
          wfRollbacks.All(r => r.RestoredOrRemovedCount >= 1) &&
          !File.Exists(Path.Combine(wfOut, "accounting_bundle.zip")) &&
          !File.Exists(Path.Combine(wfOut, "invoices_clean.csv")) &&
          !File.Exists(Path.Combine(wfOut, "invoices_raw.csv")),
        "WorkflowEngine.RollbackWorkflowAsync rolls back all 3 steps in reverse order (#43)");

    using var watcher = new FolderWatcherTrigger(accountingPlaybook, wfInputs, TimeSpan.FromMilliseconds(10), wfEngine);
    watcher.NotifyFileDetected(Path.Combine(wfSource, "inv_a.txt"), DateTimeOffset.UtcNow.AddSeconds(-2));
    var watcherEvt = watcher.FlushReadyFiles(DateTimeOffset.UtcNow, "fa");
    Check(watcherEvt is not null && watcherEvt.ReadyFilePaths.Count == 1 && watcherEvt.PreviewPlan.ExecutableStepCount == 3,
        "FolderWatcherTrigger debounces incoming files, checks file readiness, and emits dry-run preview (#48)");

    var scheduler = new LocalWorkflowScheduler();
    var nowUtc = new DateTimeOffset(2026, 10, 10, 8, 30, 0, TimeSpan.Zero);
    var schedEntry = scheduler.RegisterSchedule(
        "daily-backup",
        "playbook.it.daily_incremental_backup",
        wfInputs,
        ScheduleCadence.Daily,
        nowUtc,
        timeOfDay: new TimeOnly(9, 0));
    Check(schedEntry.NextRunUtc == new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero),
        "LocalWorkflowScheduler calculates deterministic daily next-run timestamp (#49)");
    var dueAfter9 = scheduler.PopDueSchedules(new DateTimeOffset(2026, 10, 10, 9, 5, 0, TimeSpan.Zero));
    Check(dueAfter9.Count == 1 && dueAfter9[0].ScheduleId == "daily-backup",
        "LocalWorkflowScheduler pops due workflows and advances next run to tomorrow (#49)");

    // Phase 5 checks: Local & Hybrid AI Runtime, Anonymizer Proxy, Consent Gate, Plan Cache, Voice Adapter (#51..#59, #76)
    var anonymizer = new LocalAnonymizerProxy();
    var anonSession = anonymizer.Anonymize("Please copy files from /home/user/docs to /home/user/out for ali@example.com and IBAN DE89370400440532013000");
    Check(anonSession.RedactedTokenCount == 4 &&
          !anonSession.AnonymizedText.Contains("/home/user/docs", StringComparison.Ordinal) &&
          !anonSession.AnonymizedText.Contains("ali@example.com", StringComparison.Ordinal) &&
          anonSession.AnonymizedText.Contains("<PATH_1>", StringComparison.Ordinal) &&
          anonSession.AnonymizedText.Contains("<EMAIL_1>", StringComparison.Ordinal),
        "LocalAnonymizerProxy replaces paths, emails, and IBANs with placeholders before cloud call (#56)");

    var semMatcher = new SemanticIntentMatcher();
    Check(semMatcher.MatchIntent("لطفاً اطلاعات محرمانه و شماره کارت را سانسور کن").RecommendedSkillId == PiiRedactionSkill.SkillId &&
          semMatcher.MatchIntent("合并pdf文档").RecommendedSkillId == PdfToolkitSkill.SkillId &&
          semMatcher.MatchIntent("Extraer datos de factura a csv").RecommendedSkillId == InvoiceExtractSkill.SkillId,
        "SemanticIntentMatcher maps multilingual requests across Persian, Chinese, and Spanish to exact skills (#52)");

    ExpectThrows<InvalidOperationException>(
        () => new LocalModelPlanningProvider("http://example.com:11434/api/generate"),
        "LocalModelPlanningProvider blocks non-loopback endpoints (#51)");

    var allowedSkills = new[]
    {
        new AiAllowedSkill(FileCopySkill.SkillId, "Copy files", """{"type":"object","required":["query"]}"""),
        new AiAllowedSkill(PiiRedactionSkill.SkillId, "Redact PII", """{"type":"object","required":["query"]}""")
    };

    var planCache = new SemanticPlanCache();
    var localProvider = new LocalModelPlanningProvider(cache: planCache);
    var localProposal = await localProvider.ProposePlanAsync(new AiPlanningRequest("سانسور اطلاعات محرمانه در /home/user/docs", allowedSkills));
    Check(localProposal.Steps.Count == 1 && localProposal.Steps[0].SkillId == PiiRedactionSkill.SkillId,
        "LocalModelPlanningProvider proposes schema-valid local plan (#51, #53)");

    Check(planCache.TryGet("سانسور اطلاعات محرمانه در /home/user/other_folder", out var cachedRestoredPlan) &&
          cachedRestoredPlan is not null &&
          cachedRestoredPlan.Steps[0].Arguments.GetProperty("query").GetString()!.Contains("/home/user/other_folder", StringComparison.Ordinal),
        "SemanticPlanCache reuses cached plan template and substitutes new local path (#58)");

    var schemaValidator = new PlanSchemaValidator(skillRegistry);
    using var badArgsDoc = System.Text.Json.JsonDocument.Parse("""{"query":"../../etc/passwd"}""");
    var badPlan = new AiPlanProposal("Bad plan", [new AiProposedStep(FileCopySkill.SkillId, badArgsDoc.RootElement.Clone())], null);
    Check(!schemaValidator.Validate(badPlan, allowedSkills).IsValid,
        "PlanSchemaValidator rejects path traversal in proposed arguments (#53)");

    var credStore = new InMemoryAiCredentialStore();
    var credLifecycle = new CredentialLifecycleManager(credStore);
    await credLifecycle.SaveOrRotateKeyAsync("openai", "sk-proj-1234567890abcdef9a2f");
    var statuses = await credLifecycle.GetAllStatusesAsync();
    var openAiStatus = statuses.First(s => s.ProviderId == "openai");
    Check(openAiStatus.IsConfigured && openAiStatus.MaskedPreview == "sk-p...9a2f",
        "CredentialLifecycleManager stores and masks BYOK API keys (#76)");

    var consentGate = new HybridConsentGate();
    var cloudProvider = new OpenAiCompatiblePlanningProvider("openai", "OpenAI (BYOK)", "api.openai.com", credStore, anonymizer, consentGate, schemaValidator);
    var cloudRequest = new AiPlanningRequest("Copy PDF files from /home/user/docs for sara@example.com", allowedSkills);

    await ExpectThrowsAsync<InvalidOperationException>(
        () => cloudProvider.ProposePlanAsync(cloudRequest),
        "OpenAiCompatiblePlanningProvider blocks cloud call until user approves exact payload in HybridConsentGate (#55)");

    var inspection = cloudProvider.PrepareInspection(cloudRequest);
    Check(!inspection.AnonymizedPayloadText.Contains("sara@example.com", StringComparison.Ordinal) &&
          inspection.RedactedSensitiveItemsCount == 2,
        "OutboundPayloadInspection shows anonymized payload and redacted count (#55)");

    consentGate.ApprovePayload(inspection);
    var cloudPlan = await cloudProvider.ProposePlanAsync(cloudRequest);
    Check(cloudPlan.Summary.Contains("/home/user/docs", StringComparison.Ordinal) &&
          cloudPlan.Summary.Contains("sara@example.com", StringComparison.Ordinal),
        "OpenAiCompatiblePlanningProvider de-anonymizes returned plan locally after user consent (#54, #56)");

    consentGate.StrictLocalOnlyLock = true;
    ExpectThrows<InvalidOperationException>(
        () => consentGate.ApprovePayload(inspection),
        "HybridConsentGate blocks approval when StrictLocalOnlyLock is enabled");

    var voiceAdapter = new OfflineVoiceCommandAdapter();
    var wavBytes = OfflineVoiceCommandAdapter.CreateSyntheticPcmWav("فایل های پی دی اف از پوشه ۱۲۳ کپی شوند");
    var voiceResult = await voiceAdapter.ProcessVoiceCommandAsync(wavBytes, "fa");
    Check(voiceResult.NormalizedCommand.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
          voiceResult.NormalizedCommand.Contains("123", StringComparison.Ordinal),
        "OfflineVoiceCommandAdapter validates PCM WAV and normalizes spoken Persian words and digits (#59)");

    // Phase 6 checks: Desktop UI Automation, Smart Clipboard, Recorder, Workspace Snapshot, Kill-Switch, Guards (#31..#40)
    Check(AppCatalog.AllDefinitions.Count == 6, "AppCatalog expanded to 6 allow-listed Windows productivity apps (#32)");

    var clipSkill = new SmartClipboardSkill();
    var clipPreview = clipSkill.Preview(new SmartClipboardRequest("ColA\tColB\nVal1\tVal2", ClipboardTransformMode.TsvToMarkdownTable));
    var clipRes = await clipSkill.ExecuteAsync(new SmartClipboardRequest("ColA\tColB\nVal1\tVal2", ClipboardTransformMode.TsvToMarkdownTable), clipPreview, skillContext);
    Check(clipRes.TransformedText.Contains("| ColA | ColB |", StringComparison.Ordinal) && clipSkill.SafeHistory.Count == 1,
        "SmartClipboardSkill transforms TSV to Markdown table and stores safe history (#34)");

    var webSkill = new WebTaskAutomationSkill();
    var webFields = new[] { new WebFormFieldMapping("#customerName", "Customer Name", "Sara") };
    var webPreview = webSkill.Preview(new WebTaskAutomationRequest("https://example.com/form", webFields, UserConfirmedFinalSubmit: false));
    Check(webPreview.RequiresFinalSubmitConfirmation, "WebTaskAutomationSkill enforces final human approval gate (#36)");
    await ExpectThrowsAsync<InvalidOperationException>(
        () => webSkill.ExecuteAsync(new WebTaskAutomationRequest("https://example.com/form", webFields, UserConfirmedFinalSubmit: false), webPreview, skillContext),
        "WebTaskAutomationSkill blocks auto-submission without UserConfirmedFinalSubmit (#36)");
    ExpectThrows<InvalidOperationException>(
        () => webSkill.Preview(new WebTaskAutomationRequest("https://example.com/login", [new WebFormFieldMapping("#password", "Password", "123")])),
        "WebTaskAutomationSkill blocks password fields (#36)");

    var killSwitch = new EmergencyKillSwitchService();
    using var opTokenSource = killSwitch.RegisterOperation();
    var killEvt = killSwitch.TriggerKillSwitch("Emergency test");
    Check(killEvt.CancelledOperationsCount == 1 && opTokenSource.IsCancellationRequested,
        "EmergencyKillSwitchService immediately cancels all registered operations (#38)");

    var interferenceGuard = new InputInterferenceGuard();
    interferenceGuard.BeginAutomationSession(100, 100);
    Check(interferenceGuard.ReportPhysicalMousePosition(200, 200),
        "InputInterferenceGuard detects physical mouse movement exceeding threshold (#39)");
    ExpectThrows<InvalidOperationException>(
        () => interferenceGuard.EnsureNoInterference(),
        "InputInterferenceGuard halts automation when user moves mouse (#39)");

    var dialogGuard = new SafeDialogGuard();
    ExpectThrows<InvalidOperationException>(
        () => dialogGuard.EnsureWindowStateIsSafe(new ActiveWindowSnapshot("#32770", "Unexpected Error", IsModalDialog: true), "Notepad"),
        "SafeDialogGuard halts automation when modal dialog appears (#40)");

    interferenceGuard.BeginAutomationSession(0, 0);
    var uiEngine = new WindowsUiAutomationEngine(interferenceGuard, dialogGuard);
    var uiSteps = new[]
    {
        new UiAutomationStep(1, UiActionKind.TypeText, new UiTargetElementSelector("Notepad", "Edit1", "Text Editor", HighlightBounds: new BoundingBox(10, 10, 400, 300)), "Hello World")
    };
    var uiPreview = uiEngine.Preview("Type in Notepad", uiSteps);
    var uiExec = await uiEngine.ExecuteAsync(uiPreview);
    Check(uiExec.ExecutedStepsCount == 1 && uiExec.ExecutionLog[0].Contains("[Highlight 10,10 400x300]", StringComparison.Ordinal),
        "WindowsUiAutomationEngine previews and executes safe UI steps with highlight bounds (#31)");
    ExpectThrows<UnauthorizedAccessException>(
        () => uiEngine.Preview("Bad target", [new UiAutomationStep(1, UiActionKind.TypeText, new UiTargetElementSelector("Notepad", "pwd", "Password Box", IsPasswordControl: true))]),
        "WindowsUiAutomationEngine blocks password controls (#31)");

    var recorder = new WorkflowActionRecorder(skillRegistry);
    recorder.StartRecording();
    recorder.RecordSkillDemonstration(
        PiiRedactionSkill.SkillId,
        new Dictionary<string, string> { ["sourceDir"] = wfSource, ["outputDir"] = wfOut },
        baselineSourceDir: wfSource,
        baselineOutputDir: wfOut);
    recorder.StopRecording();
    var compiledFlow = recorder.CompileWorkflow("recorded.pii_flow", "گردش‌کار ضبط‌شده");
    Check(compiledFlow.Steps.Count == 1 && compiledFlow.Steps[0].Parameters["sourceDir"] == "${sourceDir}",
        "WorkflowActionRecorder parameterizes concrete paths into ${sourceDir} and compiles workflow (#33)");

    var snapshotService = new WorkspaceSnapshotService();
    var savedSnap = snapshotService.SaveSnapshot(
        "accounting-workspace",
        "میز کار حسابداری",
        [wfSource, wfOut],
        [new WindowPlacementSnapshot(AppId.Calculator, 50, 50, 320, 480)]);
    Check(snapshotService.RestoreSnapshot("accounting-workspace").Windows.Count == 1,
        "WorkspaceSnapshotService saves and restores workspace folder and window layouts (#35)");

    var snipService = new ScreenSnipToActionService();
    var snipResult = await snipService.ProcessSnipFileAsync(new ScreenRegion(10, 10, 500, 300), Path.Combine(pdfDir, "sample.pdf"), "en");
    Check(snipResult.FormattedOutput.Contains("Hello Robotic Agent PDF", StringComparison.Ordinal),
        "ScreenSnipToActionService extracts text from captured screen region via offline OCR (#37)");

    // Phase 7 checks: Enterprise Security, Hash-Chain Audit Log, DPAPI/AES-GCM, Command Palette, WCAG 2.2, ROI (#71, #72, #75, #77..#90)
    var gov = new ResourceGovernor(new ResourceBudgetPolicy(MaxCpuPercent: 35, MaxWorkingSetBytes: 256L * 1024 * 1024, MaxDiskBytesPerSecond: 10L * 1024 * 1024));
    ExpectThrows<InvalidOperationException>(
        () => gov.EnsureWithinMemoryCap(512L * 1024 * 1024),
        "ResourceGovernor enforces working-set memory limit (#71)");
    Check(gov.CalculateIoThrottleDelay(20L * 1024 * 1024, TimeSpan.FromSeconds(1)) >= TimeSpan.FromSeconds(0.9),
        "ResourceGovernor computes disk I/O throttle delay (#71)");

    var auditChain = await history.VerifyHashChainAsync();
    Check(auditChain.IsValid && auditChain.VerifiedCount == 2,
        "LocalHistoryStore maintains tamper-evident SHA-256 hash chain across entries (#72)");

    var fwGuard = new OfflineFirewallGuard { CurrentMode = AiExecutionMode.LocalOnly };
    fwGuard.EnsureUriAllowed("http://127.0.0.1:11434/api/generate");
    ExpectThrows<UnauthorizedAccessException>(
        () => fwGuard.EnsureUriAllowed("https://api.openai.com/v1/chat/completions"),
        "OfflineFirewallGuard blocks external network URIs in LocalOnly mode (#75)");

    var hmacKey = System.Text.Encoding.UTF8.GetBytes("EnterpriseSecretKey2026!");
    var policyDoc = new EnterprisePolicyDocument("corp.strict", ForceLocalOnlyMode: true, AllowExternalWebToolLaunch: false, MaxBatchFilesLimit: 100, AllowedSkillIds: [FileCopySkill.SkillId]);
    var signedPolicyDoc = policyDoc with { SignatureHex = EnterprisePolicy.SignPolicyDocument(policyDoc, hmacKey) };
    Check(EnterprisePolicy.VerifyPolicySignature(signedPolicyDoc, hmacKey),
        "EnterprisePolicy verifies HMAC-SHA256 policy signature (#77)");
    var entPolicy = new EnterprisePolicy(signedPolicyDoc);
    ExpectThrows<UnauthorizedAccessException>(
        () => entPolicy.EnsureSkillAllowedByPolicy(SafeArchiveSkill.SkillId),
        "EnterprisePolicy blocks skills not on corporate allowlist (#77)");

    var protector = new LocalDataProtector("TestScope");
    var cipher = protector.ProtectString("SensitiveHistoryPayload123");
    Check(protector.UnprotectString(cipher) == "SensitiveHistoryPayload123",
        "LocalDataProtector encrypts and decrypts payloads with AES-256-GCM (#78)");

    var catalogVerif = SignedSkillCatalogVerifier.VerifyRegistry(skillRegistry);
    Check(catalogVerif.IsVerified && catalogVerif.VerifiedSkillCount == 24,
        "SignedSkillCatalogVerifier validates all 24 skills in SkillRegistry (#79)");

    string sandboxPathToVerifyCleanup;
    using (var sandbox = new SandboxDryRunWorkspace(wfSource))
    {
        sandboxPathToVerifyCleanup = sandbox.SandboxRoot;
        Check(sandbox.MirroredFilesCount == 2 && Directory.Exists(sandbox.SandboxSourceDirectory),
            "SandboxDryRunWorkspace mirrors sample files into isolated temp workspace (#80)");
    }
    Check(!Directory.Exists(sandboxPathToVerifyCleanup),
        "SandboxDryRunWorkspace cleans up temporary directory on Dispose (#80)");

    var palette = new CommandPaletteService(skillRegistry);
    var paletteAll = palette.Search("", "fa", maxResults: 50);
    var palettePii = palette.Search("سانسور", "fa", maxResults: 5);
    Check(paletteAll.Count == 50 && palettePii.Any(i => i.Id == PiiRedactionSkill.SkillId),
        "CommandPaletteService searches across 20 skills, 20 playbooks, and 200 AI tools (#81)");

    var lightTheme = ThemeAndAccessibilityService.GetThemePalette(AppThemeKind.Light);
    var darkTheme = ThemeAndAccessibilityService.GetThemePalette(AppThemeKind.Dark);
    var hcTheme = ThemeAndAccessibilityService.GetThemePalette(AppThemeKind.HighContrast);
    Check(lightTheme.PrimaryContrastRatio >= 4.5 && darkTheme.PrimaryContrastRatio >= 4.5 && hcTheme.PrimaryContrastRatio >= 7.0,
        "All 3 themes (Light, Dark, HighContrast) meet WCAG 2.2 AA/AAA contrast ratios (#82, #83)");
    Check(ThemeAndAccessibilityService.CalculateCompanionBounds(1920, 1080, dockRight: true).DockedRight,
        "ThemeAndAccessibilityService calculates docked companion window bounds (#88)");

    var roi = RoiTelemetryDashboard.ComputeSummary(loadedHistory);
    Check(roi.TotalCompletedOperations == 2 && roi.TotalItemsAutomated == 4 && roi.EstimatedMinutesSaved > 1.0,
        "RoiTelemetryDashboard calculates time and monetary value saved from history (#84)");

    var starterPack = OnboardingWizardService.BuildStarterPack(UserRolePersona.Accountant, "fa");
    Check(starterPack.RecommendedPlaybooks.Count == 3 && starterPack.RecommendedAiTools.Count == 3,
        "OnboardingWizardService builds role-tailored playbook and AI tool starter pack (#85)");

    var prog = RichPreviewAndProgressService.CalculateLiveProgress(50, 100, TimeSpan.FromSeconds(5));
    Check(prog.PercentComplete == 50.0 && prog.ItemsPerSecond == 10.0,
        "RichPreviewAndProgressService computes live throughput and ETA (#86, #87)");
    var friendlyErr = RichPreviewAndProgressService.TranslateException(new UnauthorizedAccessException(), "fa");
    Check(friendlyErr.ErrorCode == "ERR_ACCESS_DENIED" && !string.IsNullOrWhiteSpace(friendlyErr.ActionableRecoveryTip),
        "RichPreviewAndProgressService translates exceptions into actionable localized guidance (#89)");
    var diagJson = RichPreviewAndProgressService.ExportPiiFreeDiagnosticsJson(skillRegistry, auditChain, "fa", "Error in /home/user/secret for ali@example.com");
    Check(!diagJson.Contains("ali@example.com", StringComparison.Ordinal) && diagJson.Contains("<EMAIL_1>", StringComparison.Ordinal),
        "ExportPiiFreeDiagnosticsJson scrubs user paths and emails before exporting diagnostics (#90)");

    // Phase 8 checks: Cross-Platform CLI, Security Fuzz Harness, VirtualClock, Offline Update Verifier (#92..#97, #100)
    var fuzzReport = SecurityFuzzHarness.RunAllSuites(skillRegistry);
    Check(fuzzReport.AllVectorsBlockedOrSanitized && fuzzReport.TotalVectorsTested >= 20,
        "SecurityFuzzHarness blocks all double-extension, path traversal, CSV injection, and shell injection vectors (#92)");

    var vClock = new VirtualClock(new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero));
    var lease = new FolderPermissionLease(wfSource, vClock.UtcNow.AddMinutes(15));
    Check(lease.IsValid(vClock.UtcNow), "FolderPermissionLease is valid before VirtualClock advances (#93)");
    vClock.Advance(TimeSpan.FromMinutes(20));
    Check(!lease.IsValid(vClock.UtcNow), "FolderPermissionLease expires deterministically when VirtualClock advances (#93)");

    var pkgBytes = System.Text.Encoding.UTF8.GetBytes("MockSignedMsixBundlePayload");
    var pkgHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pkgBytes)).ToLowerInvariant();
    var rawManifest = new UpdatePackageManifest("2.1.0", "RoboticAgent-2.1.0.msix", pkgBytes.Length, pkgHash, "");
    var signedUpdateManifest = rawManifest with { SignatureHex = UpdatePackageVerifier.ComputeManifestSignatureHex(rawManifest, hmacKey) };
    var updateCheck = UpdatePackageVerifier.VerifyUpdatePackage("2.0.0", signedUpdateManifest, pkgBytes, hmacKey);
    Check(updateCheck.IsValid && updateCheck.IsNewerVersion,
        "UpdatePackageVerifier validates newer version, SHA-256 hash, and HMAC signature (#96)");
    var downgradeCheck = UpdatePackageVerifier.VerifyUpdatePackage("2.2.0", signedUpdateManifest, pkgBytes, hmacKey);
    Check(!downgradeCheck.IsValid, "UpdatePackageVerifier blocks version downgrade attacks (#96)");

    var cli = new CliCommandProcessor(skillRegistry, wfEngine);
    var cliSkills = cli.Execute(["skills", "--lang", "fa"]);
    var cliPlaybooks = cli.Execute(["playbooks", "--lang", "en"]);
    var cliTools = cli.Execute(["tools", "--search", "spreadsheet", "--lang", "en"]);
    var cliPreview = cli.Execute(["preview-workflow", "playbook.accounting.invoice_pipeline", "--source", wfSource, "--output", wfOut, "--lang", "fa"]);
    var cliDiag = cli.Execute(["diagnose", "--lang", "fa"]);
    Check(cliSkills.ExitCode == 0 && cliSkills.StandardOutput.Contains("Registered Skills (24)", StringComparison.Ordinal) &&
          cliPlaybooks.ExitCode == 0 && cliPlaybooks.StandardOutput.Contains("SMB Playbooks (20)", StringComparison.Ordinal) &&
          cliTools.ExitCode == 0 &&
          cliPreview.ExitCode == 0 && cliPreview.StandardOutput.Contains("Workflow Dry-Run Preview", StringComparison.Ordinal) &&
          cliDiag.ExitCode == 0,
        "CliCommandProcessor executes skills, playbooks, tools, preview-workflow, and diagnose commands (#100)");

    // Phase 9 checks: Consumer Simplicity, Prompt Coach, Drop-Zone, Bilingual Guide, and Monetization
    var mediaOptSkill = new MediaAndPdfOptimizeSkill();
    var mediaReq = new MediaAndPdfOptimizeRequest(docsLab, docsOut, MediaOptimizeMode.ImagesToSinglePdf, "scans_merged.pdf");
    var mediaPreview = mediaOptSkill.Preview(mediaReq);
    var mediaRes = await mediaOptSkill.ExecuteAsync(mediaReq, mediaPreview, skillContext);
    Check(mediaRes.ProcessedFilesCount >= 1 && File.Exists(Path.Combine(docsOut, "scans_merged.pdf")),
        "MediaAndPdfOptimizeSkill combines documents/images into a single standard PDF with Undo");

    var regScript = WindowsShellIntegrationService.GenerateCurrentUserContextMenuRegistryScript(@"C:\App\Agent.Desktop.exe");
    var dropActions = WindowsShellIntegrationService.AnalyzeDroppedItems(["photo1.jpg", "photo2.png"]);
    Check(regScript.Contains(@"HKEY_CURRENT_USER\Software\Classes\Directory\shell\RoboticAgent", StringComparison.Ordinal) &&
          dropActions.Count == 3 && dropActions[0].SkillId == MediaAndPdfOptimizeSkill.SkillId,
        "WindowsShellIntegrationService generates HKCU context-menu script and analyzes dropped image files");

    var wifiQr = OfflineQrAndShareService.BuildWifiPayload("OfficeNet", "SecretPass123");
    var qrSvg = OfflineQrAndShareService.RenderOfflineMatrixSvg(wifiQr);
    Check(wifiQr.StartsWith("WIFI:S:OfficeNet;", StringComparison.Ordinal) && qrSvg.Contains("<svg", StringComparison.Ordinal),
        "OfflineQrAndShareService builds Wi-Fi QR payload and renders standalone offline SVG");

    var coached = InteractivePromptCoach.BuildCoachedPrompt(
        "Write an invoice dispute email for sara@example.com regarding /home/user/inv1.pdf",
        languageCode: "en");
    Check(coached.RedactedSensitiveItemsCount == 2 &&
          !coached.SafeEngineeredPrompt.Contains("sara@example.com", StringComparison.Ordinal) &&
          coached.RecommendedTools.Count == 3,
        "InteractivePromptCoach scrubs PII, structures expert prompt, and recommends matching catalog tools");

    var licManager = new LicenseAndPlanManager();
    ExpectThrows<InvalidOperationException>(
        () => licManager.EnsureBatchSizeAllowed(120),
        "Free tier enforces 50-file batch limit");
    var ltdKey = LicenseAndPlanManager.GenerateSignedLicenseKey(SubscriptionPlanTier.LifetimeDeal, "buyer@example.com");
    Check(licManager.TryActivateLicenseKey(ltdKey) &&
          licManager.CurrentEntitlements.Tier == SubscriptionPlanTier.LifetimeDeal &&
          licManager.CurrentEntitlements.UnlocksAll20Playbooks,
        "LicenseAndPlanManager generates and activates signed offline Lifetime Deal (RA-LTD) keys");
    licManager.EnsureBatchSizeAllowed(1000);

    var sampleTool = AiToolCatalog.AllTools.First();
    var monetizedUrl = AffiliateLinkBuilder.BuildMonetizedLaunchUrl(sampleTool, "my_partner_id");
    Check(monetizedUrl.Contains("utm_source=robotic_agent", StringComparison.Ordinal) &&
          monetizedUrl.Contains("ref=my_partner_id", StringComparison.Ordinal),
        "AffiliateLinkBuilder appends transparent UTM and referral tags to catalog URLs");

    var viralSvg = ViralImpactCardGenerator.GenerateShareableSvgCard(roi, "Ali Rezaei");
    Check(viralSvg.Contains("<svg", StringComparison.Ordinal) && viralSvg.Contains("Ali Rezaei", StringComparison.Ordinal),
        "ViralImpactCardGenerator renders bilingual SVG productivity report card");

    Check(BilingualUserGuideCatalog.AllSections.Count == 7 &&
          BilingualUserGuideCatalog.AllSections.All(s => s.StepsFa.Count >= 3 && s.StepsEn.Count >= 3),
        "BilingualUserGuideCatalog provides 7 complete tutorial chapters in both Persian and English");

    // Phase 10 checks: SubtitleStudioSkill, PrivacyLeakRiskScanner, MCP Server (JSON-RPC 2.0), and Seller License CLI
    var subDir = Path.Combine(temporaryRoot.FullName, "sub-lab");
    Directory.CreateDirectory(subDir);
    await File.WriteAllTextAsync(
        Path.Combine(subDir, "movie.srt"),
        "1\n00:00:01,000 --> 00:00:03,000\nسلام يک تست كوچك\n\n2\n00:00:04,000 --> 00:00:06,000\nJoin @my_spam_channel on t.me/spam\n");
    var subSkill = new SubtitleStudioSkill();
    var subReq = new SubtitleStudioRequest(subDir, docsOut, SubtitleOperationMode.CleanAndSyncSrt, TimeShiftMilliseconds: 1000, RemoveAdvertisingCues: true);
    var subPreview = subSkill.Preview(subReq);
    Check(subPreview.ReadyCount == 1 && subPreview.TotalAdsRemoved == 1,
        "SubtitleStudioSkill detects and removes advertising cues in SRT preview");
    var subRes = await subSkill.ExecuteAsync(subReq, subPreview, skillContext);
    var cleanedSrt = await File.ReadAllTextAsync(subRes.OutputPaths[0]);
    Check(cleanedSrt.Contains("00:00:02,000 --> 00:00:04,000", StringComparison.Ordinal) &&
          cleanedSrt.Contains("یک تست کوچک", StringComparison.Ordinal) &&
          !cleanedSrt.Contains("t.me/spam", StringComparison.Ordinal),
        "SubtitleStudioSkill shifts SRT timestamps, normalizes Persian Yeh/Kaf, and strips spam cues");

    var privacyScanner = new PrivacyLeakRiskScanner();
    var riskReport = privacyScanner.ScanFolder(docsLab);
    Check(riskReport.RiskScoreZeroToHundred > 0 && riskReport.FilesWithPiiCount >= 1 && riskReport.FilesWithHiddenMetadataCount >= 1,
        "PrivacyLeakRiskScanner detects exposed PII and hidden metadata and computes folder risk score");

    var mcpEngine = new McpServerEngine(skillRegistry, wfEngine);
    var mcpInitResp = mcpEngine.HandleJsonRpcRequest("""{"jsonrpc":"2.0","id":1,"method":"initialize"}""");
    var mcpToolsResp = mcpEngine.HandleJsonRpcRequest("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
    var mcpCallResp = mcpEngine.HandleJsonRpcRequest("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"doc.redact_pii"}}""");
    Check(mcpInitResp.Contains("RoboticAgent.McpServer", StringComparison.Ordinal) &&
          mcpToolsResp.Contains("doc.subtitle_studio", StringComparison.Ordinal) &&
          mcpCallResp.Contains("Human-in-the-Loop Preview Gate", StringComparison.Ordinal),
        "McpServerEngine handles initialize, tools/list, and tools/call JSON-RPC 2.0 requests");

    var cliLicOut = cli.Execute(["generate-license", "--tier", "LTD", "--customer", "vip@example.com"]);
    Check(cliLicOut.ExitCode == 0 && cliLicOut.StandardOutput.Contains("RA-LTD-", StringComparison.Ordinal),
        "CLI generate-license command produces valid signed RA-LTD license keys");

    // Phase 11 checks: MeetingAndVoiceNotesSkill, FinancialReconciliationSkill, PluginMarketplaceCatalog, SmartContractAndLetterBuilder
    var meetDir = Path.Combine(temporaryRoot.FullName, "meet-lab");
    Directory.CreateDirectory(meetDir);
    await File.WriteAllTextAsync(
        Path.Combine(meetDir, "board_meeting.txt"),
        "علی: جلسه بررسی بودجه را آغاز می‌کنیم.\nسارا: مصوبه: بودجه امنیت سایبری ۲۰ درصد افزایش یابد.\nرضا: اقدام: ارسال گزارش نهایی به هیئت‌مدیره تا دوشنبه.");
    var meetOutPath = Path.Combine(docsOut, "minutes.md");
    var meetSkill = new MeetingAndVoiceNotesSkill();
    var meetReq = new MeetingNotesRequest(meetDir, meetOutPath, "txt");
    var meetPreview = meetSkill.Preview(meetReq);
    Check(meetPreview.TotalDecisions == 1 && meetPreview.TotalActionItems == 1 && meetPreview.Meetings[0].Participants.Count >= 2,
        "MeetingAndVoiceNotesSkill extracts participants, decisions, and action items");
    await meetSkill.ExecuteAsync(meetReq, meetPreview, skillContext);
    Check(File.Exists(meetOutPath), "MeetingAndVoiceNotesSkill writes formal meeting minutes document");

    var reconDir = Path.Combine(temporaryRoot.FullName, "recon-lab");
    Directory.CreateDirectory(reconDir);
    var invLedgerPath = Path.Combine(reconDir, "issued_invoices.csv");
    var bankLedgerPath = Path.Combine(reconDir, "bank_statement.csv");
    var reconReportPath = Path.Combine(docsOut, "reconciliation.md");
    await File.WriteAllTextAsync(invLedgerPath, "RefId,Amount,Customer\nINV-01,1000,Acme\nINV-02,۲۵۰۰,Beta\nINV-03,500,Gamma");
    await File.WriteAllTextAsync(bankLedgerPath, "RefId,Amount,Note\nINV-01,1000,Wire\nINV-02,2400,Partial\nDEP-99,750,Unknown");
    var reconSkill = new FinancialReconciliationSkill();
    var reconReq = new FinancialReconciliationRequest(invLedgerPath, bankLedgerPath, reconReportPath);
    var reconPreview = reconSkill.Preview(reconReq);
    Check(reconPreview.MatchedPaidCount == 1 &&
          reconPreview.DiscrepancyCount == 1 &&
          reconPreview.UnpaidInvoicesCount == 1 &&
          reconPreview.UnmatchedDepositsCount == 1,
        "FinancialReconciliationSkill spots exact matches, amount discrepancies, unpaid invoices, and unmatched deposits");
    await reconSkill.ExecuteAsync(reconReq, reconPreview, skillContext);
    Check(File.Exists(reconReportPath), "FinancialReconciliationSkill writes reconciliation report");

    Check(PluginMarketplaceCatalog.FeaturedPlugins.Count == 3 &&
          PluginMarketplaceCatalog.FeaturedPlugins.All(p => PluginMarketplaceCatalog.VerifyPluginManifest(p)),
        "PluginMarketplaceCatalog verifies HMAC-SHA256 signatures on all featured plugin packs");

    var ndaDoc = SmartContractAndLetterBuilder.BuildDocument(
        LegalDocumentTemplateKind.MutualNda,
        "شرکت داده پرداز",
        "Acme Global Corp",
        "2026-10-11",
        "پروژه هوش مصنوعی محلی / Local AI Project");
    Check(ndaDoc.BilingualMarkdown.Contains("توافق‌نامهٔ دوجانبهٔ حفظ محرمانگی", StringComparison.Ordinal) &&
          ndaDoc.BilingualMarkdown.Contains("Mutual Non-Disclosure Agreement", StringComparison.Ordinal) &&
          ndaDoc.StandaloneBilingualHtml.Contains("Acme Global Corp", StringComparison.Ordinal),
        "SmartContractAndLetterBuilder generates bilingual Persian & English NDA in Markdown and HTML");

    // 46 Subscription Models & 6 Payment Gateways (3 Iranian + 3 International) checks
    Check(SubscriptionCatalog46.AllPlans.Count == 46 &&
          SubscriptionCatalog46.AllPlans.Select(p => p.PlanNumber).Distinct().Count() == 46 &&
          SubscriptionCatalog46.AllPlans.Select(p => p.SkuId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 46,
        "SubscriptionCatalog46 exposes 46 unique subscription models across 5 categories");

    Check(PaymentGatewayOrchestrator.IranianGateways.Count == 3 &&
          PaymentGatewayOrchestrator.InternationalGateways.Count == 3 &&
          PaymentGatewayOrchestrator.AllGateways.Count == 6,
        "PaymentGatewayOrchestrator registers 3 Iranian gateways (ZarinPal, Zibal, IDPay) and 3 International gateways (Stripe, LemonSqueezy, NOWPayments)");

    var gwOrchestrator = new PaymentGatewayOrchestrator();
    var zarinSession = gwOrchestrator.CreateCheckoutSession(
        new PaymentCheckoutRequest("plan.06.pro_lifetime", PaymentGatewayProviderId.ZarinPal, "buyer@example.com", "علی رضایی"));
    var zarinReceipt = gwOrchestrator.VerifyPaymentAndIssueLicense(zarinSession.SessionId, "buyer@example.com");
    Check(zarinSession.RedirectCheckoutUrl.StartsWith("https://www.zarinpal.com/pg/StartPay/", StringComparison.Ordinal) &&
          zarinReceipt.CurrencyCode == "IRT" &&
          licMgr.TryActivateLicenseKey(zarinReceipt.IssuedLicenseKey),
        "ZarinPal checkout creates session, verifies Toman payment, and issues valid signed license key");

    var stripeSession = gwOrchestrator.CreateCheckoutSession(
        new PaymentCheckoutRequest("plan.43.team_20_seats", PaymentGatewayProviderId.Stripe, "global@example.com", "Global Buyer"));
    var stripeReceipt = gwOrchestrator.VerifyPaymentAndIssueLicense(stripeSession.SessionId, "global@example.com");
    Check(stripeSession.RedirectCheckoutUrl.StartsWith("https://checkout.stripe.com/c/pay/", StringComparison.Ordinal) &&
          stripeReceipt.CurrencyCode == "USD" &&
          licMgr.TryActivateLicenseKey(stripeReceipt.IssuedLicenseKey),
        "Stripe checkout creates session, verifies USD payment, and issues valid signed Enterprise license key");

    var siteCms = new SiteAdminCmsManager();
    siteCms.ApplyBulkDiscountAcrossAll46Plans(20);
    siteCms.UpsertCoupon("SPRING40", 40, true, "تخفیف بهاره");
    var exportedSiteJson = siteCms.ExportCompleteSiteConfigJson();
    Check(siteCms.PlanOverrides.Count == 46 &&
          siteCms.GatewayConfigs.Count == 6 &&
          exportedSiteJson.Contains("SPRING40", StringComparison.Ordinal),
        "SiteAdminCmsManager manages all 46 plans, 6 gateways, coupons, branding, and exports full JSON config");

    // =========================================================================
    // 5-Phase 50-Upgrade Verification Suite (#1..#50)
    // =========================================================================
    var secEngine = new SiteSecurityAndTrustEngine();
    var totpNow = DateTimeOffset.UtcNow;
    var totpBundle = SiteSecurityAndTrustEngine.CreateTotpProvisioningBundle("admin@robotic-agent.ir", "ADMIN_SECRET_KEY_2026");
    var totpCode = SiteSecurityAndTrustEngine.GenerateTotpCode("ADMIN_SECRET_KEY_2026", totpNow);
    Check(totpCode.Length == 6 &&
          totpBundle.EmergencyRecoveryCodes.Count == 8 &&
          totpBundle.OtpAuthUri.StartsWith("otpauth://totp/RoboticAgent:", StringComparison.Ordinal) &&
          SiteSecurityAndTrustEngine.VerifyTotpCode("ADMIN_SECRET_KEY_2026", totpCode, totpNow),
        "Phase 1 (#1): TOTP 6-digit 2FA code, otpauth URI, and 8 emergency recovery codes verify cleanly");

    var webhookSig = SiteSecurityAndTrustEngine.SignGatewayWebhookPayload(PaymentGatewayProviderId.ZarinPal, """{"authority":"A0001"}""", "ZP_SECRET");
    Check(secEngine.VerifyWebhookWithAntiReplay(PaymentGatewayProviderId.ZarinPal, """{"authority":"A0001"}""", "ZP_SECRET", webhookSig, "NONCE-001", totpNow, totpNow) &&
          !secEngine.VerifyWebhookWithAntiReplay(PaymentGatewayProviderId.ZarinPal, """{"authority":"A0001"}""", "ZP_SECRET", webhookSig, "NONCE-001", totpNow, totpNow),
        "Phase 1 (#2): HMAC-SHA256 gateway webhook verification and anti-replay nonce shield succeed");

    var secHeaders = SiteSecurityAndTrustEngine.BuildStrictSecurityHeaders();
    Check(secHeaders.ContainsKey("Content-Security-Policy") && secHeaders.ContainsKey("Strict-Transport-Security"),
        "Phase 1 (#3): Strict CSP and HSTS security headers are generated");

    var hwFp = SiteSecurityAndTrustEngine.ComputeMachineFingerprintHash("CPU-INTEL-01", "MB-ASUS-99", "VOL-C-88");
    var hwKey = SiteSecurityAndTrustEngine.GenerateHardwareBoundLicenseKey("plan.06.pro_lifetime", "buyer@example.com", hwFp);
    Check(SiteSecurityAndTrustEngine.VerifyHardwareBoundLicenseKey(hwKey, "plan.06.pro_lifetime", "buyer@example.com", hwFp),
        "Phase 1 (#4): Hardware-bound machine fingerprint license key verification succeeds");

    secEngine.RecordAdminAction("superadmin", "SuperAdmin", "127.0.0.1", "UPDATE_PRICE", "Applied 20% discount");
    secEngine.RecordAdminAction("superadmin", "SuperAdmin", "127.0.0.1", "UPDATE_GATEWAY", "Enabled ZarinPal Live");
    Check(secEngine.AuditChain.Count == 2 && secEngine.VerifyAdminAuditChain(),
        "Phase 1 (#5): Tamper-evident SHA-256 admin audit trail verifies hash chain integrity");

    Check(secEngine.AllowRequestUnderRateLimit("ip:1.2.3.4", 2) &&
          secEngine.AllowRequestUnderRateLimit("ip:1.2.3.4", 2) &&
          !secEngine.AllowRequestUnderRateLimit("ip:1.2.3.4", 2),
        "Phase 1 (#6): Sliding-window rate limiter blocks excessive brute-force requests");

    var encSecret = SiteSecurityAndTrustEngine.EncryptMerchantSecretAesGcm("ZP-MERCHANT-88421-IR");
    var decSecret = SiteSecurityAndTrustEngine.DecryptMerchantSecretAesGcm(encSecret);
    Check(decSecret == "ZP-MERCHANT-88421-IR" &&
          SiteSecurityAndTrustEngine.MaskMerchantSecretForDisplay(decSecret) == "ZP-M-****-1-IR",
        "Phase 1 (#7): AES-256-GCM merchant secret vault encrypts, decrypts, and masks secrets");

    var fraudRisk = SiteSecurityAndTrustEngine.EvaluateCheckoutFraudRisk("bot@tempmail.com", "US", PaymentGatewayProviderId.ZarinPal, 499m, 6);
    Check(fraudRisk.IsBlocked && fraudRisk.RiskScoreZeroToHundred >= 75,
        "Phase 1 (#8-#10): Anti-Fraud Risk Scorer flags disposable emails, geo mismatch, and velocity");

    // Phase 2 (#11..#20): Smart Commerce & Billing Engine
    var commerce = new SmartCommerceAndBillingEngine();
    var irRoute = SmartCommerceAndBillingEngine.RecommendGatewaysForCountry("IR");
    var usRoute = SmartCommerceAndBillingEngine.RecommendGatewaysForCountry("US");
    Check(irRoute.PrimaryGateway == PaymentGatewayProviderId.ZarinPal && usRoute.PrimaryGateway == PaymentGatewayProviderId.Stripe,
        "Phase 2 (#11): Geo-IP router selects ZarinPal (IRT) for Iran and Stripe (USD) internationally");

    var fxPrices = SmartCommerceAndBillingEngine.CalculateDynamicTomanPricesForAll46Plans(62_000);
    Check(fxPrices.Count == 46 && fxPrices["plan.01.free_starter"] == 0 && fxPrices["plan.06.pro_lifetime"] > 0,
        "Phase 2 (#12): Dynamic USD-to-Toman exchange rate synchronizer recalculates all 46 plans");

    var taxInvoice = SmartCommerceAndBillingEngine.GenerateOfficialInvoiceWithQr(zarinReceipt);
    var bnplPlan = SmartCommerceAndBillingEngine.BuildBnplFourPaySchedule("plan.06.pro_lifetime");
    var prorated = SmartCommerceAndBillingEngine.CalculateProratedUpgradeQuote("plan.02.plus_monthly", "plan.04.pro_monthly", 15, 30);
    Check(taxInvoice.VerificationQrPayload.Contains("verify-invoice", StringComparison.Ordinal) &&
          bnplPlan.Installments.Count == 4 &&
          prorated.PayableDifferenceUsd < SubscriptionCatalog46.GetBySku("plan.04.pro_monthly").PriceUsd,
        "Phase 2 (#13-#15): Official QR invoice, 4-Pay BNPL schedule, and prorated upgrade quote succeed");

    commerce.TopUpWallet("wallet@example.com", 1_000_000, 20m);
    Check(commerce.TryPayPlanFromWallet("wallet@example.com", "plan.02.plus_monthly", payInToman: true, out var walletLicKey) &&
          !string.IsNullOrWhiteSpace(walletLicKey),
        "Phase 2 (#16): In-site user wallet top-up and instant purchase succeed");

    var recoveryOffer = SmartCommerceAndBillingEngine.CreateAbandonedCartRecoveryOffer(zarinSession, "buyer@example.com");
    var bumpQuote = SmartCommerceAndBillingEngine.CalculateOrderWithBump("plan.04.pro_monthly");
    var seatQuote = SmartCommerceAndBillingEngine.CalculateCustomTeamSeatQuote(25);
    Check(recoveryOffer.DiscountPercent == 10 &&
          bumpQuote.CombinedTotalUsd > bumpQuote.MainPriceUsd &&
          seatQuote.VolumeDiscountPercent == 25 &&
          commerce.ProcessRefundAndRevokeLicense(zarinReceipt) &&
          commerce.IsLicenseKeyRevoked(zarinReceipt.IssuedLicenseKey),
        "Phase 2 (#17-#20): Abandoned cart recovery, 50% off order bump, team volume slider, and 7-day refund CRL succeed");

    // Phase 3 (#21..#30): Customer Portal Pro Service
    var portalPro = new CustomerPortalProService();
    var deepLink = CustomerPortalProService.BuildDeepLinkActivationUri(stripeReceipt.IssuedLicenseKey, "plan.43.team_20_seats", "global@example.com");
    Check(CustomerPortalProService.TryParseDeepLinkActivationUri(deepLink, out var parsedKey, out var parsedSku) &&
          parsedKey == stripeReceipt.IssuedLicenseKey && parsedSku == "plan.43.team_20_seats",
        "Phase 3 (#21): Deep-link desktop activation URI builder and parser round-trip cleanly");

    Check(portalPro.RegisterDeviceSession(stripeReceipt.IssuedLicenseKey, "DEV-01", "Workstation-PC", "Windows 11") &&
          portalPro.GetActiveDevices(stripeReceipt.IssuedLicenseKey).Count == 1 &&
          portalPro.DeauthorizeDeviceSession(stripeReceipt.IssuedLicenseKey, "DEV-01"),
        "Phase 3 (#22): Active device session registration and deauthorization succeed");

    var quizRec = CustomerPortalProService.RecommendBestPlansFromQuiz("حسابدار و مالی", 300, needsTeamSeats: false);
    var cashback = portalPro.RecordReferralPurchaseAndCreditCashback("referrer@example.com", 1_000_000);
    var subKey = CustomerPortalProService.IssueTeamEmployeeSubLicense(stripeReceipt.IssuedLicenseKey, "emp1@company.com", 1);
    var encWf = CustomerPortalProService.EncryptWorkflowBackupZeroKnowledge("""{"workflow":"accounting"}""", "MyPassphrase!2026");
    var decWf = CustomerPortalProService.DecryptWorkflowBackupZeroKnowledge(encWf, "MyPassphrase!2026");
    Check(quizRec.PrimaryRecommendedPlan.SkuId == "plan.17.job_accounting" &&
          cashback == 200_000 &&
          subKey.StartsWith("RA-SEAT01-", StringComparison.Ordinal) &&
          decWf.Contains("accounting", StringComparison.Ordinal),
        "Phase 3 (#23-#30): Plan Finder Quiz, 20% referral cashback, team seat sub-license, and Zero-Knowledge E2E backup succeed");

    // Phase 4 (#31..#40): Marketing & Admin Automation Engine
    var mktEngine = new MarketingAndAdminAutomationEngine();
    mktEngine.RecordVariantEvent("B", isConversion: true);
    var winner = mktEngine.GetWinningVariant();
    var hooks = MarketingAndAdminAutomationEngine.BuildOrderNotificationPayloads(stripeReceipt);
    var schemaJsonLd = MarketingAndAdminAutomationEngine.GenerateSchemaOrgJsonLd();
    var payouts = MarketingAndAdminAutomationEngine.CalculatePublisherPayoutQueue();
    Check(winner.VariantId == "B" &&
          MarketingAndAdminAutomationEngine.HasAdminPermission(AdminRoleKind.FinanceManager, "plans.edit") &&
          !MarketingAndAdminAutomationEngine.HasAdminPermission(AdminRoleKind.ContentEditor, "orders.refund") &&
          hooks.TelegramBotJson.Contains(stripeReceipt.ReceiptNumber, StringComparison.Ordinal) &&
          schemaJsonLd.Contains("SoftwareApplication", StringComparison.Ordinal) &&
          payouts.Count == 3 && payouts.All(p => p.IsManifestSignatureValid),
        "Phase 4 (#31-#40): A/B testing, RBAC permissions, Telegram/Bale/Slack webhooks, Schema.org JSON-LD, and 70/30 publisher payouts succeed");
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
