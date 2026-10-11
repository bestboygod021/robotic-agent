using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Skills.Documents;
using Agent.Core.Skills.Rollback;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills;

public enum ClipboardTransformMode
{
    CleanAndNormalizeText,
    TsvToMarkdownTable,
    TsvToCsv,
    RedactSensitivePii,
    ExtractLinksAndEmails
}

public sealed record SmartClipboardRequest(
    string RawClipboardText,
    ClipboardTransformMode Mode = ClipboardTransformMode.CleanAndNormalizeText);

public sealed record SmartClipboardPreview(
    ClipboardTransformMode Mode,
    string OriginalSample,
    string TransformedText,
    int RedactedPiiCount);

public sealed record SmartClipboardResult(
    string TransformedText,
    int RedactedPiiCount,
    OperationManifest RollbackManifest);

/// <summary>
/// Smart Clipboard &amp; Safe History Skill (#34 — clipboard.smart): transforms
/// clipboard text (plain-text normalization, TSV to Markdown/CSV table, PII masking,
/// link/email extraction) and maintains a bounded privacy-aware clipboard history.
/// </summary>
public sealed class SmartClipboardSkill : ISkill<SmartClipboardRequest, SmartClipboardPreview, SmartClipboardResult>
{
    public const string SkillId = "clipboard.smart";
    public const int MaxHistoryItems = 25;

    private static readonly Regex UrlOrEmailPattern = new(
        @"(?:https?://[^\s""'<>]+|\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "کلیپ‌بورد هوشمند و تبدیل‌گر فوری متن/جدول",
            ["en"] = "Smart Clipboard & Instant Table/Text Transformer",
            ["zh"] = "智能剪贴板与即时表格/文本转换器",
            ["es"] = "Portapapeles Inteligente y Transformador de Texto/Tablas",
            ["ar"] = "الحافظة الذكية ومحول النصوص والجداول الفوري",
            ["fr"] = "Presse-Papiers Intelligent et Convertisseur Texte/Tableau"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "پاک‌سازی فرمت متن کلیپ‌بورد، تبدیل داده‌های کپی‌شده از اکسل به جدول Markdown/CSV و سانسور فوری اطلاعات حساس.",
            ["en"] = "Cleans clipboard text, converts copied spreadsheet cells to Markdown/CSV tables, and redacts sensitive PII.",
            ["zh"] = "清理剪贴板格式、将复制的表格转换为 Markdown/CSV 并即时脱敏敏感信息。",
            ["es"] = "Limpia el portapapeles, convierte celdas copiadas en tablas Markdown/CSV y oculta PII sensible.",
            ["ar"] = "ينظف نصوص الحافظة ويحول خلايا الجداول المنسوخة إلى Markdown/CSV ويحجب البيانات الحساسة.",
            ["fr"] = "Nettoie le presse-papiers, convertit les cellules copiées en tableaux Markdown/CSV et masque les PII."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    private readonly List<string> _safeHistory = [];

    public SkillDescriptor Descriptor => DefaultDescriptor;
    public IReadOnlyList<string> SafeHistory => _safeHistory;

    public SmartClipboardPreview Preview(SmartClipboardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raw = request.RawClipboardText ?? string.Empty;
        var (transformed, piiCount) = Transform(raw, request.Mode);
        var sample = raw.Length <= 120 ? raw : raw[..120] + "...";
        return new SmartClipboardPreview(request.Mode, sample, transformed, piiCount);
    }

    public Task<SmartClipboardResult> ExecuteAsync(
        SmartClipboardRequest request,
        SmartClipboardPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();

        // Store only PII-redacted version in clipboard history ring buffer
        var (redactedForHistory, _) = PiiRedactionSkill.RedactText(approvedPreview.TransformedText, new PiiRedactionRequest(".", "."));
        if (!string.IsNullOrWhiteSpace(redactedForHistory))
        {
            if (_safeHistory.Count >= MaxHistoryItems)
            {
                _safeHistory.RemoveAt(0);
            }

            _safeHistory.Add(redactedForHistory);
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: []);

        return Task.FromResult(new SmartClipboardResult(
            approvedPreview.TransformedText,
            approvedPreview.RedactedPiiCount,
            manifest));
    }

    public static (string Output, int RedactedCount) Transform(string rawText, ClipboardTransformMode mode)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return (string.Empty, 0);
        }

        switch (mode)
        {
            case ClipboardTransformMode.CleanAndNormalizeText:
            {
                var lines = rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => Regex.Replace(l.Trim(), @"\s+", " "));
                return (string.Join("\n", lines), 0);
            }

            case ClipboardTransformMode.TsvToMarkdownTable:
            {
                var lines = rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                {
                    return (string.Empty, 0);
                }

                var sb = new StringBuilder();
                var headers = SplitRow(lines[0]);
                sb.AppendLine("| " + string.Join(" | ", headers) + " |");
                sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
                for (var i = 1; i < lines.Length; i++)
                {
                    sb.AppendLine("| " + string.Join(" | ", SplitRow(lines[i])) + " |");
                }

                return (sb.ToString().TrimEnd(), 0);
            }

            case ClipboardTransformMode.TsvToCsv:
            {
                var lines = rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder();
                foreach (var line in lines)
                {
                    sb.AppendLine(string.Join(",", SplitRow(line)));
                }

                return (sb.ToString().TrimEnd(), 0);
            }

            case ClipboardTransformMode.RedactSensitivePii:
                return PiiRedactionSkill.RedactText(rawText, new PiiRedactionRequest(".", "."));

            case ClipboardTransformMode.ExtractLinksAndEmails:
            {
                var matches = UrlOrEmailPattern.Matches(rawText)
                    .Select(m => m.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                return (string.Join("\n", matches), 0);
            }

            default:
                return (rawText.Trim(), 0);
        }
    }

    private static string[] SplitRow(string line) =>
        line.Contains('\t', StringComparison.Ordinal)
            ? line.Split('\t').Select(c => c.Trim()).ToArray()
            : line.Split(',').Select(c => c.Trim()).ToArray();
}
