using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public enum PdfToolkitOperation
{
    ExtractText,
    MergeTextToPdf
}

public sealed record PdfToolkitRequest(
    PdfToolkitOperation Operation,
    string SourceDirectory,
    string OutputFilePath);

public sealed record PdfCandidateSummary(
    string FilePath,
    string FileName,
    int EstimatedPageCount,
    long SizeBytes);

public sealed record PdfToolkitPreview(
    PdfToolkitOperation Operation,
    string SourceDirectory,
    string OutputFilePath,
    IReadOnlyList<PdfCandidateSummary> Candidates)
{
    public int ReadyCount => Candidates.Count;
    public int TotalEstimatedPages => Candidates.Sum(c => c.EstimatedPageCount);
}

public sealed record PdfToolkitResult(
    int ProcessedFilesCount,
    int TotalPagesProcessed,
    string OutputFilePath,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline PDF toolkit (#23) supporting local text extraction from PDF streams
/// and generating/merging clean standard PDF-1.4 documents without cloud services.
/// </summary>
public sealed class PdfToolkitSkill : ISkill<PdfToolkitRequest, PdfToolkitPreview, PdfToolkitResult>
{
    public const string SkillId = "doc.pdf_toolkit";
    private static readonly Regex PageMarkerPattern = new(@"/Type\s*/Page\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PdfLiteralStringPattern = new(@"\(([^()\\]*(?:\\.[^()\\]*)*)\)\s*Tj", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "جعبهابزار آفلاین PDF (استخراج و ادغام)",
            ["en"] = "Offline PDF Toolkit (Extract & Merge)",
            ["zh"] = "离线 PDF 工具箱（文本提取与合并）",
            ["es"] = "Kit de Herramientas PDF Sin Conexión",
            ["ar"] = "أدوات PDF دون اتصال (استخراج ودمج)",
            ["fr"] = "Boîte à Outils PDF Hors-Ligne (Extraction & Fusion)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "شمارش صفحات، استخراج متن از جریان‌های PDF و ادغام چندین سند در یک فایل PDF استاندارد به‌صورت کاملاً محلی همراه با واگرد.",
            ["en"] = "Counts pages, extracts PDF text streams, and merges multiple documents into a standard PDF locally with preview and Undo.",
            ["zh"] = "本地统计页数、提取 PDF 文本流并将多份文档合并为标准 PDF，支持强制预览与撤销。",
            ["es"] = "Cuenta páginas, extrae texto de PDF y fusiona documentos en un archivo PDF estándar localmente con deshacer.",
            ["ar"] = "يحسب الصفحات ويستخرج نصوص PDF ويدمج عدة مستندات في ملف PDF قياسي محلياً مع معاينة وتراجع.",
            ["fr"] = "Compte les pages, extrait le texte PDF et fusionne plusieurs documents en un PDF standard hors-ligne avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public PdfToolkitPreview Preview(PdfToolkitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "پوشهٔ PDF");
        var outFullPath = Path.GetFullPath(request.OutputFilePath);
        var outDir = Path.GetDirectoryName(outFullPath)
            ?? throw new ArgumentException("مسیر فایل خروجی معتبر نیست.", nameof(request));
        PathGuard.ValidateSelectedFolder(outDir, "پوشهٔ خروجی PDF");

        var candidates = new List<PdfCandidateSummary>();
        foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.PathsEqual(filePath, outFullPath))
            {
                continue;
            }

            var rawText = File.ReadAllText(filePath, Encoding.Latin1);
            var pages = Math.Max(1, PageMarkerPattern.Matches(rawText).Count);
            var info = new FileInfo(filePath);
            candidates.Add(new PdfCandidateSummary(filePath, info.Name, pages, info.Length));
        }

        return new PdfToolkitPreview(request.Operation, sourceDir, outFullPath, candidates);
    }

    public async Task<PdfToolkitResult> ExecuteAsync(
        PdfToolkitRequest request,
        PdfToolkitPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        if (approvedPreview.ReadyCount == 0)
        {
            throw new InvalidOperationException("فایل PDF برای پردازش وجود ندارد.");
        }

        var currentPreview = Preview(request);
        if (currentPreview.Candidates.Count != approvedPreview.Candidates.Count)
        {
            throw new InvalidOperationException("فایل‌های PDF از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var extractedSegments = new List<(string FileName, string Content)>();
        foreach (var item in currentPreview.Candidates)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var raw = await File.ReadAllTextAsync(item.FilePath, Encoding.UTF8, context.CancellationToken).ConfigureAwait(false);
            var extracted = ExtractTextFromPdfContent(raw);
            extractedSegments.Add((item.FileName, extracted));
        }

        if (request.Operation == PdfToolkitOperation.ExtractText)
        {
            var sb = new StringBuilder();
            foreach (var (fileName, content) in extractedSegments)
            {
                sb.AppendLine($"=== {fileName} ===");
                sb.AppendLine(content);
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(currentPreview.OutputFilePath, sb.ToString(), new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            var pdfBytes = BuildMinimalMergedPdf(extractedSegments);
            await File.WriteAllBytesAsync(currentPreview.OutputFilePath, pdfBytes, context.CancellationToken).ConfigureAwait(false);
        }

        var outInfo = new FileInfo(currentPreview.OutputFilePath);
        var sha256 = await RollbackManager.ComputeFileSha256HexAsync(currentPreview.OutputFilePath, context.CancellationToken).ConfigureAwait(false);
        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records:
            [
                new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: currentPreview.SourceDirectory,
                    TargetPath: currentPreview.OutputFilePath,
                    SizeBytes: outInfo.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new PdfToolkitResult(
            currentPreview.Candidates.Count,
            currentPreview.TotalEstimatedPages,
            currentPreview.OutputFilePath,
            manifest);
    }

    public static string ExtractTextFromPdfContent(string rawPdfText)
    {
        var matches = PdfLiteralStringPattern.Matches(rawPdfText);
        if (matches.Count > 0)
        {
            return string.Join(Environment.NewLine, matches.Select(m => m.Groups[1].Value));
        }

        return rawPdfText.Trim();
    }

    public static byte[] BuildMinimalMergedPdf(IReadOnlyList<(string Title, string Body)> pages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("%PDF-1.4");
        sb.AppendLine("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj");

        var pageObjIds = new List<int>();
        var nextObjId = 4;
        var pageObjects = new StringBuilder();

        foreach (var (title, body) in pages)
        {
            var pageId = nextObjId++;
            var contentId = nextObjId++;
            pageObjIds.Add(pageId);

            var safeTitle = EscapePdfLiteral(title);
            var safeBody = EscapePdfLiteral(body.Length > 240 ? body[..240] : body);
            var streamData = $"BT /F1 12 Tf 50 750 Td ({safeTitle}) Tj 0 -24 Td ({safeBody}) Tj ET";

            pageObjects.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{pageId} 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >> endobj"));
            pageObjects.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{contentId} 0 obj << /Length {streamData.Length} >> stream\n{streamData}\nendstream endobj"));
        }

        var kids = string.Join(' ', pageObjIds.Select(id => $"{id} 0 R"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"2 0 obj << /Type /Pages /Kids [{kids}] /Count {pageObjIds.Count} >> endobj"));
        sb.AppendLine("3 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj");
        sb.Append(pageObjects);
        sb.AppendLine("trailer << /Root 1 0 R >>");
        sb.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string EscapePdfLiteral(string input)
    {
        var asciiBuilder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch is '(' or ')' or '\\')
            {
                asciiBuilder.Append('\\').Append(ch);
            }
            else if (ch >= 32 && ch <= 126)
            {
                asciiBuilder.Append(ch);
            }
            else if (ch is '\r' or '\n')
            {
                asciiBuilder.Append(' ');
            }
        }

        return asciiBuilder.ToString();
    }
}
