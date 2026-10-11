using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public sealed record TemplateMergeRequest(
    string TemplateFilePath,
    string CsvDataFilePath,
    string OutputDirectory,
    string OutputFileNameColumn = "");

public sealed record TemplateMergeItemPreview(
    int RowIndex,
    string OutputFileName,
    string OutputFilePath,
    IReadOnlyDictionary<string, string> FieldValues);

public sealed record TemplateMergePreview(
    string TemplateFilePath,
    string CsvDataFilePath,
    string OutputDirectory,
    IReadOnlyList<string> Headers,
    IReadOnlyList<TemplateMergeItemPreview> Items)
{
    public int ReadyCount => Items.Count;
}

public sealed record TemplateMergeResult(
    int GeneratedFilesCount,
    IReadOnlyList<string> GeneratedPaths,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline Mail-Merge skill (#27) that reads a template file containing
/// {{ColumnName}} placeholders and a CSV data table, generating personalized
/// documents for every row with mandatory preview and Undo (#20).
/// </summary>
public sealed class TemplateMergeSkill : ISkill<TemplateMergeRequest, TemplateMergePreview, TemplateMergeResult>
{
    public const string SkillId = "doc.template_merge";
    public const int MaximumRowsPerMerge = 500;
    private static readonly Regex SafeFileStemChars = new(@"[^\p{L}\p{N}_\-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "پرکردن خودکار فرم‌ها و قالب‌های اداری (Mail-Merge)",
            ["en"] = "Template Mail-Merge Generator",
            ["zh"] = "模板批量填充生成（Mail-Merge）",
            ["es"] = "Generador de Documentos por Plantilla (Mail-Merge)",
            ["ar"] = "دمج المراسلات وتعبئة القوالب تلقائياً",
            ["fr"] = "Publipostage et Fusion de Modèles (Mail-Merge)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "خواندن ردیف‌های جدول CSV و تولید دسته‌ای قراردادها، نامه‌ها یا گواهی‌ها از روی یک فایل قالب ({{Field}}) همراه با پیش‌نمایش و واگرد.",
            ["en"] = "Populates a {{Field}} document template from CSV rows to batch-generate contracts, letters, or certificates with Undo.",
            ["zh"] = "读取 CSV 表格行并批量填充 {{Field}} 模板文档，自动生成合同、信函或证书并支持撤销。",
            ["es"] = "Rellena una plantilla {{Field}} desde filas CSV para generar contratos o cartas en lote con opción de deshacer.",
            ["ar"] = "يملأ قالب مستند {{Field}} من صفوف CSV لإنتاج العقود والخطابات والشهادات دفعة واحدة مع إمكانية التراجع.",
            ["fr"] = "Remplit un modèle {{Field}} à partir de lignes CSV pour générer contrats ou lettres en lot avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public TemplateMergePreview Preview(TemplateMergeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.TemplateFilePath))
        {
            throw new FileNotFoundException("فایل قالب پیدا نشد.", request.TemplateFilePath);
        }
        if (!File.Exists(request.CsvDataFilePath))
        {
            throw new FileNotFoundException("فایل دادهٔ CSV پیدا نشد.", request.CsvDataFilePath);
        }

        var templateFull = Path.GetFullPath(request.TemplateFilePath);
        var csvFull = Path.GetFullPath(request.CsvDataFilePath);
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(templateFull)!, "پوشهٔ قالب");
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(csvFull)!, "پوشهٔ CSV");
        var outDir = PathGuard.ValidateSelectedFolder(request.OutputDirectory, "مقصد تولید اسناد");

        var lines = File.ReadAllLines(csvFull, Encoding.UTF8)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length < 2)
        {
            throw new InvalidOperationException("فایل CSV باید حداقل یک سطر سرستون و یک سطر داده داشته باشد.");
        }

        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        var templateExt = Path.GetExtension(templateFull);
        var templateStem = Path.GetFileNameWithoutExtension(templateFull);
        var items = new List<TemplateMergeItemPreview>();

        for (var i = 1; i < lines.Length; i++)
        {
            if (items.Count >= MaximumRowsPerMerge)
            {
                throw new InvalidOperationException($"در هر نوبت حداکثر {MaximumRowsPerMerge} سند از روی قالب تولید می‌شود.");
            }

            var cells = lines[i].Split(',');
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length; c++)
            {
                var val = c < cells.Length ? cells[c].Trim() : string.Empty;
                map[headers[c]] = val;
            }

            var rowLabel = !string.IsNullOrWhiteSpace(request.OutputFileNameColumn) &&
                           map.TryGetValue(request.OutputFileNameColumn, out var colVal) &&
                           !string.IsNullOrWhiteSpace(colVal)
                ? SafeFileStemChars.Replace(colVal, "_")
                : i.ToString("D3", CultureInfo.InvariantCulture);

            var outName = $"{templateStem}_{rowLabel}{templateExt}";
            var outPath = Path.Combine(outDir, outName);
            items.Add(new TemplateMergeItemPreview(i, outName, outPath, map));
        }

        return new TemplateMergePreview(templateFull, csvFull, outDir, headers, items);
    }

    public async Task<TemplateMergeResult> ExecuteAsync(
        TemplateMergeRequest request,
        TemplateMergePreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Items.Count != approvedPreview.Items.Count)
        {
            throw new InvalidOperationException("سطرهای CSV از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var templateText = await File.ReadAllTextAsync(currentPreview.TemplateFilePath, Encoding.UTF8, context.CancellationToken).ConfigureAwait(false);
        var generated = new List<string>();
        var records = new List<OperationRecord>();

        foreach (var item in currentPreview.Items)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var rendered = RenderTemplate(templateText, item.FieldValues);

            await File.WriteAllTextAsync(item.OutputFilePath, rendered, new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
            var outInfo = new FileInfo(item.OutputFilePath);
            var sha256 = await RollbackManager.ComputeFileSha256HexAsync(item.OutputFilePath, context.CancellationToken).ConfigureAwait(false);

            records.Add(new OperationRecord(
                OperationRecordKind.FileCreatedOrCopied,
                OriginalPath: currentPreview.TemplateFilePath,
                TargetPath: item.OutputFilePath,
                SizeBytes: outInfo.Length,
                ContentSha256Hex: sha256));
            generated.Add(item.OutputFilePath);
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return new TemplateMergeResult(generated.Count, generated, manifest);
    }

    public static string RenderTemplate(string templateText, IReadOnlyDictionary<string, string> fields)
    {
        var output = templateText;
        foreach (var (key, value) in fields)
        {
            output = output.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
            output = output.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }
}
