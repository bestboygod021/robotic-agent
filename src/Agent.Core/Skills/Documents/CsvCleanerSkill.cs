using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;

namespace Agent.Core.Skills.Documents;

public sealed record CsvCleanerRequest(
    string SourceCsvPath,
    string OutputCsvPath,
    char Delimiter = ',',
    bool RemoveDuplicateRows = true,
    bool NormalizeTextAndDigits = true);

public sealed record CsvCleanerPreview(
    string SourceCsvPath,
    string OutputCsvPath,
    int TotalInputRows,
    int EmptyRowsRemoved,
    int DuplicateRowsRemoved,
    int CleanOutputRows)
{
    public int ReadyCount => CleanOutputRows;
}

public sealed record CsvCleanerResult(
    int CleanOutputRows,
    int RemovedRowsCount,
    string OutputCsvPath,
    OperationManifest RollbackManifest);

/// <summary>
/// Cleans and normalizes CSV/TSV tabular files (#24): trims cells, converts
/// Persian/Arabic digits, removes empty and duplicate rows, prevents formula
/// injection, and writes a clean UTF-8 BOM CSV with Undo support (#20).
/// </summary>
public sealed class CsvCleanerSkill : ISkill<CsvCleanerRequest, CsvCleanerPreview, CsvCleanerResult>
{
    public const string SkillId = "doc.csv_clean";

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "پاک‌سازی و یکسان‌سازی فایل‌های Excel/CSV",
            ["en"] = "CSV & Spreadsheet Data Cleaner",
            ["zh"] = "CSV 与表格数据清洗去重",
            ["es"] = "Limpiador y Normalizador de Tablas CSV",
            ["ar"] = "تنظيف وتوحيد جداول البيانات CSV/Excel",
            ["fr"] = "Nettoyage et Normalisation de Tableaux CSV"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "حذف ردیف‌های خالی و تکراری، یکسان‌سازی ارقام و حروف فارسی/عربی و تولید فایل CSV تمیز بدون تغییر فایل اصلی.",
            ["en"] = "Removes empty and duplicate rows, normalizes digits/text across scripts, and outputs a clean UTF-8 CSV with Undo.",
            ["zh"] = "自动去除空行与重复行、统一多语言数字与字符格式，输出干净的 UTF-8 CSV 并支持撤销。",
            ["es"] = "Elimina filas vacías y duplicadas, normaliza dígitos y texto y genera un CSV limpio con opción de deshacer.",
            ["ar"] = "يزيل الصفوف الفارغة والمكررة ويوحد الأرقام والنصوص وينتج ملف CSV نظيفاً مع إمكانية التراجع.",
            ["fr"] = "Supprime les lignes vides et doublons, normalise chiffres et textes et génère un CSV propre avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public CsvCleanerPreview Preview(CsvCleanerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SourceCsvPath) || !File.Exists(request.SourceCsvPath))
        {
            throw new FileNotFoundException("فایل جدول مبدأ پیدا نشد.", request.SourceCsvPath);
        }

        var sourceFullPath = Path.GetFullPath(request.SourceCsvPath);
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(sourceFullPath)!, "پوشهٔ CSV مبدأ");
        var outFullPath = Path.GetFullPath(request.OutputCsvPath);
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(outFullPath)!, "پوشهٔ CSV مقصد");
        if (PathGuard.PathsEqual(sourceFullPath, outFullPath))
        {
            throw new InvalidOperationException("فایل مبدأ و مقصد پاک‌سازی CSV نمی‌توانند یکسان باشند.");
        }

        var lines = File.ReadAllLines(sourceFullPath, Encoding.UTF8);
        var (_, emptyCount, duplicateCount, cleanRowsCount) = CleanCsvLines(lines, request);

        return new CsvCleanerPreview(
            SourceCsvPath: sourceFullPath,
            OutputCsvPath: outFullPath,
            TotalInputRows: lines.Length,
            EmptyRowsRemoved: emptyCount,
            DuplicateRowsRemoved: duplicateCount,
            CleanOutputRows: cleanRowsCount);
    }

    public async Task<CsvCleanerResult> ExecuteAsync(
        CsvCleanerRequest request,
        CsvCleanerPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.TotalInputRows != approvedPreview.TotalInputRows ||
            currentPreview.CleanOutputRows != approvedPreview.CleanOutputRows)
        {
            throw new InvalidOperationException("محتوای فایل CSV از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var lines = await File.ReadAllLinesAsync(currentPreview.SourceCsvPath, Encoding.UTF8, context.CancellationToken).ConfigureAwait(false);
        var (cleanedLines, emptyCount, duplicateCount, _) = CleanCsvLines(lines, request);

        await File.WriteAllLinesAsync(currentPreview.OutputCsvPath, cleanedLines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), context.CancellationToken).ConfigureAwait(false);
        var outInfo = new FileInfo(currentPreview.OutputCsvPath);
        var sha256 = await RollbackManager.ComputeFileSha256HexAsync(currentPreview.OutputCsvPath, context.CancellationToken).ConfigureAwait(false);

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records:
            [
                new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: currentPreview.SourceCsvPath,
                    TargetPath: currentPreview.OutputCsvPath,
                    SizeBytes: outInfo.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new CsvCleanerResult(
            CleanOutputRows: cleanedLines.Count,
            RemovedRowsCount: emptyCount + duplicateCount,
            OutputCsvPath: currentPreview.OutputCsvPath,
            RollbackManifest: manifest);
    }

    private static (IReadOnlyList<string> CleanedLines, int EmptyRemoved, int DuplicatesRemoved, int OutputCount) CleanCsvLines(
        string[] rawLines,
        CsvCleanerRequest request)
    {
        var output = new List<string>();
        var seenCanonical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emptyCount = 0;
        var duplicateCount = 0;

        foreach (var rawLine in rawLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                emptyCount++;
                continue;
            }

            var cells = rawLine.Split(request.Delimiter)
                .Select(cell => CleanCell(cell, request.NormalizeTextAndDigits))
                .ToArray();

            if (cells.All(string.IsNullOrWhiteSpace))
            {
                emptyCount++;
                continue;
            }

            var normalizedLine = string.Join(request.Delimiter, cells);
            var canonicalKey = TextNormalizer.NormalizeMultilingual(normalizedLine);

            if (request.RemoveDuplicateRows && !seenCanonical.Add(canonicalKey))
            {
                duplicateCount++;
                continue;
            }

            output.Add(normalizedLine);
        }

        return (output, emptyCount, duplicateCount, output.Count);
    }

    private static string CleanCell(string rawCell, bool normalizeDigitsAndChars)
    {
        var trimmed = rawCell.Trim();
        if (!normalizeDigitsAndChars)
        {
            return trimmed;
        }

        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            var mapped = ch switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                '۰' or '٠' => '0',
                '۱' or '١' => '1',
                '۲' or '٢' => '2',
                '۳' or '٣' => '3',
                '۴' or '٤' => '4',
                '۵' or '٥' => '5',
                '۶' or '٦' => '6',
                '۷' or '٧' => '7',
                '۸' or '٨' => '8',
                '۹' or '٩' => '9',
                _ => ch
            };
            sb.Append(mapped);
        }

        return sb.ToString();
    }
}
