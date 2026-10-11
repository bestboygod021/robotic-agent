using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public sealed record ExtractedInvoiceRow(
    string SourceFileName,
    string InvoiceNumber,
    string InvoiceDate,
    string VendorName,
    string TotalAmount,
    string TaxAmount,
    string Currency);

public sealed record InvoiceExtractRequest(
    string SourceDirectory,
    string OutputCsvPath,
    string Extension = "*");

public sealed record InvoiceExtractPreview(
    string SourceDirectory,
    string OutputCsvPath,
    IReadOnlyList<ExtractedInvoiceRow> Rows)
{
    public int ReadyCount => Rows.Count;
}

public sealed record InvoiceExtractResult(
    int ExtractedCount,
    string OutputCsvPath,
    OperationManifest RollbackManifest);

/// <summary>
/// Extracts structured invoice/receipt fields (Invoice #, Date, Vendor, Total,
/// Tax, Currency) across all 6 supported languages into an Excel-ready UTF-8 CSV
/// table (#22) with mandatory preview and Undo (#20).
/// </summary>
public sealed class InvoiceExtractSkill : ISkill<InvoiceExtractRequest, InvoiceExtractPreview, InvoiceExtractResult>
{
    public const string SkillId = "doc.invoice_extract";

    private static readonly Regex InvoiceNumberPattern = new(
        @"(?:Invoice\s*(?:No|Number|#)?|شماره\s*فاکتور|فاکتور\s*شماره|رقم\s*الفاتور[ةه]|Factura\s*(?:N[oº]|#)?|Facture\s*(?:N[o°]|#)?|发票号码|发票号)\s*[:#\-]?\s*([A-Za-z0-9\-_/]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DatePattern = new(
        @"(?:Date|تاریخ|التاريخ|Fecha|日期)\s*[:\-]?\s*(\d{4}[/\-\.]\d{1,2}[/\-\.]\d{1,2}|\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VendorPattern = new(
        @"(?:Vendor|Seller|From|فروشنده|شرکت|المورد|البائع|Proveedor|Fournisseur|供应商|销售方)\s*[:\-]?\s*([^\r\n,;]{2,60})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TotalPattern = new(
        @"(?:Total(?:\s*Amount)?|Amount\s*Due|مبلغ\s*کل|جمع\s*کل|قابل\s*پرداخت|الإجمالي|المبلغ\s*الإجمالي|Total\s*a\s*pagar|Montant\s*total|总计|合计|金额)\s*[:\-]?\s*([$€£¥]?\s*\d[\d,\.]*\s*(?:USD|EUR|IRR|IRT|TOMAN|تومان|ریال|CNY|RMB|SAR|AED)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TaxPattern = new(
        @"(?:Tax|VAT|مالیات|ارزش\s*افزوده|الضريب[ةه]|IVA|TVA|税额|增值税)\s*[:\-]?\s*([$€£¥]?\s*\d[\d,\.]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "استخراج خودکار فاکتور و رسید به اکسل/CSV",
            ["en"] = "Invoice & Receipt Extractor to CSV/Excel",
            ["zh"] = "发票与收据结构化提取（导出 CSV/Excel）",
            ["es"] = "Extractor de Facturas y Recibos a CSV/Excel",
            ["ar"] = "استخراج الفواتير والإيصالات إلى جدول CSV/Excel",
            ["fr"] = "Extraction de Factures et Reçus vers CSV/Excel"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "خواندن شمارهٔ فاکتور، تاریخ، نام فروشنده، مالیات و مبلغ کل از فایل‌های پوشه در ۶ زبان و ساخت جدول CSV آمادهٔ حسابداری.",
            ["en"] = "Extracts invoice number, date, vendor, tax, and total across 6 languages into an accounting-ready CSV table with preview and Undo.",
            ["zh"] = "支持 6 种语言的发票号、日期、供应商、税额与总金额自动提取，生成财务就绪的 CSV 表格并支持撤销。",
            ["es"] = "Extrae número de factura, fecha, proveedor, impuestos y total en 6 idiomas a una tabla CSV lista para contabilidad.",
            ["ar"] = "يستخرج رقم الفاتورة والتاريخ والمورد والضريبة والإجمالي بـ 6 لغات في جدول CSV جاهز للمحاسبة.",
            ["fr"] = "Extrait numéro de facture, date, fournisseur, TVA et total en 6 langues vers un fichier CSV comptable."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public InvoiceExtractPreview Preview(InvoiceExtractRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "پوشهٔ فاکتورها");
        var outFullPath = Path.GetFullPath(request.OutputCsvPath);
        var outDir = Path.GetDirectoryName(outFullPath)
            ?? throw new ArgumentException("مسیر فایل CSV خروجی معتبر نیست.", nameof(request));
        PathGuard.ValidateSelectedFolder(outDir, "پوشهٔ خروجی CSV");

        var ext = FileCopySkill.NormalizeExtension(request.Extension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var rows = new List<ExtractedInvoiceRow>();

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _) ||
                PathGuard.PathsEqual(filePath, outFullPath))
            {
                continue;
            }

            var rawText = File.ReadAllText(filePath, Encoding.UTF8);
            rows.Add(ParseInvoiceText(Path.GetFileName(filePath), rawText));
        }

        return new InvoiceExtractPreview(sourceDir, outFullPath, rows);
    }

    public async Task<InvoiceExtractResult> ExecuteAsync(
        InvoiceExtractRequest request,
        InvoiceExtractPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        if (approvedPreview.ReadyCount == 0)
        {
            throw new InvalidOperationException("فاکتوری برای استخراج در پیش‌نمایش وجود ندارد.");
        }

        var currentPreview = Preview(request);
        if (currentPreview.Rows.Count != approvedPreview.Rows.Count)
        {
            throw new InvalidOperationException("فایل‌های پوشهٔ فاکتور تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("SourceFile,InvoiceNumber,InvoiceDate,VendorName,TotalAmount,TaxAmount,Currency");
        foreach (var row in currentPreview.Rows)
        {
            csvBuilder.AppendLine(string.Join(",",
                EscapeCsv(row.SourceFileName),
                EscapeCsv(row.InvoiceNumber),
                EscapeCsv(row.InvoiceDate),
                EscapeCsv(row.VendorName),
                EscapeCsv(row.TotalAmount),
                EscapeCsv(row.TaxAmount),
                EscapeCsv(row.Currency)));
        }

        await File.WriteAllTextAsync(currentPreview.OutputCsvPath, csvBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), context.CancellationToken).ConfigureAwait(false);
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
                    OriginalPath: currentPreview.SourceDirectory,
                    TargetPath: currentPreview.OutputCsvPath,
                    SizeBytes: outInfo.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new InvoiceExtractResult(currentPreview.Rows.Count, currentPreview.OutputCsvPath, manifest);
    }

    public static ExtractedInvoiceRow ParseInvoiceText(string fileName, string rawText)
    {
        // Convert Persian/Arabic digits to ASCII while keeping line structure
        var normalizedDigits = NormalizeDigitsPreservingLines(rawText);

        var invNo = MatchFirstGroup(InvoiceNumberPattern, normalizedDigits, "UNKNOWN");
        var date = MatchFirstGroup(DatePattern, normalizedDigits, "UNKNOWN");
        var vendor = MatchFirstGroup(VendorPattern, normalizedDigits, "UNKNOWN");
        var totalRaw = MatchFirstGroup(TotalPattern, normalizedDigits, "0");
        var taxRaw = MatchFirstGroup(TaxPattern, normalizedDigits, "0");

        var currency = DetectCurrency(totalRaw + " " + normalizedDigits);
        var numericTotal = ExtractNumericAmount(totalRaw);
        var numericTax = ExtractNumericAmount(taxRaw);

        return new ExtractedInvoiceRow(
            SourceFileName: fileName,
            InvoiceNumber: invNo,
            InvoiceDate: date,
            VendorName: vendor,
            TotalAmount: numericTotal,
            TaxAmount: numericTax,
            Currency: currency);
    }

    private static string NormalizeDigitsPreservingLines(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var mapped = ch switch
            {
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
                'ي' => 'ی',
                'ك' => 'ک',
                _ => ch
            };
            sb.Append(mapped);
        }

        return sb.ToString();
    }

    private static string MatchFirstGroup(Regex regex, string input, string fallback)
    {
        var match = regex.Match(input);
        return match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value)
            ? match.Groups[1].Value.Trim()
            : fallback;
    }

    private static string ExtractNumericAmount(string value)
    {
        var m = Regex.Match(value, @"\d[\d,\.]*");
        return m.Success ? m.Value.Replace(",", string.Empty, StringComparison.Ordinal) : "0";
    }

    private static string DetectCurrency(string text)
    {
        if (text.Contains("تومان", StringComparison.OrdinalIgnoreCase) || text.Contains("TOMAN", StringComparison.OrdinalIgnoreCase) || text.Contains("IRT", StringComparison.OrdinalIgnoreCase))
        {
            return "IRT";
        }
        if (text.Contains("ریال", StringComparison.OrdinalIgnoreCase) || text.Contains("IRR", StringComparison.OrdinalIgnoreCase))
        {
            return "IRR";
        }
        if (text.Contains('$') || text.Contains("USD", StringComparison.OrdinalIgnoreCase))
        {
            return "USD";
        }
        if (text.Contains('€') || text.Contains("EUR", StringComparison.OrdinalIgnoreCase))
        {
            return "EUR";
        }
        if (text.Contains('¥') || text.Contains("CNY", StringComparison.OrdinalIgnoreCase) || text.Contains("RMB", StringComparison.OrdinalIgnoreCase))
        {
            return "CNY";
        }

        return "UNSPECIFIED";
    }

    private static string EscapeCsv(string value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@')
        {
            safe = "'" + safe; // Prevent CSV formula injection in Excel
        }

        if (safe.Contains(',', StringComparison.Ordinal) ||
            safe.Contains('"', StringComparison.Ordinal) ||
            safe.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return safe;
    }
}
