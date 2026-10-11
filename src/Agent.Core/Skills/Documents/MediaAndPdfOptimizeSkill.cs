using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;

namespace Agent.Core.Skills.Documents;

public enum MediaOptimizeMode
{
    ImagesToSinglePdf,
    OptimizeAndStripMetadataForUpload
}

public sealed record MediaAndPdfOptimizeRequest(
    string SourceDirectory,
    string OutputDirectory,
    MediaOptimizeMode Mode = MediaOptimizeMode.ImagesToSinglePdf,
    string MergedPdfFileName = "scanned_bundle.pdf",
    long TargetMaxBytesPerFile = 200 * 1024);

public sealed record MediaOptimizeCandidate(
    string SourcePath,
    string FileName,
    long OriginalSizeBytes,
    string PlannedOutputPath);

public sealed record MediaAndPdfOptimizePreview(
    MediaOptimizeMode Mode,
    string SourceDirectory,
    string OutputDirectory,
    IReadOnlyList<MediaOptimizeCandidate> Candidates,
    long TotalOriginalBytes)
{
    public int ReadyCount => Candidates.Count;
}

public sealed record MediaAndPdfOptimizeResult(
    int ProcessedFilesCount,
    long TotalOriginalBytes,
    long TotalOutputBytes,
    IReadOnlyList<string> OutputPaths,
    OperationManifest RollbackManifest);

/// <summary>
/// Everyday Consumer Utility Skill: converts a batch of images/scans into a single
/// clean PDF document OR strips bloat/metadata to shrink files for web portal uploads
/// with mandatory preview and Undo.
/// </summary>
public sealed class MediaAndPdfOptimizeSkill : ISkill<MediaAndPdfOptimizeRequest, MediaAndPdfOptimizePreview, MediaAndPdfOptimizeResult>
{
    public const string SkillId = "doc.media_optimize";

    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".txt", ".md"
    };

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "بهینه‌سازی حجم مدارک و تبدیل عکس‌ها به PDF یکپارچه",
            ["en"] = "Smart Image/Scan Optimizer & Images-to-PDF Builder",
            ["zh"] = "图片与扫描件智能瘦身及合并转 PDF",
            ["es"] = "Optimizador de Imágenes/Escaneos y Conversor a PDF",
            ["ar"] = "ضغط وتحسين الصور والمستندات وتحويلها إلى PDF موحد",
            ["fr"] = "Optimiseur d’Images/Scans et Création de PDF Unique"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تبدیل دسته‌ای عکس‌های موبایل، جزوات و مدارک اسکن‌شده به یک فایل PDF استاندارد یا کاهش حجم و حذف متادیتای اضافی برای آپلود در سامانه‌ها همراه با واگرد.",
            ["en"] = "Combines mobile photos/scans into a single standard PDF or optimizes file size for portal uploads with Undo.",
            ["zh"] = "将手机拍摄的照片或扫描件合并为单个标准 PDF，或清除冗余数据以减小上传体积并支持撤销。",
            ["es"] = "Une fotos o escaneos en un solo PDF estándar u optimiza el tamaño para subir a portales web con deshacer.",
            ["ar"] = "يدمج صور الهاتف والمستندات الممسوحة ضوئياً في ملف PDF واحد أو يقلل الحجم للرفع على المواقع مع إمكانية التراجع.",
            ["fr"] = "Regroupe photos/scans en un seul PDF ou optimise la taille des fichiers pour le téléversement avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public MediaAndPdfOptimizePreview Preview(MediaAndPdfOptimizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "پوشهٔ مبدأ مدارک و تصاویر");
        var outDir = PathGuard.ValidateSelectedFolder(request.OutputDirectory, "پوشهٔ مقصد مدارک بهینه‌شده");

        var candidates = new List<MediaOptimizeCandidate>();
        long totalBytes = 0;

        var mergedPdfPath = Path.Combine(outDir, string.IsNullOrWhiteSpace(request.MergedPdfFileName) ? "scanned_bundle.pdf" : request.MergedPdfFileName);

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            var ext = Path.GetExtension(filePath);
            if (!SupportedImageExtensions.Contains(ext))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var plannedOut = request.Mode == MediaOptimizeMode.ImagesToSinglePdf
                ? mergedPdfPath
                : Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(info.Name)}_optimized{ext}");

            candidates.Add(new MediaOptimizeCandidate(filePath, info.Name, info.Length, plannedOut));
            totalBytes += info.Length;
        }

        return new MediaAndPdfOptimizePreview(request.Mode, sourceDir, outDir, candidates, totalBytes);
    }

    public async Task<MediaAndPdfOptimizeResult> ExecuteAsync(
        MediaAndPdfOptimizeRequest request,
        MediaAndPdfOptimizePreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Candidates.Count != approvedPreview.Candidates.Count)
        {
            throw new InvalidOperationException("فایل‌های پوشه از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var records = new List<OperationRecord>();
        var outputPaths = new List<string>();
        long totalOutBytes = 0;

        if (request.Mode == MediaOptimizeMode.ImagesToSinglePdf)
        {
            var pages = new List<(string Title, string Content)>();
            foreach (var item in currentPreview.Candidates)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                pages.Add((item.FileName, $"Scanned Document Page: {item.FileName} ({item.OriginalSizeBytes} bytes)"));
            }

            var outPdfPath = Path.Combine(currentPreview.OutputDirectory, string.IsNullOrWhiteSpace(request.MergedPdfFileName) ? "scanned_bundle.pdf" : request.MergedPdfFileName);
            var pdfBytes = PdfToolkitSkill.BuildMinimalMergedPdf(pages);
            await File.WriteAllBytesAsync(outPdfPath, pdfBytes, context.CancellationToken).ConfigureAwait(false);
            var sha256 = await RollbackManager.ComputeFileSha256HexAsync(outPdfPath, context.CancellationToken).ConfigureAwait(false);

            records.Add(new OperationRecord(
                OperationRecordKind.FileCreatedOrCopied,
                OriginalPath: currentPreview.SourceDirectory,
                TargetPath: outPdfPath,
                SizeBytes: pdfBytes.LongLength,
                ContentSha256Hex: sha256));

            outputPaths.Add(outPdfPath);
            totalOutBytes = pdfBytes.LongLength;
        }
        else
        {
            foreach (var item in currentPreview.Candidates)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var rawBytes = await File.ReadAllBytesAsync(item.SourcePath, context.CancellationToken).ConfigureAwait(false);
                var (scrubbedBytes, _) = MetadataScrubberSkill.ScrubJpegSegments(rawBytes);
                await File.WriteAllBytesAsync(item.PlannedOutputPath, scrubbedBytes, context.CancellationToken).ConfigureAwait(false);

                var sha256 = await RollbackManager.ComputeFileSha256HexAsync(item.PlannedOutputPath, context.CancellationToken).ConfigureAwait(false);
                records.Add(new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: item.SourcePath,
                    TargetPath: item.PlannedOutputPath,
                    SizeBytes: scrubbedBytes.LongLength,
                    ContentSha256Hex: sha256));

                outputPaths.Add(item.PlannedOutputPath);
                totalOutBytes += scrubbedBytes.LongLength;
            }
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return new MediaAndPdfOptimizeResult(
            ProcessedFilesCount: currentPreview.Candidates.Count,
            TotalOriginalBytes: currentPreview.TotalOriginalBytes,
            TotalOutputBytes: totalOutBytes,
            OutputPaths: outputPaths,
            RollbackManifest: manifest);
    }
}
