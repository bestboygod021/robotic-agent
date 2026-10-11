using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public sealed record DocumentSummaryItem(
    string FileName,
    IReadOnlyList<string> KeySentences,
    IReadOnlyList<string> ActionItems);

public sealed record BatchSummarizeRequest(
    string SourceDirectory,
    string OutputSummaryFilePath,
    string Extension = "txt",
    int MaxSentencesPerDoc = 3);

public sealed record BatchSummarizePreview(
    string SourceDirectory,
    string OutputSummaryFilePath,
    IReadOnlyList<DocumentSummaryItem> Summaries)
{
    public int ReadyCount => Summaries.Count;
    public int TotalActionItemsCount => Summaries.Sum(s => s.ActionItems.Count);
}

public sealed record BatchSummarizeResult(
    int SummarizedFilesCount,
    int TotalActionItemsCount,
    string OutputSummaryFilePath,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline extractive document summarizer & action-item extractor (#29).
/// Scores sentences by term-frequency centrality across all 6 languages and
/// extracts action items (TODO/اقدام/مهلت/Action/Deadline/待办/اجراء/Tarea).
/// </summary>
public sealed class BatchSummarizeSkill : ISkill<BatchSummarizeRequest, BatchSummarizePreview, BatchSummarizeResult>
{
    public const string SkillId = "doc.summarize";

    private static readonly Regex SentenceSplitter = new(
        @"(?<=[\.\!\?؟。！？\n])\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WordTokenPattern = new(
        @"[\p{L}\p{N}]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ActionItemMarkers =
    [
        "todo", "action", "deadline", "must", "assigned to", "follow up",
        "اقدام", "مهلت", "مسئول", "پیگیری", "واجب", "انجام شود",
        "اجراء", "إجراء", "موعد", "مطلوب", "مسؤول",
        "tarea", "pendiente", "plazo", "responsable",
        "action", "echeance", "échéance", "responsable",
        "待办", "行动项", "截止", "负责人", "必须"
    ];

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "خلاصه‌سازی دسته‌ای اسناد و استخراج اقدام‌ها",
            ["en"] = "Batch Document Summarizer & Action Items",
            ["zh"] = "批量文档摘要与行动项提取",
            ["es"] = "Resumen Masivo de Documentos y Tareas",
            ["ar"] = "تلخيص المستندات واستخراج المهام دفعة واحدة",
            ["fr"] = "Résumé par Lots de Documents et Actions"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تولید چکیدهٔ استخراجی آفلاین برای ده‌ها سند داخل پوشه و استخراج خودکار فهرست اقدام‌ها (Action Items) همراه با واگرد.",
            ["en"] = "Produces offline extractive summaries and action-item checklists across multiple documents with preview and Undo.",
            ["zh"] = "离线为文件夹内多份文档生成核心摘要与待办行动清单，支持强制预览与撤销。",
            ["es"] = "Genera resúmenes extractivos sin conexión y listas de tareas pendientes con vista previa y deshacer.",
            ["ar"] = "ينتج ملخصات استخراجية محلياً وقوائم مهام تنفيذية لعدة مستندات مع معاينة وتراجع.",
            ["fr"] = "Génère des résumés extractifs hors-ligne et des listes d’actions pour plusieurs documents avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public BatchSummarizePreview Preview(BatchSummarizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "پوشهٔ اسناد خلاصه‌سازی");
        var outFull = Path.GetFullPath(request.OutputSummaryFilePath);
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(outFull)!, "پوشهٔ خروجی خلاصه");

        var ext = FileCopySkill.NormalizeExtension(request.Extension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var maxSentences = Math.Clamp(request.MaxSentencesPerDoc, 1, 10);
        var summaries = new List<DocumentSummaryItem>();

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _) ||
                PathGuard.PathsEqual(filePath, outFull))
            {
                continue;
            }

            var text = File.ReadAllText(filePath, Encoding.UTF8);
            summaries.Add(SummarizeDocument(Path.GetFileName(filePath), text, maxSentences));
        }

        return new BatchSummarizePreview(sourceDir, outFull, summaries);
    }

    public async Task<BatchSummarizeResult> ExecuteAsync(
        BatchSummarizeRequest request,
        BatchSummarizePreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Summaries.Count != approvedPreview.Summaries.Count)
        {
            throw new InvalidOperationException("فایل‌های پوشه از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("# Batch Executive Summary & Action Items");
        sb.AppendLine();

        foreach (var item in currentPreview.Summaries)
        {
            sb.AppendLine($"## {item.FileName}");
            sb.AppendLine("### Key Points");
            foreach (var sentence in item.KeySentences)
            {
                sb.AppendLine($"- {sentence}");
            }

            if (item.ActionItems.Count > 0)
            {
                sb.AppendLine("### Action Items");
                foreach (var action in item.ActionItems)
                {
                    sb.AppendLine($"- [ ] {action}");
                }
            }

            sb.AppendLine();
        }

        await File.WriteAllTextAsync(currentPreview.OutputSummaryFilePath, sb.ToString(), new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
        var outInfo = new FileInfo(currentPreview.OutputSummaryFilePath);
        var sha256 = await RollbackManager.ComputeFileSha256HexAsync(currentPreview.OutputSummaryFilePath, context.CancellationToken).ConfigureAwait(false);

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records:
            [
                new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: currentPreview.SourceDirectory,
                    TargetPath: currentPreview.OutputSummaryFilePath,
                    SizeBytes: outInfo.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new BatchSummarizeResult(
            currentPreview.Summaries.Count,
            currentPreview.TotalActionItemsCount,
            currentPreview.OutputSummaryFilePath,
            manifest);
    }

    public static DocumentSummaryItem SummarizeDocument(string fileName, string rawText, int maxSentences)
    {
        var sentences = SentenceSplitter.Split(rawText)
            .Select(s => s.Trim())
            .Where(s => s.Length >= 8)
            .ToArray();

        if (sentences.Length == 0 && !string.IsNullOrWhiteSpace(rawText))
        {
            sentences = [rawText.Trim()];
        }

        var wordFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WordTokenPattern.Matches(TextNormalizer.NormalizeMultilingual(rawText)))
        {
            wordFreq.TryGetValue(match.Value, out var c);
            wordFreq[match.Value] = c + 1;
        }

        var scoredSentences = sentences
            .Select((sentence, idx) =>
            {
                var norm = TextNormalizer.NormalizeMultilingual(sentence);
                var tokens = WordTokenPattern.Matches(norm).Select(m => m.Value).ToArray();
                var score = tokens.Length == 0
                    ? 0
                    : tokens.Sum(t => wordFreq.GetValueOrDefault(t, 1)) / Math.Sqrt(tokens.Length + 2.0);
                if (idx == 0)
                {
                    score *= 1.25; // Title/lead sentence bonus
                }

                return (Sentence: sentence, Index: idx, Score: score);
            })
            .OrderByDescending(item => item.Score)
            .Take(maxSentences)
            .OrderBy(item => item.Index)
            .Select(item => item.Sentence)
            .ToArray();

        var actions = sentences
            .Where(s =>
            {
                var norm = TextNormalizer.NormalizeMultilingual(s);
                return ActionItemMarkers.Any(marker => norm.Contains(TextNormalizer.NormalizeMultilingual(marker), StringComparison.OrdinalIgnoreCase));
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

        return new DocumentSummaryItem(fileName, scoredSentences, actions);
    }
}
