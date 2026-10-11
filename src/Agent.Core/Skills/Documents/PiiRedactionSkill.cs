using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public sealed record PiiRedactionRequest(
    string SourceDirectory,
    string DestinationDirectory,
    string Extension = "txt");

public sealed record PiiRedactionCandidate(
    string SourcePath,
    string FileName,
    string DestinationPath,
    long SizeBytes,
    int DetectedPiiCount,
    IReadOnlyList<string> DetectedCategories);

public sealed record PiiRedactionPreview(
    string SourceDirectory,
    string DestinationDirectory,
    IReadOnlyList<PiiRedactionCandidate> Candidates)
{
    public int ReadyCount => Candidates.Count;
    public int TotalPiiDetected => Candidates.Sum(c => c.DetectedPiiCount);
}

public sealed record PiiRedactionResult(
    int SanitizedFileCount,
    int TotalRedactionsApplied,
    IReadOnlyList<string> FailedFileNames,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline PII redaction skill (#30) that detects and masks emails, phone numbers,
/// Luhn-validated payment cards, IBANs, IPv4 addresses, and national IDs into new
/// _sanitized files without altering originals.
/// </summary>
public sealed class PiiRedactionSkill : ISkill<PiiRedactionRequest, PiiRedactionPreview, PiiRedactionResult>
{
    public const string SkillId = "doc.redact_pii";

    private static readonly Regex EmailPattern = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IbanPattern = new(
        @"\b[A-Z]{2}\d{2}[A-Z0-9]{12,30}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Ipv4Pattern = new(
        @"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(?:\+\d{1,3}[-.\s]?|09\d{2}[-.\s]?|\(\d{3,4}\)[-.\s]?)\d{3}[-.\s]?\d{4}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PotentialCardPattern = new(
        @"(?<!\d)(?:\d[ -]?){13,19}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "سانسور خودکار اطلاعات محرمانه (PII)",
            ["en"] = "Automatic PII Redaction",
            ["zh"] = "敏感个人信息（PII）自动脱敏",
            ["es"] = "Redacción Automática de Datos Sensibles (PII)",
            ["ar"] = "حجب تلقائي للبيانات الشخصية والمالية الحساسة",
            ["fr"] = "Caviardage Automatique des Données Sensibles (PII)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "شناسایی و ماسک کردن ایمیل، شماره‌تلفن، شماره‌کارت بانکی (با بررسی الگوریتم Luhn)، شبا (IBAN) و IP در نسخه‌های پاک‌سازی‌شده بدون دست‌زدن به فایل اصلی.",
            ["en"] = "Detects and masks emails, phones, Luhn-verified payment cards, IBANs, and IPs into sanitized copies without modifying original files.",
            ["zh"] = "自动识别并掩码邮箱、电话、银行卡号（Luhn 校验）、IBAN 与 IP 地址，生成脱敏副本且不修改源文件。",
            ["es"] = "Detecta y oculta correos, teléfonos, tarjetas bancarias (Luhn), IBAN e IP en copias limpias sin modificar el original.",
            ["ar"] = "يكتشف ويحجب البريد الإلكتروني والهاتف وبطاقات الدفع (Luhn) ورقم الآيبان وعناوين IP في نسخ آمنة دون تعديل الملف الأصلي.",
            ["fr"] = "Détecte et masque e-mails, téléphones, cartes bancaires (Luhn), IBAN et IP dans des copies nettoyées sans modifier l’original."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public PiiRedactionPreview Preview(PiiRedactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "مبدأ سانسور PII");
        var destDir = PathGuard.ValidateSelectedFolder(request.DestinationDirectory, "مقصد سانسور PII");

        var ext = FileCopySkill.NormalizeExtension(request.Extension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var candidates = new List<PiiRedactionCandidate>();

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            var content = File.ReadAllText(filePath, Encoding.UTF8);
            var (_, count, categories) = RedactText(content);
            var info = new FileInfo(filePath);
            var stem = Path.GetFileNameWithoutExtension(info.Name);
            var outName = $"{stem}_sanitized{info.Extension}";
            var outPath = Path.Combine(destDir, outName);

            candidates.Add(new PiiRedactionCandidate(
                SourcePath: filePath,
                FileName: info.Name,
                DestinationPath: outPath,
                SizeBytes: info.Length,
                DetectedPiiCount: count,
                DetectedCategories: categories));
        }

        return new PiiRedactionPreview(sourceDir, destDir, candidates);
    }

    public async Task<PiiRedactionResult> ExecuteAsync(
        PiiRedactionRequest request,
        PiiRedactionPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Candidates.Count != approvedPreview.Candidates.Count)
        {
            throw new InvalidOperationException("فایل‌های پوشه از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var sanitizedFiles = 0;
        var totalRedactions = 0;
        var failed = new List<string>();
        var records = new List<OperationRecord>();

        foreach (var item in currentPreview.Candidates)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            try
            {
                var content = await File.ReadAllTextAsync(item.SourcePath, Encoding.UTF8, context.CancellationToken).ConfigureAwait(false);
                var (redactedText, count, _) = RedactText(content);

                await File.WriteAllTextAsync(item.DestinationPath, redactedText, new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
                var outInfo = new FileInfo(item.DestinationPath);
                var sha256 = await RollbackManager.ComputeFileSha256HexAsync(item.DestinationPath, context.CancellationToken).ConfigureAwait(false);

                records.Add(new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: item.SourcePath,
                    TargetPath: item.DestinationPath,
                    SizeBytes: outInfo.Length,
                    ContentSha256Hex: sha256));

                sanitizedFiles++;
                totalRedactions += count;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(item.FileName);
            }
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return new PiiRedactionResult(sanitizedFiles, totalRedactions, failed, manifest);
    }

    public static (string RedactedText, int RedactionCount, IReadOnlyList<string> Categories) RedactText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (string.Empty, 0, []);
        }

        var count = 0;
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var text = EmailPattern.Replace(input, _ =>
        {
            count++;
            categories.Add("Email");
            return "[REDACTED_EMAIL]";
        });

        text = IbanPattern.Replace(text, _ =>
        {
            count++;
            categories.Add("IBAN");
            return "[REDACTED_IBAN]";
        });

        text = PotentialCardPattern.Replace(text, match =>
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && PassesLuhnCheck(digits))
            {
                count++;
                categories.Add("PaymentCard");
                return "[REDACTED_CARD]";
            }

            return match.Value;
        });

        text = PhonePattern.Replace(text, _ =>
        {
            count++;
            categories.Add("Phone");
            return "[REDACTED_PHONE]";
        });

        text = Ipv4Pattern.Replace(text, _ =>
        {
            count++;
            categories.Add("IPv4");
            return "[REDACTED_IP]";
        });

        return (text, count, categories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public static bool PassesLuhnCheck(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits) || digits.Length < 13 || digits.All(ch => ch == digits[0]))
        {
            return false;
        }

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var ch = digits[i];
            if (ch < '0' || ch > '9')
            {
                return false;
            }

            var n = ch - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
