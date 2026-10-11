using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;

namespace Agent.Core.Skills.Documents;

public enum DiffLineKind
{
    Unchanged,
    Added,
    Removed
}

public sealed record DiffLineEntry(
    DiffLineKind Kind,
    int? OldLineNumber,
    int? NewLineNumber,
    string Text);

public sealed record DocumentDiffRequest(
    string OriginalFilePath,
    string RevisedFilePath,
    string OutputReportPath);

public sealed record DocumentDiffPreview(
    string OriginalFilePath,
    string RevisedFilePath,
    string OutputReportPath,
    int AddedCount,
    int RemovedCount,
    int UnchangedCount,
    IReadOnlyList<DiffLineEntry> Entries)
{
    public bool HasDifferences => AddedCount > 0 || RemovedCount > 0;
}

public sealed record DocumentDiffResult(
    int AddedCount,
    int RemovedCount,
    int UnchangedCount,
    string OutputReportPath,
    OperationManifest RollbackManifest);

/// <summary>
/// Compares two document versions line-by-line using Longest Common Subsequence
/// (LCS) (#25) and writes a structured Markdown diff report with Undo (#20).
/// </summary>
public sealed class DocumentDiffSkill : ISkill<DocumentDiffRequest, DocumentDiffPreview, DocumentDiffResult>
{
    public const string SkillId = "doc.diff";

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "مقایسهٔ هوشمند دو نسخه از سند (Diff)",
            ["en"] = "Smart Document Version Diff",
            ["zh"] = "文档双版本智能对比（Diff）",
            ["es"] = "Comparación Inteligente de Versiones de Documento",
            ["ar"] = "مقارنة ذكية بين نسختين من المستند",
            ["fr"] = "Comparaison Intelligente de Versions de Document"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "مقایسهٔ دقیق خط‌به‌خط دو نسخه از قرارداد، مقاله یا کد و تولید گزارش خطوط اضافه‌شده و حذف‌شده همراه با قابلیت واگرد.",
            ["en"] = "Compares two document versions line-by-line (LCS) and generates a structured diff report with preview and Undo.",
            ["zh"] = "逐行对比两份合同、文档或代码版本（LCS），生成增删差异报告并支持撤销。",
            ["es"] = "Compara dos versiones de un documento línea por línea y genera un informe de diferencias con opción de deshacer.",
            ["ar"] = "يقارن بين نسختين من مستند سطراً بسطر وينتج تقريراً بالأسطر المضافة والمحذوفة مع إمكانية التراجع.",
            ["fr"] = "Compare deux versions d’un document ligne par ligne et génère un rapport de différences avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public DocumentDiffPreview Preview(DocumentDiffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.OriginalFilePath))
        {
            throw new FileNotFoundException("فایل نسخهٔ اول پیدا نشد.", request.OriginalFilePath);
        }
        if (!File.Exists(request.RevisedFilePath))
        {
            throw new FileNotFoundException("فایل نسخهٔ دوم پیدا نشد.", request.RevisedFilePath);
        }

        var origFull = Path.GetFullPath(request.OriginalFilePath);
        var revFull = Path.GetFullPath(request.RevisedFilePath);
        var outFull = Path.GetFullPath(request.OutputReportPath);

        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(origFull)!, "پوشهٔ سند اول");
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(revFull)!, "پوشهٔ سند دوم");
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(outFull)!, "پوشهٔ گزارش مقایسه");

        var oldLines = File.ReadAllLines(origFull, Encoding.UTF8);
        var newLines = File.ReadAllLines(revFull, Encoding.UTF8);
        var entries = ComputeLcsDiff(oldLines, newLines);

        return new DocumentDiffPreview(
            OriginalFilePath: origFull,
            RevisedFilePath: revFull,
            OutputReportPath: outFull,
            AddedCount: entries.Count(e => e.Kind == DiffLineKind.Added),
            RemovedCount: entries.Count(e => e.Kind == DiffLineKind.Removed),
            UnchangedCount: entries.Count(e => e.Kind == DiffLineKind.Unchanged),
            Entries: entries);
    }

    public async Task<DocumentDiffResult> ExecuteAsync(
        DocumentDiffRequest request,
        DocumentDiffPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.AddedCount != approvedPreview.AddedCount ||
            currentPreview.RemovedCount != approvedPreview.RemovedCount)
        {
            throw new InvalidOperationException("محتوای اسناد از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Document Comparison Report");
        sb.AppendLine($"- Original: `{Path.GetFileName(currentPreview.OriginalFilePath)}`");
        sb.AppendLine($"- Revised: `{Path.GetFileName(currentPreview.RevisedFilePath)}`");
        sb.AppendLine($"- Summary: +{currentPreview.AddedCount} added, -{currentPreview.RemovedCount} removed, {currentPreview.UnchangedCount} unchanged");
        sb.AppendLine();
        sb.AppendLine("```diff");
        foreach (var entry in currentPreview.Entries)
        {
            var prefix = entry.Kind switch
            {
                DiffLineKind.Added => "+ ",
                DiffLineKind.Removed => "- ",
                _ => "  "
            };
            sb.AppendLine($"{prefix}{entry.Text}");
        }
        sb.AppendLine("```");

        await File.WriteAllTextAsync(currentPreview.OutputReportPath, sb.ToString(), new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
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
                    OriginalPath: currentPreview.OriginalFilePath,
                    TargetPath: currentPreview.OutputReportPath,
                    SizeBytes: outInfo.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new DocumentDiffResult(
            currentPreview.AddedCount,
            currentPreview.RemovedCount,
            currentPreview.UnchangedCount,
            currentPreview.OutputReportPath,
            manifest);
    }

    public static IReadOnlyList<DiffLineEntry> ComputeLcsDiff(string[] oldLines, string[] newLines)
    {
        var m = oldLines.Length;
        var n = newLines.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = m - 1; i >= 0; i--)
        {
            for (var j = n - 1; j >= 0; j--)
            {
                if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                {
                    dp[i, j] = 1 + dp[i + 1, j + 1];
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }
        }

        var result = new List<DiffLineEntry>();
        var x = 0;
        var y = 0;
        while (x < m && y < n)
        {
            if (string.Equals(oldLines[x], newLines[y], StringComparison.Ordinal))
            {
                result.Add(new DiffLineEntry(DiffLineKind.Unchanged, x + 1, y + 1, oldLines[x]));
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
            {
                result.Add(new DiffLineEntry(DiffLineKind.Removed, x + 1, null, oldLines[x]));
                x++;
            }
            else
            {
                result.Add(new DiffLineEntry(DiffLineKind.Added, null, y + 1, newLines[y]));
                y++;
            }
        }

        while (x < m)
        {
            result.Add(new DiffLineEntry(DiffLineKind.Removed, x + 1, null, oldLines[x]));
            x++;
        }

        while (y < n)
        {
            result.Add(new DiffLineEntry(DiffLineKind.Added, null, y + 1, newLines[y]));
            y++;
        }

        return result;
    }
}
