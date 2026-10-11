using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills;
using Agent.Core.Skills.Documents;
using System.Text;

namespace Agent.Core.UX;

public sealed record DropZoneQuickAction(
    string SkillId,
    string TitleFa,
    string TitleEn,
    string ReasonFa,
    string ReasonEn);

public sealed record SmartNudgeRecommendation(
    bool ShouldShowNudge,
    int TotalFiles,
    long TotalSizeBytes,
    string MessageFa,
    string MessageEn,
    string SuggestedSkillId);

/// <summary>
/// Windows Explorer Right-Click Context Menu generator, Floating Magic Drop-Zone
/// file-type analyzer, and Proactive Smart Nudge service for everyday users.
/// </summary>
public static class WindowsShellIntegrationService
{
    public static string GenerateCurrentUserContextMenuRegistryScript(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var escapedExe = executablePath.Replace("\\", "\\\\", StringComparison.Ordinal);

        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine(@"[HKEY_CURRENT_USER\Software\Classes\Directory\shell\RoboticAgent]");
        sb.AppendLine("@=\"🤖 انجام خودکار با رباتیک ایجنت (Robotic Agent)\"");
        sb.AppendLine($"\"Icon\"=\"{escapedExe}\"");
        sb.AppendLine();
        sb.AppendLine(@"[HKEY_CURRENT_USER\Software\Classes\Directory\shell\RoboticAgent\command]");
        sb.AppendLine($"@=\"\\\"{escapedExe}\\\" --folder \\\"%1\\\"\"");
        sb.AppendLine();
        sb.AppendLine(@"[HKEY_CURRENT_USER\Software\Classes\*\shell\RoboticAgentSanitize]");
        sb.AppendLine("@=\"🛡️ پاک‌سازی اطلاعات محرمانه و متادیتا (Robotic Agent)\"");
        sb.AppendLine($"\"Icon\"=\"{escapedExe}\"");
        sb.AppendLine();
        sb.AppendLine(@"[HKEY_CURRENT_USER\Software\Classes\*\shell\RoboticAgentSanitize\command]");
        sb.AppendLine($"@=\"\\\"{escapedExe}\\\" --sanitize \\\"%1\\\"\"");

        return sb.ToString();
    }

    public static IReadOnlyList<DropZoneQuickAction> AnalyzeDroppedItems(IReadOnlyList<string> droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);
        if (droppedPaths.Count == 0)
        {
            return [];
        }

        var extensions = droppedPaths
            .Where(p => !Directory.Exists(p))
            .Select(p => Path.GetExtension(p).ToLowerInvariant())
            .Distinct()
            .ToArray();

        if (extensions.Any(e => e is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp"))
        {
            return
            [
                new(MediaAndPdfOptimizeSkill.SkillId, "تبدیل عکس‌ها به یک فایل PDF و بهینه‌سازی حجم", "Convert Images to Single PDF & Optimize Size", "مناسب برای ارسال مدارک و جزوات", "Ideal for uploading scanned IDs & receipts"),
                new(MetadataScrubberSkill.SkillId, "حذف مکان GPS و اطلاعات مخفی دوربین (EXIF)", "Strip Hidden GPS & Camera EXIF Metadata", "حفظ حریم خصوصی پیش از اشتراک‌گذاری", "Protects privacy before sharing photos"),
                new(OfflineOcrSkill.SkillId, "استخراج متن داخل تصویر (OCR آفلاین)", "Extract Text from Image (Offline OCR)", "تبدیل عکس به متن قابل ویرایش", "Converts image text into editable UTF-8 text")
            ];
        }

        if (extensions.Any(e => e == ".pdf"))
        {
            return
            [
                new(PdfToolkitSkill.SkillId, "ادغام PDFها و استخراج متن", "Merge PDFs & Extract Text", "مدیریت سریع صفحات PDF بدون اینترنت", "Fast offline PDF page management"),
                new(PiiRedactionSkill.SkillId, "سانسور اطلاعات محرمانه (کارت، شبا، تلفن، ایمیل)", "Redact Sensitive PII (Card, IBAN, Phone, Email)", "ایجاد نسخهٔ امن _sanitized", "Creates a safe _sanitized copy"),
                new(BatchSummarizeSkill.SkillId, "خلاصه‌سازی و استخراج فهرست اقدام‌ها", "Summarize & Extract Action Items", "تولید چکیدهٔ اجرایی", "Generates an executive summary & checklist")
            ];
        }

        if (extensions.Any(e => e == ".csv"))
        {
            return
            [
                new(CsvCleanerSkill.SkillId, "پاک‌سازی جدول CSV و حذف ردیف‌های تکراری", "Clean CSV & Remove Duplicate Rows", "اصلاح ی/ک و ارقام فارسی/عربی", "Normalizes characters/digits and deduplicates"),
                new(BatchConvertSkill.SkillId, "تبدیل جدول CSV به گزارش Markdown/HTML", "Convert CSV to Markdown/HTML Table", "آماده برای گزارش‌دهی", "Ready for documentation & reports"),
                new(PiiRedactionSkill.SkillId, "ماسک کردن اطلاعات شخصی مشتریان در جدول", "Mask Customer PII in Spreadsheet", "حفاظت از داده‌های مشتریان", "Protects customer contact & card data")
            ];
        }

        return
        [
            new(FolderOrganizeSkill.SkillId, "مرتب‌سازی خودکار در ۶ پوشهٔ استاندارد", "Auto-Organize into 6 Standard Folders", "مرتب‌سازی اسناد، تصاویر، آرشیوها و کدها", "Sorts documents, images, archives & code"),
            new(DuplicateFinderSkill.SkillId, "یافتن و قرنطینهٔ فایل‌های تکراری (SHA-256)", "Find & Quarantine Duplicate Files (SHA-256)", "آزادسازی فضای دیسک با امکان واگرد", "Frees disk space with 1-click Undo"),
            new(IncrementalBackupSkill.SkillId, "پشتیبان‌گیری افزایشی هوشمند", "Smart Incremental Backup", "کپی فقط فایل‌های جدید و تغییریافته", "Backs up only new or modified files")
        ];
    }

    public static SmartNudgeRecommendation EvaluateFolderForProactiveNudge(
        string directoryPath,
        int fileCountThreshold = 25)
    {
        var validated = PathGuard.ValidateSelectedFolder(directoryPath, "بررسی هوشمند پوشه");
        var files = Directory.EnumerateFiles(validated, "*", SearchOption.TopDirectoryOnly).ToArray();
        long totalBytes = files.Sum(f => new FileInfo(f).Length);

        if (files.Length < fileCountThreshold)
        {
            return new SmartNudgeRecommendation(
                ShouldShowNudge: false,
                TotalFiles: files.Length,
                TotalSizeBytes: totalBytes,
                MessageFa: "پوشهٔ شما مرتب و خلوت است.",
                MessageEn: "Your folder is clean and organized.",
                SuggestedSkillId: FolderOrganizeSkill.SkillId);
        }

        var formattedSizeFa = CultureCatalog.FormatFileSize(totalBytes, "fa");
        var formattedSizeEn = CultureCatalog.FormatFileSize(totalBytes, "en");

        return new SmartNudgeRecommendation(
            ShouldShowNudge: true,
            TotalFiles: files.Length,
            TotalSizeBytes: totalBytes,
            MessageFa: $"پیشنهاد هوشمند: در این پوشه {files.Length} فایل ({formattedSizeFa}) انباشته شده است. آیا مایلید پیش‌نمایش مرتب‌سازی خودکار و یافتن فایل‌های تکراری را مشاهده کنید؟",
            MessageEn: $"Smart Nudge: This folder has {files.Length} files ({formattedSizeEn}). Would you like to preview automatic organization and duplicate cleanup?",
            SuggestedSkillId: FolderOrganizeSkill.SkillId);
    }
}
