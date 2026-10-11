using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Skills;
using Agent.Core.Skills.Documents;
using Agent.Core.Skills.Rollback;
using System.Globalization;

namespace Agent.Core.Workflows;

/// <summary>
/// Multi-step Workflow DAG Runner (#41) with End-to-End Dry-Run Preview (#42),
/// Transactional Rollback across steps in reverse order (#43), Step Variable Passing (#45),
/// and Conditional Branching (#46).
/// </summary>
public sealed class WorkflowEngine
{
    public const int MaximumStepsPerWorkflow = 15;

    private readonly SkillRegistry _registry;
    private readonly RollbackManager _rollbackManager;

    public WorkflowEngine(SkillRegistry? registry = null, RollbackManager? rollbackManager = null)
    {
        _registry = registry ?? SkillRegistry.CreateDefault();
        _rollbackManager = rollbackManager ?? new RollbackManager();
    }

    public WorkflowPreviewPlan PreviewWorkflow(
        WorkflowDefinition workflow,
        WorkflowRuntimeInputs inputs,
        string languageCode = CultureCatalog.DefaultLanguageCode)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(inputs);

        if (workflow.Steps.Count == 0)
        {
            throw new InvalidOperationException("گردش‌کار باید حداقل یک گام داشته باشد.");
        }

        if (workflow.Steps.Count > MaximumStepsPerWorkflow)
        {
            throw new InvalidOperationException($"تعداد گام‌های گردش‌کار نمی‌تواند بیش از {MaximumStepsPerWorkflow} گام باشد.");
        }

        var varContext = new WorkflowVariableContext(inputs);
        var warnings = new List<string>();
        var stepPreviews = new List<WorkflowStepPreview>(workflow.Steps.Count);

        for (var i = 0; i < workflow.Steps.Count; i++)
        {
            var step = workflow.Steps[i];
            if (!_registry.TryGetDescriptor(step.SkillId, out var descriptor) || descriptor is null)
            {
                throw new InvalidOperationException($"مهارت '{step.SkillId}' در رجیستری مجاز نیست.");
            }

            var resolvedParams = varContext.ResolveParameters(step.Parameters);
            var (willExecute, conditionSummary) = EvaluateCondition(step.Condition, varContext);
            var (estimatedCount, plannedOutput) = EstimateStepPreview(step.SkillId, resolvedParams, varContext);

            var displayTitle = step.GetTitle(languageCode, descriptor.GetDisplayName(languageCode));
            stepPreviews.Add(new WorkflowStepPreview(
                StepNumber: i + 1,
                StepId: step.StepId,
                SkillId: step.SkillId,
                DisplayTitle: displayTitle,
                WillExecute: willExecute,
                ConditionSummary: conditionSummary,
                EstimatedItemsCount: willExecute ? estimatedCount : 0,
                PlannedOutputPath: plannedOutput,
                ResolvedParameters: resolvedParams));

            if (willExecute)
            {
                varContext.RecordStepOutcome(step.StepId, plannedOutput, estimatedCount);
            }
            else
            {
                warnings.Add($"گام {i + 1} ({displayTitle}) به علت برقرار نبودن شرط اجرا نخواهد شد.");
            }
        }

        return new WorkflowPreviewPlan(
            WorkflowId: workflow.Id,
            WorkflowTitle: workflow.GetTitle(languageCode),
            Steps: stepPreviews,
            IsValid: true,
            Warnings: warnings);
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowDefinition workflow,
        WorkflowPreviewPlan approvedPlan,
        WorkflowRuntimeInputs inputs,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(approvedPlan);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(workflow.Id, approvedPlan.WorkflowId, StringComparison.OrdinalIgnoreCase) ||
            workflow.Steps.Count != approvedPlan.Steps.Count)
        {
            throw new InvalidOperationException("طرح پیش‌نمایش گردش‌کار با تعریف فعلی گردش‌کار هم‌خوانی ندارد.");
        }

        var varContext = new WorkflowVariableContext(inputs);
        var executedRecords = new List<WorkflowStepExecutionRecord>(workflow.Steps.Count);

        try
        {
            for (var i = 0; i < workflow.Steps.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var step = workflow.Steps[i];
                var resolvedParams = varContext.ResolveParameters(step.Parameters);
                var (willExecute, _) = EvaluateCondition(step.Condition, varContext);

                if (!willExecute)
                {
                    executedRecords.Add(new WorkflowStepExecutionRecord(
                        StepNumber: i + 1,
                        StepId: step.StepId,
                        SkillId: step.SkillId,
                        Executed: false,
                        ProcessedCount: 0,
                        OutputPath: varContext.PreviousOutputPath,
                        Manifest: null));
                    continue;
                }

                var (processedCount, outputPath, manifest) = await ExecuteSingleStepAsync(
                    step.SkillId,
                    resolvedParams,
                    varContext,
                    context).ConfigureAwait(false);

                varContext.RecordStepOutcome(step.StepId, outputPath, processedCount);
                executedRecords.Add(new WorkflowStepExecutionRecord(
                    StepNumber: i + 1,
                    StepId: step.StepId,
                    SkillId: step.SkillId,
                    Executed: true,
                    ProcessedCount: processedCount,
                    OutputPath: outputPath,
                    Manifest: manifest));
            }

            return new WorkflowExecutionResult(
                WorkflowId: workflow.Id,
                Succeeded: true,
                WasRolledBack: false,
                ErrorMessage: null,
                StepRecords: executedRecords);
        }
        catch (Exception ex)
        {
            var rollbackResults = await RollbackCompletedStepsInReverseAsync(executedRecords, CancellationToken.None).ConfigureAwait(false);
            return new WorkflowExecutionResult(
                WorkflowId: workflow.Id,
                Succeeded: false,
                WasRolledBack: true,
                ErrorMessage: ex.Message,
                StepRecords: executedRecords,
                RollbackResults: rollbackResults);
        }
    }

    public async Task<IReadOnlyList<RollbackResult>> RollbackWorkflowAsync(
        WorkflowExecutionResult executionResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionResult);
        return await RollbackCompletedStepsInReverseAsync(executionResult.StepRecords, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<RollbackResult>> RollbackCompletedStepsInReverseAsync(
        IReadOnlyList<WorkflowStepExecutionRecord> stepRecords,
        CancellationToken cancellationToken)
    {
        var results = new List<RollbackResult>();
        for (var i = stepRecords.Count - 1; i >= 0; i--)
        {
            var record = stepRecords[i];
            if (record.Executed && record.Manifest is not null && record.Manifest.Records.Count > 0)
            {
                var rb = await _rollbackManager.RollbackAsync(record.Manifest, cancellationToken).ConfigureAwait(false);
                results.Add(rb);
            }
        }

        return results;
    }

    private static (bool WillExecute, string Summary) EvaluateCondition(
        WorkflowStepCondition? condition,
        WorkflowVariableContext varContext)
    {
        if (condition is null || condition.Kind == WorkflowConditionKind.Always)
        {
            return (true, "همیشه اجرا شود");
        }

        return condition.Kind switch
        {
            WorkflowConditionKind.PreviousCountAtLeast =>
                (varContext.PreviousCount >= condition.Threshold,
                 $"تعداد خروجی گام قبل ({varContext.PreviousCount}) >= {condition.Threshold}"),
            WorkflowConditionKind.SourceFileCountAtLeast =>
                (WorkflowVariableContext.CountTopLevelFilesSafe(varContext.SourceDirectory) >= condition.Threshold,
                 $"تعداد فایل‌های مبدأ >= {condition.Threshold}"),
            WorkflowConditionKind.PreviousOutputExists =>
                (File.Exists(varContext.PreviousOutputPath) || Directory.Exists(varContext.PreviousOutputPath),
                 $"وجود خروجی گام قبل ({Path.GetFileName(varContext.PreviousOutputPath)})"),
            _ => (true, "همیشه اجرا شود")
        };
    }

    private static (int EstimatedCount, string PlannedOutput) EstimateStepPreview(
        string skillId,
        IReadOnlyDictionary<string, string> p,
        WorkflowVariableContext ctx)
    {
        var sourceDir = p.GetValueOrDefault("sourceDir", ctx.SourceDirectory);
        var outputDir = p.GetValueOrDefault("outputDir", ctx.OutputDirectory);
        var ext = p.GetValueOrDefault("extension", "*");

        var sourceCount = Directory.Exists(sourceDir)
            ? Directory.EnumerateFiles(sourceDir, ext == "*" ? "*" : $"*.{ext.TrimStart('.')}", SearchOption.TopDirectoryOnly).Count()
            : Math.Max(1, ctx.PreviousCount);

        return skillId switch
        {
            InvoiceExtractSkill.SkillId => (sourceCount, p.GetValueOrDefault("outputCsvPath", Path.Combine(outputDir, "invoices.csv"))),
            CsvCleanerSkill.SkillId => (sourceCount, p.GetValueOrDefault("outputFilePath", Path.Combine(outputDir, "cleaned.csv"))),
            SafeArchiveSkill.SkillId => (sourceCount, p.GetValueOrDefault("archiveFilePath", Path.Combine(outputDir, "archive.zip"))),
            FolderReportSkill.SkillId => (sourceCount, p.GetValueOrDefault("outputReportPath", Path.Combine(outputDir, "folder-report.md"))),
            BatchSummarizeSkill.SkillId => (sourceCount, p.GetValueOrDefault("outputSummaryPath", Path.Combine(outputDir, "summary.md"))),
            DocumentDiffSkill.SkillId => (1, p.GetValueOrDefault("outputReportPath", Path.Combine(outputDir, "diff.md"))),
            _ => (sourceCount, outputDir)
        };
    }

    private static async Task<(int ProcessedCount, string OutputPath, OperationManifest Manifest)> ExecuteSingleStepAsync(
        string skillId,
        IReadOnlyDictionary<string, string> p,
        WorkflowVariableContext ctx,
        ISkillContext skillContext)
    {
        var sourceDir = p.GetValueOrDefault("sourceDir", ctx.SourceDirectory);
        var outputDir = p.GetValueOrDefault("outputDir", ctx.OutputDirectory);
        var ext = p.GetValueOrDefault("extension", "*");
        Directory.CreateDirectory(outputDir);

        switch (skillId)
        {
            case FileCopySkill.SkillId:
            {
                var skill = new FileCopySkill();
                var req = new FileCopyRequest(sourceDir, outputDir, ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.CopiedCount, outputDir, res.RollbackManifest ?? EmptyManifest(skillId));
            }
            case BatchRenameSkill.SkillId:
            {
                var skill = new BatchRenameSkill();
                var req = new BatchRenameRequest(
                    sourceDir,
                    Prefix: p.GetValueOrDefault("prefix", "doc_"),
                    Suffix: p.GetValueOrDefault("suffix", ""),
                    ExtensionFilter: ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.RenamedCount, sourceDir, res.RollbackManifest);
            }
            case FolderOrganizeSkill.SkillId:
            {
                var skill = new FolderOrganizeSkill();
                var req = new FolderOrganizeRequest(sourceDir);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.MovedCount, sourceDir, res.RollbackManifest);
            }
            case DuplicateFinderSkill.SkillId:
            {
                var skill = new DuplicateFinderSkill();
                var req = new DuplicateFinderRequest(sourceDir, MoveDuplicatesToStaging: true);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.DuplicateFilesCount, res.StagingFolderPath, res.RollbackManifest);
            }
            case SafeArchiveSkill.SkillId:
            {
                var archivePath = p.GetValueOrDefault("archiveFilePath", Path.Combine(outputDir, "workflow-bundle.zip"));
                var skill = new SafeArchiveSkill();
                var req = new SafeArchiveRequest( ArchiveMode.CreateZip, sourceDir, archivePath);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.ProcessedFileCount, res.TargetPath, res.RollbackManifest);
            }
            case IncrementalBackupSkill.SkillId:
            {
                var skill = new IncrementalBackupSkill();
                var req = new IncrementalBackupRequest(sourceDir, outputDir);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.CopiedOrUpdatedCount, outputDir, res.RollbackManifest);
            }
            case PiiRedactionSkill.SkillId:
            {
                var skill = new PiiRedactionSkill();
                var req = new PiiRedactionRequest(sourceDir, outputDir, ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.SanitizedFileCount, outputDir, res.RollbackManifest);
            }
            case MetadataScrubberSkill.SkillId:
            {
                var skill = new MetadataScrubberSkill();
                var req = new MetadataScrubberRequest(sourceDir, outputDir, ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.ScrubbedFileCount, outputDir, res.RollbackManifest);
            }
            case OfflineOcrSkill.SkillId:
            {
                var skill = new OfflineOcrSkill();
                var req = new OfflineOcrRequest(sourceDir, outputDir, ext == "*" ? "pdf" : ext, p.GetValueOrDefault("lang", "fa"));
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.ProcessedCount, outputDir, res.RollbackManifest);
            }
            case InvoiceExtractSkill.SkillId:
            {
                var csvOut = p.GetValueOrDefault("outputCsvPath", Path.Combine(outputDir, "invoices.csv"));
                var skill = new InvoiceExtractSkill();
                var req = new InvoiceExtractRequest(sourceDir, csvOut, ext == "*" ? "txt" : ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.ExtractedCount, res.OutputCsvFilePath, res.RollbackManifest);
            }
            case CsvCleanerSkill.SkillId:
            {
                var inCsv = p.GetValueOrDefault("sourceFilePath", ctx.PreviousOutputPath);
                var outCsv = p.GetValueOrDefault("outputFilePath", Path.Combine(outputDir, "invoices_clean.csv"));
                var skill = new CsvCleanerSkill();
                var req = new CsvCleanerRequest(inCsv, outCsv);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.CleanOutputRows, res.OutputFilePath, res.RollbackManifest);
            }
            case PdfToolkitSkill.SkillId:
            {
                var outPdf = p.GetValueOrDefault("outputFilePath", Path.Combine(outputDir, "merged.pdf"));
                var skill = new PdfToolkitSkill();
                var req = new PdfToolkitRequest(PdfToolkitOperation.MergeIntoSinglePdf, sourceDir, outPdf);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.ProcessedFilesCount, res.OutputFilePath, res.RollbackManifest);
            }
            case FolderReportSkill.SkillId:
            {
                var reportPath = p.GetValueOrDefault("outputReportPath", Path.Combine(outputDir, "folder-report.md"));
                var skill = new FolderReportSkill();
                var req = new FolderReportRequest(sourceDir, reportPath, p.GetValueOrDefault("lang", "fa"));
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.TotalFilesCount, res.OutputReportFilePath, res.RollbackManifest);
            }
            case BatchSummarizeSkill.SkillId:
            {
                var sumPath = p.GetValueOrDefault("outputSummaryPath", Path.Combine(outputDir, "summary.md"));
                var skill = new BatchSummarizeSkill();
                var req = new BatchSummarizeRequest(sourceDir, sumPath, ext == "*" ? "txt" : ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.SummarizedFilesCount, res.OutputSummaryFilePath, res.RollbackManifest);
            }
            case BatchConvertSkill.SkillId:
            {
                var skill = new BatchConvertSkill();
                var req = new BatchConvertRequest(sourceDir, outputDir, ext == "*" ? "md" : ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.ConvertedCount, outputDir, res.RollbackManifest);
            }
            case LocalSemanticSearchSkill.SkillId:
            {
                var skill = new LocalSemanticSearchSkill();
                var req = new LocalSemanticSearchRequest(sourceDir, p.GetValueOrDefault("query", "فاکتور"), ext);
                var preview = skill.Preview(req);
                var res = await skill.ExecuteAsync(req, preview, skillContext).ConfigureAwait(false);
                return (res.HitCount, sourceDir, res.RollbackManifest);
            }
            default:
                throw new InvalidOperationException($"اجرای خودکار برای مهارت '{skillId}' پیکربندی نشده است.");
        }
    }

    private static OperationManifest EmptyManifest(string skillId) =>
        new(Guid.NewGuid().ToString("N"), skillId, DateTimeOffset.UtcNow, []);
}
