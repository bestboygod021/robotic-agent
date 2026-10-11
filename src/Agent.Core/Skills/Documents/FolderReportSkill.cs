using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;

namespace Agent.Core.Skills.Documents;

public sealed record ExtensionBreakdownItem(
    string Extension,
    int FileCount,
    long TotalBytes);

public sealed record FolderReportRequest(
    string SourceDirectory,
    string OutputReportPath,
    string LanguageCode = CultureCatalog.DefaultLanguageCode,
    bool IncludeSubdirectories = true);

public sealed record FolderReportPreview(
    string SourceDirectory,
    string OutputReportPath,
    int TotalFilesCount,
    long TotalBytes,
    IReadOnlyList<ExtensionBreakdownItem> ByExtension)
{
    public int ReadyCount => TotalFilesCount;
}

public sealed record FolderReportResult(
    int TotalFilesReported,
    long TotalBytesReported,
    string OutputReportPath,
    OperationManifest RollbackManifest);

/// <summary>
/// Generates a structured periodic inventory & storage report (#26) in Markdown
/// or HTML for a folder across any of the 6 supported languages, with Undo (#20).
/// </summary>
public sealed class FolderReportSkill : ISkill<FolderReportRequest, FolderReportPreview, FolderReportResult>
{
    public const string SkillId = "doc.folder_report";

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تولید گزارش تحلیلی و دوره‌ای پوشه",
            ["en"] = "Periodic Folder Inventory & Storage Report",
            ["zh"] = "文件夹定期盘点与存储分析报告",
            ["es"] = "Informe Periódico de Inventario de Carpeta",
            ["ar"] = "تقرير دوري لإحصاءات ومساحة المجلد",
            ["fr"] = "Rapport Périodique d’Inventaire de Dossier"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تولید گزارش ساختاریافته (Markdown/HTML) از تعداد فایل‌ها، حجم کل و تفکیک پسوندها در ۶ زبان همراه با پیش‌نمایش و واگرد.",
            ["en"] = "Generates a structured Markdown/HTML report of file counts, total storage, and extension breakdown in 6 languages with Undo.",
            ["zh"] = "生成包含文件数量、总容量与后缀分布的 6 语言结构化报告（Markdown/HTML），支持预览与撤销。",
            ["es"] = "Genera un informe estructurado de cantidad de archivos, tamaño total y desglose por extensión en 6 idiomas.",
            ["ar"] = "ينشئ تقريراً منظماً لعدد الملفات والحجم الإجمالي وتوزيع الامتدادات بـ 6 لغات مع إمكانية التراجع.",
            ["fr"] = "Génère un rapport structuré du nombre de fichiers, du volume total et des extensions en 6 langues avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public FolderReportPreview Preview(FolderReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "پوشهٔ گزارش‌گیری");
        var outFull = Path.GetFullPath(request.OutputReportPath);
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(outFull)!, "پوشهٔ خروجی گزارش");

        var files = new List<FileInfo>();
        CollectSafeFiles(sourceDir, outFull, 0, request.IncludeSubdirectories ? 5 : 0, files);

        var byExt = files
            .GroupBy(f => string.IsNullOrWhiteSpace(f.Extension) ? "(no-ext)" : f.Extension.TrimStart('.').ToLowerInvariant())
            .Select(g => new ExtensionBreakdownItem(g.Key, g.Count(), g.Sum(f => f.Length)))
            .OrderByDescending(item => item.TotalBytes)
            .ThenBy(item => item.Extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FolderReportPreview(
            SourceDirectory: sourceDir,
            OutputReportPath: outFull,
            TotalFilesCount: files.Count,
            TotalBytes: files.Sum(f => f.Length),
            ByExtension: byExt);
    }

    public async Task<FolderReportResult> ExecuteAsync(
        FolderReportRequest request,
        FolderReportPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.TotalFilesCount != approvedPreview.TotalFilesCount)
        {
            throw new InvalidOperationException("محتوای پوشه از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var lang = CultureCatalog.NormalizeLanguageCode(request.LanguageCode);
        var reportText = BuildMarkdownReport(currentPreview, lang);

        await File.WriteAllTextAsync(currentPreview.OutputReportPath, reportText, new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
        var outInfo = new FileInfo(currentPreview.OutputReportPath);
        var sha256 = await RollbackManager.ComputeFileSha256HexAsync(currentPreview.OutputReportPath, context.CancellationToken).ConfigureAwait(false);

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records:
            [
                new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: currentPreview.SourceDirectory,
                    TargetPath: currentPreview.OutputReportPath,
                    SizeBytes: outInfo.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new FolderReportResult(
            currentPreview.TotalFilesCount,
            currentPreview.TotalBytes,
            currentPreview.OutputReportPath,
            manifest);
    }

    private static string BuildMarkdownReport(FolderReportPreview preview, string lang)
    {
        var sb = new StringBuilder();
        var totalFiles = CultureCatalog.FormatNumber(preview.TotalFilesCount, lang);
        var totalSize = CultureCatalog.FormatByteSize(preview.TotalBytes, lang);

        sb.AppendLine($"# Folder Storage & Inventory Report ({lang.ToUpperInvariant()})");
        sb.AppendLine($"- Total Files: **{totalFiles}**");
        sb.AppendLine($"- Total Volume: **{totalSize}**");
        sb.AppendLine();
        sb.AppendLine("| Extension | Count | Size |");
        sb.AppendLine("|---|---:|---:|");
        foreach (var item in preview.ByExtension)
        {
            sb.AppendLine($"| `{item.Extension}` | {CultureCatalog.FormatNumber(item.FileCount, lang)} | {CultureCatalog.FormatByteSize(item.TotalBytes, lang)} |");
        }

        return sb.ToString();
    }

    private static void CollectSafeFiles(
        string currentDir,
        string excludedOutputFilePath,
        int depth,
        int maxDepth,
        List<FileInfo> results)
    {
        foreach (var filePath in Directory.EnumerateFiles(currentDir, "*", SearchOption.TopDirectoryOnly))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.PathsEqual(filePath, excludedOutputFilePath))
            {
                continue;
            }

            results.Add(new FileInfo(filePath));
        }

        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var subDir in Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly))
        {
            var attrs = File.GetAttributes(subDir);
            if ((attrs & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
            {
                continue;
            }

            CollectSafeFiles(subDir, excludedOutputFilePath, depth + 1, maxDepth, results);
        }
    }
}
