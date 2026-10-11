using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Net;
using System.Text;

namespace Agent.Core.Skills.Documents;

public enum TargetDocumentFormat
{
    HtmlStandalone,
    NormalizedUtf8Text,
    MarkdownTableFromCsv
}

public sealed record BatchConvertRequest(
    string SourceDirectory,
    string DestinationDirectory,
    string SourceExtension = "md",
    TargetDocumentFormat TargetFormat = TargetDocumentFormat.HtmlStandalone,
    string LanguageCode = CultureCatalog.DefaultLanguageCode);

public sealed record BatchConvertCandidate(
    string SourcePath,
    string SourceName,
    string DestinationPath,
    TargetDocumentFormat TargetFormat,
    long SourceSizeBytes);

public sealed record BatchConvertPreview(
    string SourceDirectory,
    string DestinationDirectory,
    IReadOnlyList<BatchConvertCandidate> Candidates)
{
    public int ReadyCount => Candidates.Count;
}

public sealed record BatchConvertResult(
    int ConvertedCount,
    IReadOnlyList<string> OutputPaths,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline batch document format converter (#16): converts Markdown, plain text,
/// or CSV tables into printable RTL/LTR-aware standalone HTML documents, Markdown
/// tables, or normalized UTF-8 text without uploading files to external websites.
/// </summary>
public sealed class BatchConvertSkill : ISkill<BatchConvertRequest, BatchConvertPreview, BatchConvertResult>
{
    public const string SkillId = "doc.convert";

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تبدیل فرمت دسته‌ای آفلاین اسناد",
            ["en"] = "Offline Batch Document Converter",
            ["zh"] = "离线批量文档格式转换",
            ["es"] = "Conversor Masivo de Documentos Sin Conexión",
            ["ar"] = "تحويل صيغ المستندات دفعة واحدة دون اتصال",
            ["fr"] = "Convertisseur de Documents par Lots Hors-Ligne"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تبدیل دسته‌ای فایل‌های Markdown، متن و CSV به اسناد HTML استاندارد دوجهته (RTL/LTR) یا جداول Markdown بدون آپلود در اینترنت.",
            ["en"] = "Batch converts Markdown, text, and CSV files into standalone RTL/LTR HTML documents or Markdown tables offline with Undo.",
            ["zh"] = "本地批量将 Markdown、文本与 CSV 转换为独立 HTML 页面或 Markdown 表格，支持撤销。",
            ["es"] = "Convierte archivos Markdown, texto y CSV en documentos HTML independientes o tablas Markdown sin conexión.",
            ["ar"] = "يحول ملفات Markdown والنصوص وCSV إلى صفحات HTML قياسية تدعم العربية والفارسية محلياً مع تراجع.",
            ["fr"] = "Convertit par lots les fichiers Markdown, texte et CSV en documents HTML autonomes hors-ligne avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public BatchConvertPreview Preview(BatchConvertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "مبدأ تبدیل فرمت");
        var destDir = PathGuard.ValidateSelectedFolder(request.DestinationDirectory, "مقصد تبدیل فرمت");

        var ext = FileCopySkill.NormalizeExtension(request.SourceExtension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var targetExt = request.TargetFormat switch
        {
            TargetDocumentFormat.HtmlStandalone => ".html",
            TargetDocumentFormat.MarkdownTableFromCsv => ".md",
            _ => ".utf8.txt"
        };

        var candidates = new List<BatchConvertCandidate>();
        foreach (var filePath in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var stem = Path.GetFileNameWithoutExtension(info.Name);
            var outPath = Path.Combine(destDir, $"{stem}{targetExt}");
            candidates.Add(new BatchConvertCandidate(filePath, info.Name, outPath, request.TargetFormat, info.Length));
        }

        return new BatchConvertPreview(sourceDir, destDir, candidates);
    }

    public async Task<BatchConvertResult> ExecuteAsync(
        BatchConvertRequest request,
        BatchConvertPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Candidates.Count != approvedPreview.Candidates.Count)
        {
            throw new InvalidOperationException("فایل‌های مبدأ تبدیل فرمت تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var langMeta = CultureCatalog.GetLanguage(request.LanguageCode);
        var outputs = new List<string>();
        var records = new List<OperationRecord>();

        foreach (var item in currentPreview.Candidates)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var raw = await File.ReadAllTextAsync(item.SourcePath, Encoding.UTF8, context.CancellationToken).ConfigureAwait(false);
            var converted = ConvertContent(item.SourceName, raw, item.TargetFormat, langMeta);

            await File.WriteAllTextAsync(item.DestinationPath, converted, new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
            var outInfo = new FileInfo(item.DestinationPath);
            var sha256 = await RollbackManager.ComputeFileSha256HexAsync(item.DestinationPath, context.CancellationToken).ConfigureAwait(false);

            records.Add(new OperationRecord(
                OperationRecordKind.FileCreatedOrCopied,
                OriginalPath: item.SourcePath,
                TargetPath: item.DestinationPath,
                SizeBytes: outInfo.Length,
                ContentSha256Hex: sha256));
            outputs.Add(item.DestinationPath);
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return new BatchConvertResult(outputs.Count, outputs, manifest);
    }

    private static string ConvertContent(
        string sourceName,
        string rawContent,
        TargetDocumentFormat format,
        SupportedLanguage language)
    {
        if (format == TargetDocumentFormat.NormalizedUtf8Text)
        {
            return rawContent.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() + "\n";
        }

        if (format == TargetDocumentFormat.MarkdownTableFromCsv)
        {
            var lines = rawContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
            sb.AppendLine("| " + string.Join(" | ", headers) + " |");
            sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
            for (var i = 1; i < lines.Length; i++)
            {
                var cells = lines[i].Split(',').Select(c => c.Trim());
                sb.AppendLine("| " + string.Join(" | ", cells) + " |");
            }

            return sb.ToString();
        }

        var dir = language.IsRightToLeft ? "rtl" : "ltr";
        var encodedTitle = WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(sourceName));
        var htmlLines = new StringBuilder();
        foreach (var rawLine in rawContent.Split(['\r', '\n']))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                htmlLines.AppendLine($"<h1>{WebUtility.HtmlEncode(trimmed[2..])}</h1>");
            }
            else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                htmlLines.AppendLine($"<h2>{WebUtility.HtmlEncode(trimmed[3..])}</h2>");
            }
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                htmlLines.AppendLine($"<li>{WebUtility.HtmlEncode(trimmed[2..])}</li>");
            }
            else if (!string.IsNullOrWhiteSpace(trimmed))
            {
                htmlLines.AppendLine($"<p>{WebUtility.HtmlEncode(trimmed)}</p>");
            }
        }

        return $$"""
            <!DOCTYPE html>
            <html lang="{{language.Code}}" dir="{{dir}}">
            <head>
              <meta charset="utf-8" />
              <title>{{encodedTitle}}</title>
              <style>
                body { font-family: {{language.RecommendedFontFamily}}, sans-serif; margin: 2rem auto; max-width: 820px; line-height: 1.7; color: #182230; }
                h1, h2 { color: #1570EF; }
              </style>
            </head>
            <body>
            {{htmlLines}}
            </body>
            </html>
            """;
    }
}
