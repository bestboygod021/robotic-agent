using Agent.Core.Skills.Documents;
using Agent.Core.Workflows;
using System.Text;

namespace Agent.Core.Security;

public enum PrivacyRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public sealed record FilePrivacyFinding(
    string FilePath,
    string FileName,
    int UnredactedPiiCount,
    int HiddenMetadataItemsCount,
    bool HasDeceptiveDoubleExtension);

public sealed record FolderPrivacyAuditReport(
    string DirectoryPath,
    int TotalScannedFiles,
    int FilesWithPiiCount,
    int TotalPiiInstancesCount,
    int FilesWithHiddenMetadataCount,
    int SuspiciousDoubleExtensionCount,
    int RiskScoreZeroToHundred,
    PrivacyRiskLevel RiskLevel,
    IReadOnlyList<FilePrivacyFinding> RiskyFiles,
    string SummaryFa,
    string SummaryEn);

/// <summary>
/// Local Privacy &amp; Leak Risk Scanner: audits a user folder for unredacted PII
/// (cards, IBANs, emails, phones), hidden EXIF/GPS metadata in photos/docs, and
/// deceptive double extensions, computing a 0..100 risk score and a 1-click
/// remediation workflow.
/// </summary>
public sealed class PrivacyLeakRiskScanner
{
    public FolderPrivacyAuditReport ScanFolder(string directoryPath)
    {
        var validatedDir = PathGuard.ValidateSelectedFolder(directoryPath, "اسکن ریسک حریم خصوصی پوشه");
        var findings = new List<FilePrivacyFinding>();
        var totalScanned = 0;
        var filesWithPii = 0;
        var totalPii = 0;
        var filesWithMeta = 0;
        var doubleExtCount = 0;

        var defaultPiiReq = new PiiRedactionRequest(validatedDir, validatedDir);

        foreach (var filePath in Directory.EnumerateFiles(validatedDir, "*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            totalScanned++;
            var fileName = Path.GetFileName(filePath);
            var isDoubleExt = PathGuard.IsSuspiciousDoubleExtension(filePath, out _);
            if (isDoubleExt)
            {
                doubleExtCount++;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var piiCount = 0;
            var metaCount = 0;

            if (ext is ".txt" or ".md" or ".csv" or ".log" or ".json" or ".html")
            {
                var content = File.ReadAllText(filePath, Encoding.UTF8);
                (_, piiCount) = PiiRedactionSkill.RedactText(content, defaultPiiReq);
                (_, metaCount) = MetadataScrubberSkill.ScrubTextDocumentMetadata(content);
            }
            else if (ext is ".jpg" or ".jpeg")
            {
                var bytes = File.ReadAllBytes(filePath);
                (_, metaCount) = MetadataScrubberSkill.ScrubJpegSegments(bytes);
            }

            if (piiCount > 0)
            {
                filesWithPii++;
                totalPii += piiCount;
            }

            if (metaCount > 0)
            {
                filesWithMeta++;
            }

            if (piiCount > 0 || metaCount > 0 || isDoubleExt)
            {
                findings.Add(new FilePrivacyFinding(filePath, fileName, piiCount, metaCount, isDoubleExt));
            }
        }

        var rawScore = (filesWithPii * 22) + (totalPii * 6) + (filesWithMeta * 12) + (doubleExtCount * 40);
        var score = Math.Clamp(rawScore, 0, 100);
        var level = score switch
        {
            >= 70 => PrivacyRiskLevel.Critical,
            >= 40 => PrivacyRiskLevel.High,
            >= 15 => PrivacyRiskLevel.Medium,
            _ => PrivacyRiskLevel.Low
        };

        var summaryFa = findings.Count == 0
            ? $"عالی! در بررسی {totalScanned} فایل، هیچ اطلاعات محرمانهٔ بدون محافظ یا متادیتای مخفی پیدا نشد (امتیاز ریسک: ۰ از ۱۰۰)."
            : $"هشدار حریم خصوصی (ریسک {score}/100 — {level}): در {filesWithPii} فایل تعداد {totalPii} مورد اطلاعات محرمانه (کارت/شبا/ایمیل/تلفن) و در {filesWithMeta} فایل متادیتای مخفی (EXIF/GPS) شناسایی شد.";

        var summaryEn = findings.Count == 0
            ? $"Clean! Scanned {totalScanned} files with 0 exposed PII or hidden EXIF metadata (Risk Score: 0/100)."
            : $"Privacy Alert (Risk {score}/100 — {level}): Found {totalPii} exposed PII item(s) across {filesWithPii} file(s) and hidden metadata in {filesWithMeta} file(s).";

        return new FolderPrivacyAuditReport(
            DirectoryPath: validatedDir,
            TotalScannedFiles: totalScanned,
            FilesWithPiiCount: filesWithPii,
            TotalPiiInstancesCount: totalPii,
            FilesWithHiddenMetadataCount: filesWithMeta,
            SuspiciousDoubleExtensionCount: doubleExtCount,
            RiskScoreZeroToHundred: score,
            RiskLevel: level,
            RiskyFiles: findings,
            SummaryFa: summaryFa,
            SummaryEn: summaryEn);
    }

    public static WorkflowDefinition BuildRemediationWorkflow() =>
        PlaybookCatalog.GetById("playbook.legal.gdpr_sanitization");
}
