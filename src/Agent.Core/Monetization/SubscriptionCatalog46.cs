namespace Agent.Core.Monetization;

public enum SubscriptionCategory
{
    PersonalAndGeneral,
    SkillFocusedPacks,
    SmbJobPlaybooks,
    HybridAiAndPromptCredits,
    TeamAndEnterprise
}

public enum BillingCycleKind
{
    Free,
    Monthly,
    Annual,
    LifetimeOneTime,
    CreditTopUp
}

public sealed record SubscriptionPlanModel(
    int PlanNumber,
    string SkuId,
    SubscriptionCategory Category,
    BillingCycleKind BillingCycle,
    string TitleFa,
    string TitleEn,
    string DescriptionFa,
    string DescriptionEn,
    decimal PriceUsd,
    long PriceToman,
    int MaxFilesPerBatch,
    int IncludedSeatCount,
    IReadOnlyList<string> IncludedHighlightsFa,
    IReadOnlyList<string> IncludedHighlightsEn);

/// <summary>
/// Complete Catalog of 46 Subscription &amp; Plan Models (SKU #1 .. SKU #46) tailored
/// to all capabilities built across Phases 1–11 (Personal, Skill-Specific, 20 SMB
/// Job Playbooks, Hybrid AI Credit Packs, and Team/Enterprise/White-Label).
/// </summary>
public static class SubscriptionCatalog46
{
    public static IReadOnlyList<SubscriptionPlanModel> AllPlans { get; } = BuildAll46Plans();

    public static SubscriptionPlanModel GetBySku(string skuId) =>
        AllPlans.FirstOrDefault(p => string.Equals(p.SkuId, skuId, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"طرح اشتراک با شناسهٔ '{skuId}' پیدا نشد.");

    public static IReadOnlyList<SubscriptionPlanModel> FilterPlans(
        SubscriptionCategory? category = null,
        string? searchQuery = null)
    {
        var query = AllPlans.AsEnumerable();
        if (category is not null)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var q = searchQuery.Trim();
            query = query.Where(p =>
                p.SkuId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.TitleFa.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.TitleEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.DescriptionFa.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.DescriptionEn.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(p => p.PlanNumber).ToArray();
    }

    private static IReadOnlyList<SubscriptionPlanModel> BuildAll46Plans() =>
    [
        // =====================================================================
        // Category 1: Personal & General Tiers (6 Models: #1 .. #6)
        // =====================================================================
        Plan(1, "plan.01.free_starter", SubscriptionCategory.PersonalAndGeneral, BillingCycleKind.Free,
            "۱. اشتراک رایگان پایه (Free Starter)", "1. Free Starter Edition",
            "دسترسی به کاتالوگ ۲۰۰ ابزار AI، مربی پرامپت‌ساز و مهارت‌های پایهٔ فایل تا ۵۰ فایل در هر نوبت",
            "Full 200 AI Tools Hub, Prompt Coach, and core file skills up to 50 files/batch",
            0m, 0, 50, 1),

        Plan(2, "plan.02.plus_monthly", SubscriptionCategory.PersonalAndGeneral, BillingCycleKind.Monthly,
            "۲. اشتراک پلاس ماهانه (Plus Monthly)", "2. Plus Monthly Plan",
            "افزایش سقف به ۵۰۰ فایل در هر نوبت + تمام ۶ مهارت مدیریت فایل و بهینه‌سازی عکس به PDF",
            "Up to 500 files/batch + all 6 file management skills & image-to-PDF optimizer",
            4.99m, 290_000, 500, 1),

        Plan(3, "plan.03.plus_annual", SubscriptionCategory.PersonalAndGeneral, BillingCycleKind.Annual,
            "۳. اشتراک پلاس سالانه (Plus Annual — ۲۰٪ تخفیف)", "3. Plus Annual Plan (20% Off)",
            "اشتراک یک‌سالهٔ پلاس با صرفه‌جویی اقتصادی برای کاربران خانگی و دانشجویان",
            "Full-year Plus access for students and everyday desktop users",
            47.00m, 2_750_000, 500, 1),

        Plan(4, "plan.04.pro_monthly", SubscriptionCategory.PersonalAndGeneral, BillingCycleKind.Monthly,
            "۴. اشتراک حرفه‌ای ماهانه (Pro Monthly)", "4. Pro Monthly Plan",
            "دسترسی کامل به هر ۲۴ مهارت آفلاین، ۲۰ قالب شغلی، نگهبان پوشه و زمان‌بند تا ۵٬۰۰۰ فایل",
            "Unlocks all 24 offline skills, 20 SMB Playbooks, Folder Watcher & Scheduler (5,000 files)",
            9.99m, 590_000, 5000, 1),

        Plan(5, "plan.05.pro_annual", SubscriptionCategory.PersonalAndGeneral, BillingCycleKind.Annual,
            "۵. اشتراک حرفه‌ای سالانه (Pro Annual)", "5. Pro Annual Plan",
            "یک سال دسترسی کامل حرفه‌ای به تمام ۲۴ مهارت، استودیو گردش‌کار و اسکنر حریم خصوصی",
            "Annual full access to all 24 skills, Workflow Studio, and Privacy Leak Scanner",
            89.00m, 5_200_000, 5000, 1),

        Plan(6, "plan.06.pro_lifetime", SubscriptionCategory.PersonalAndGeneral, BillingCycleKind.LifetimeOneTime,
            "۶. لایسنس مادام‌العمر حرفه‌ای (Pro Lifetime Deal — LTD)", "6. Pro Lifetime Deal (One-Time Payment)",
            "پرداخت یک‌بار برای همیشه؛ باز شدن دائمی تمام ۲۴ مهارت، ۲۰ قالب شغلی و سرور MCP",
            "Pay once, own forever: unlocks all 24 skills, 20 SMB Playbooks & MCP Server",
            49.00m, 2_950_000, 5000, 2),

        // =====================================================================
        // Category 2: Skill-Focused Micro-Subscriptions (10 Models: #7 .. #16)
        // =====================================================================
        Plan(7, "plan.07.file_organizer_pack", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۷. بستهٔ تخصصی مرتب‌سازی، تغییر نام و حذف تکراری‌ها", "7. File Organizer, Batch Rename & Dedup Pack",
            "ویژهٔ مرتب‌سازی خودکار پوشهٔ دانلودها، قرنطینهٔ تکراری‌ها (SHA-256) و پشتیبان افزایشی",
            "Dedicated pack for auto-organizing folders, SHA-256 deduplication, and incremental backup",
            2.99m, 175_000, 2000, 1),

        Plan(8, "plan.08.pdf_ocr_master", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۸. بستهٔ تخصصی جعبهٔ ابزار PDF، اسکن به PDF و OCR آفلاین", "8. PDF Toolkit, Scan-to-PDF & Offline OCR Pack",
            "ادغام نامحدود PDF، استخراج متن، تبدیل عکس‌های موبایل به PDF و OCR چندزبانه",
            "Unlimited offline PDF merging, text extraction, mobile scan-to-PDF, and 6-language OCR",
            3.99m, 235_000, 2000, 1),

        Plan(9, "plan.09.privacy_pii_shield", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۹. بستهٔ سپر حریم خصوصی (سانسور PII + حذف EXIF/GPS + اسکنر ریسک)", "9. Privacy Shield (PII Redaction + EXIF Scrubber + Risk Scanner)",
            "ماسک کردن شماره کارت (Luhn)، شبا، ایمیل و حذف موقعیت مکانی عکس‌ها در نسخه‌های _sanitized",
            "Redacts credit cards, IBANs, emails, and strips photo GPS metadata into _sanitized copies",
            4.50m, 265_000, 2500, 1),

        Plan(10, "plan.10.invoice_csv_extractor", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۱۰. بستهٔ استخراج فاکتور به اکسل و تمیزکاری جداول CSV", "10. Invoice-to-CSV Extractor & Spreadsheet Cleaner",
            "استخراج خودکار شماره فاکتور، مبلغ، مالیات و فروشنده به جدول CSV و حذف ردیف‌های تکراری",
            "Extracts invoice fields to CSV and normalizes/deduplicates spreadsheet rows",
            4.99m, 290_000, 3000, 1),

        Plan(11, "plan.11.bank_reconciliation_pro", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۱۱. بستهٔ مغایرت‌گیری خودکار بانکی و تطبیق صورت‌حساب", "11. Automated Bank & Invoice Reconciliation Pack",
            "تطبیق خودکار فایل صورت‌حساب بانک با فاکتورها و کشف کسری واریزی یا فاکتورهای معوق",
            "Matches issued invoices against bank statement CSVs to flag unpaid and discrepant items",
            5.99m, 350_000, 5000, 1),

        Plan(12, "plan.12.meeting_minutes_assistant", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۱۲. بستهٔ دستیار هوشمند جلسات و تولید صورت‌جلسهٔ رسمی", "12. Smart Meeting Minutes & Action Item Pack",
            "تحلیل متن جلسات، تشخیص شرکت‌کنندگان، استخراج مصوبات (Decisions) و اقدام‌ها",
            "Extracts speakers, formal decisions, and action items from meeting transcripts",
            3.99m, 235_000, 1500, 1),

        Plan(13, "plan.13.subtitle_localization_studio", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۱۳. بستهٔ استودیو زیرنویس (همگام‌سازی زمان SRT، اصلاح ی/ک و حذف تبلیغات)", "13. Subtitle & Localization Studio Pack (SRT Sync & Clean)",
            "تنظیم تأخیر زمانی زیرنویس، حذف تبلیغات کانال‌ها و استخراج متن خالص فیلم و ویدیو",
            "Shifts SRT timestamps, strips spam cues, normalizes script, and extracts transcripts",
            2.99m, 175_000, 2000, 1),

        Plan(14, "plan.14.contract_diff_mailmerge", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۱۴. بستهٔ مقایسهٔ قراردادها (Diff)، قالب‌ساز دوزبانه و Mail-Merge", "14. Contract Diff, Bilingual NDA Builder & Mail-Merge Pack",
            "مقایسهٔ خط‌به‌خط دو نسخه از قرارداد، تولید دسته‌ای گواهی/نامه از CSV و قرارداد دوزبانه",
            "Line-by-line contract diffing, CSV template mail-merge, and bilingual NDA/Contract builder",
            4.99m, 290_000, 2000, 1),

        Plan(15, "plan.15.desktop_ui_automation", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Monthly,
            "۱۵. بستهٔ اتوماسیون دسکتاپ ویندوز، ضبط‌کنندهٔ کار و کلیپ‌بورد هوشمند", "15. Desktop UI Automation, Action Recorder & Smart Clipboard",
            "اجرای گام‌های رابط کاربری با هایلایت هدف، ضبط مراحل کار کاربر و تبدیل‌گر کلیپ‌بورد",
            "Windows UI automation with highlight bounds, Teach-by-Demonstration recorder & Smart Clipboard",
            4.99m, 290_000, 2000, 1),

        Plan(16, "plan.16.mcp_cli_developer", SubscriptionCategory.SkillFocusedPacks, BillingCycleKind.Annual,
            "۱۶. اشتراک توسعه‌دهندگان (سرور استاندارد MCP + خط فرمان چندسکویی agent-cli)", "16. Developer Edition (MCP JSON-RPC Server + Cross-Platform CLI)",
            "اتصال مستقیم مهارت‌های محلی به Claude Desktop، Cursor و VS Code + اجرای اسکریپتی در لینوکس/مک/ویندوز",
            "Connects all 24 local skills to Claude Desktop, Cursor & VS Code via MCP + full CLI",
            29.00m, 1_700_000, 5000, 1),

        // =====================================================================
        // Category 3: 20 SMB Job Playbook Subscriptions (20 Models: #17 .. #36)
        // =====================================================================
        Plan(17, "plan.17.playbook_acc_invoice_pipeline", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۱۷. اشتراک قالب شغلی: پایپ‌لاین فاکتورهای ماهانه به اکسل/CSV", "17. Playbook Plan: Monthly Invoice-to-CSV Pipeline",
            "اجرای خودکار زنجیرهٔ استخراج فاکتور + تمیزکاری CSV + آرشیو امن ZIP",
            "Automated pipeline: Invoice extraction -> CSV deduplication -> Verified ZIP bundle",
            3.50m, 210_000, 1500, 1),

        Plan(18, "plan.18.playbook_acc_tax_audit", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۱۸. اشتراک قالب شغلی: ممیزی پوشهٔ مالیاتی و پشتیبان افزایشی", "18. Playbook Plan: Tax Folder Audit & Incremental Backup",
            "تولید گزارش تحلیلی اسناد مالیاتی و بکاپ افزایشی خودکار",
            "Generates tax inventory reports and runs incremental backups",
            3.50m, 210_000, 1500, 1),

        Plan(19, "plan.19.playbook_acc_receipt_ocr", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۱۹. اشتراک قالب شغلی: OCR رسیدهای هزینه و خلاصه‌سازی مالی", "19. Playbook Plan: Expense Receipt OCR & Summary",
            "استخراج متن رسیدهای اسکن‌شده و تولید گزارش خلاصهٔ هزینه‌ها",
            "Extracts text from scanned receipts offline and summarizes key expense items",
            3.50m, 210_000, 1500, 1),

        Plan(20, "plan.20.playbook_acc_dedup_rename", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۰. اشتراک قالب شغلی: حذف فاکتورهای تکراری و نام‌گذاری استاندارد", "20. Playbook Plan: Duplicate Invoice Cleanup & Standard Naming",
            "قرنطینهٔ فاکتورهای تکراری و اعمال پیشوند استاندارد حسابداری",
            "Quarantines duplicate invoices and applies standard accounting file names",
            3.50m, 210_000, 1500, 1),

        Plan(21, "plan.21.playbook_legal_gdpr_sanitize", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۱. اشتراک قالب شغلی: پاک‌سازی کامل اطلاعات هویتی (GDPR/PII) پرونده‌ها", "21. Playbook Plan: Legal GDPR/PII Sanitization & Metadata Scrub",
            "سانسور خودکار اطلاعات شخصی و متادیتای مخفی پیش از ارسال لوایح و قراردادها",
            "Redacts PII and strips hidden metadata from legal filings before sharing",
            3.99m, 235_000, 1500, 1),

        Plan(22, "plan.22.playbook_legal_contract_bundle", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۲. اشتراک قالب شغلی: ادغام پیوست‌های قرارداد در PDF و آرشیو امن", "22. Playbook Plan: Contract Appendix PDF Merge & Safe Zip",
            "ادغام تمام ضمایم قرارداد در یک فایل PDF استاندارد و ساخت بستهٔ ZIP",
            "Merges contract appendices into a single PDF and creates a verified ZIP package",
            3.50m, 210_000, 1500, 1),

        Plan(23, "plan.23.playbook_legal_discovery", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۳. اشتراک قالب شغلی: جست‌وجوی مفهومی در پرونده‌ها و استخراج تعهدات", "23. Playbook Plan: Legal Discovery Search & Obligation Summary",
            "یافتن بندهای تعهدآور در پرونده‌های حقوقی و تولید چک‌لیست تعهدات",
            "Searches legal dossiers offline and extracts key contractual obligations",
            3.99m, 235_000, 1500, 1),

        Plan(24, "plan.24.playbook_hr_blind_hiring", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۴. اشتراک قالب شغلی: ناشناس‌سازی رزومه‌ها برای استخدام عادلانه (HR)", "24. Playbook Plan: Candidate Resume Anonymizer (Blind Hiring)",
            "حذف شماره تماس و ایمیل از رزومه‌ها پیش از ارسال به مدیران فنی",
            "Redacts contact PII from candidate resumes before technical review",
            3.50m, 210_000, 1500, 1),

        Plan(25, "plan.25.playbook_hr_onboarding_portal", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۵. اشتراک قالب شغلی: تبدیل راهنماهای استخدامی به پورتال HTML", "25. Playbook Plan: Onboarding Guides to HTML Portal Pack",
            "تبدیل مستندات منابع انسانی به صفحات HTML مستقل دوجهته و بسته‌بندی ZIP",
            "Converts HR Markdown guides into standalone RTL/LTR HTML pages and zips them",
            3.50m, 210_000, 1500, 1),

        Plan(26, "plan.26.playbook_hr_downloads_cleanup", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۶. اشتراک قالب شغلی: مرتب‌سازی هوشمند پوشهٔ دانلودهای اداری", "26. Playbook Plan: Office Downloads Smart Cleanup & Dedup",
            "پاک‌سازی فایل‌های تکراری و دسته‌بندی خودکار فایل‌های اداری در ۶ پوشه",
            "Moves duplicates to staging and organizes office downloads into 6 folders",
            2.99m, 175_000, 1500, 1),

        Plan(27, "plan.27.playbook_content_photo_privacy", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۷. اشتراک قالب شغلی: حذف اطلاعات GPS/EXIF تصاویر پیش از انتشار در وب", "27. Playbook Plan: Photo EXIF/GPS Privacy Scrubber for Web",
            "پاک‌سازی مختصات مکانی دوربین از عکس‌های خبری و فروشگاهی و ساخت بستهٔ انتشار",
            "Strips GPS/EXIF camera segments from photos and bundles them for web publishing",
            3.50m, 210_000, 1500, 1),

        Plan(28, "plan.28.playbook_content_markdown_series", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۸. اشتراک قالب شغلی: تبدیل دسته‌ای مقالات Markdown به HTML", "28. Playbook Plan: Batch Markdown Articles to Styled HTML",
            "تبدیل پیش‌نویس‌های وبلاگ به صفحات HTML استاندارد با فونت مناسب و گزارش محتوا",
            "Converts Markdown drafts to styled HTML pages and generates a content inventory",
            3.50m, 210_000, 1500, 1),

        Plan(29, "plan.29.playbook_content_research_digest", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۲۹. اشتراک قالب شغلی: چکیده‌سازی یادداشت‌های پژوهشی و استخراج ایده‌ها", "29. Playbook Plan: Research Notes Digest & Action Items",
            "خلاصه‌سازی ده‌ها فایل پژوهشی و استخراج فهرست کارهای تولید محتوا",
            "Summarizes research notes offline and extracts content action items",
            3.50m, 210_000, 1500, 1),

        Plan(30, "plan.30.playbook_support_ticket_sanitize", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۳۰. اشتراک قالب شغلی: سانسور اطلاعات مشتریان در تیکت‌ها و خلاصه‌سازی", "30. Playbook Plan: Support Ticket PII Sanitizer & Issue Digest",
            "ماسک کردن شماره کارت/تلفن مشتریان در تیکت‌های پشتیبانی و استخراج خلاصهٔ مشکلات",
            "Redacts customer PII from support tickets and generates an issue digest",
            3.99m, 235_000, 1500, 1),

        Plan(31, "plan.31.playbook_support_crm_hygiene", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۳۱. اشتراک قالب شغلی: پاک‌سازی خروجی CSV باشگاه مشتریان (CRM)", "31. Playbook Plan: CRM Export CSV Hygiene & Safe Archive",
            "استانداردسازی اسامی و شماره‌های مشتریان در جدول CSV و آرشیو امن",
            "Normalizes customer CRM CSV exports and creates a verified backup archive",
            3.50m, 210_000, 1500, 1),

        Plan(32, "plan.32.playbook_support_kb_pdf_book", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۳۲. اشتراک قالب شغلی: تجمیع راهنماهای پشتیبانی در کتابچهٔ PDF", "32. Playbook Plan: Knowledge Base Compilation into Single PDF",
            "ادغام مقالات راهنمای مشتریان در یک کتابچهٔ PDF قابل دانلود و آفلاین",
            "Compiles support knowledge-base articles into a single offline PDF handbook",
            3.50m, 210_000, 1500, 1),

        Plan(33, "plan.33.playbook_it_log_sanitizer", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۳۳. اشتراک قالب شغلی: پاک‌سازی آدرس‌های IP و ایمیل از لاگ‌های سرور", "33. Playbook Plan: Server Log IP/Email Sanitizer & Zip Bundle",
            "ماسک کردن IPv4 و ایمیل‌ها در فایل‌های `.log` پیش از ارسال به شرکت‌های پشتیبان",
            "Redacts IPv4 addresses and emails from server logs and bundles them into a ZIP",
            3.99m, 235_000, 2000, 1),

        Plan(34, "plan.34.playbook_it_daily_backup", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۳۴. اشتراک قالب شغلی: پشتیبان‌گیری افزایشی روزانه از پروژه و گزارش فضا", "34. Playbook Plan: Daily Incremental Workspace Backup & Report",
            "بکاپ روزانهٔ فقط فایل‌های تغییریافتهٔ پروژه به همراه گزارش مصرف فضای دیسک",
            "Daily incremental backup of modified project files with storage breakdown report",
            3.50m, 210_000, 2000, 1),

        Plan(35, "plan.35.playbook_it_release_notes", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۳۵. اشتراک قالب شغلی: تولید یادداشت انتشار (Release Notes) و HTML", "35. Playbook Plan: Release Notes Summarizer & HTML Changelog",
            "خلاصه‌سازی تغییرات نسخهٔ نرم‌افزار و تولید صفحهٔ HTML مستقل Changelog",
            "Summarizes release notes and converts Markdown changelogs to standalone HTML",
            3.50m, 210_000, 1500, 1),

        Plan(36, "plan.36.playbook_it_incident_postmortem", SubscriptionCategory.SmbJobPlaybooks, BillingCycleKind.Monthly,
            "۳۶. اشتراک قالب شغلی: تحلیل گزارش‌های رخداد فنی (Post-Mortem)", "36. Playbook Plan: Incident Post-Mortem Search & Action Items",
            "جست‌وجو در گزارش‌های رخداد و استخراج چک‌لیست اقدام‌های پیشگیرانهٔ فنی",
            "Searches incident post-mortems and extracts preventative engineering action items",
            3.99m, 235_000, 1500, 1),

        // =====================================================================
        // Category 4: Hybrid AI Credits & Prompt/Voice Add-Ons (5 Models: #37 .. #41)
        // =====================================================================
        Plan(37, "plan.37.ai_credits_starter_50k", SubscriptionCategory.HybridAiAndPromptCredits, BillingCycleKind.CreditTopUp,
            "۳۷. بستهٔ شارژ ۵۰٬۰۰۰ توکن هوش مصنوعی ابری (بدون نیاز به API Key)", "37. 50,000 Cloud AI Planning Tokens (No API Key Needed)",
            "استفاده از برنامه‌ریز ابری با محافظ ناشناس‌ساز خودکار بدون درگیری با خرید API خارجی",
            "Prepaid cloud planning credits protected by LocalAnonymizerProxy — zero API setup",
            2.50m, 150_000, 500, 1),

        Plan(38, "plan.38.ai_credits_growth_250k", SubscriptionCategory.HybridAiAndPromptCredits, BillingCycleKind.CreditTopUp,
            "۳۸. بستهٔ شارژ ۲۵۰٬۰۰۰ توکن هوش مصنوعی ابری + پروکسی ناشناس‌ساز", "38. 250,000 Cloud AI Planning Tokens + Anonymizer Proxy",
            "مناسب کاربران پرمصرف برای برنامه‌ریزی تسک‌های سنگین با حفظ کامل حریم خصوصی",
            "High-volume prepaid cloud AI tokens with automatic path & PII anonymization",
            9.00m, 530_000, 2000, 1),

        Plan(39, "plan.39.ai_credits_pro_1m", SubscriptionCategory.HybridAiAndPromptCredits, BillingCycleKind.CreditTopUp,
            "۳۹. بستهٔ شارژ ۱٬۰۰۰٬۰۰۰ توکن هوش مصنوعی ابری + کش معنایی", "39. 1,000,000 Cloud AI Planning Tokens + Semantic Cache",
            "بستهٔ اقتصادی یک میلیون توکن همراه با کش معنایی محلی برای کاهش مصرف توکن",
            "1M prepaid cloud AI tokens paired with SemanticPlanCache for maximum efficiency",
            29.00m, 1_700_000, 5000, 3),

        Plan(40, "plan.40.prompt_coach_extension_pro", SubscriptionCategory.HybridAiAndPromptCredits, BillingCycleKind.Annual,
            "۴۰. اشتراک سالانهٔ مربی پرامپت‌ساز تعاملی + افزونهٔ مرورگر کروم/اج", "40. Interactive Prompt Coach & Browser Extension Pro (Annual)",
            "مهندسی پرامپت نامحدود در ۶ زبان همراه با افزونهٔ وب برای سانسور PII قبل از ارسال به ChatGPT",
            "Unlimited 6-language prompt engineering + Chrome/Edge PII Web Shield extension",
            19.00m, 1_120_000, 1000, 1),

        Plan(41, "plan.41.offline_voice_command_pack", SubscriptionCategory.HybridAiAndPromptCredits, BillingCycleKind.LifetimeOneTime,
            "۴۱. بستهٔ دائمی فرمان صوتی آفلاین و تبدیل گفتار ۶زبانه", "41. Offline Voice Command & Speech-to-Intent Pack (Lifetime)",
            "صدور دستورات صوتی Push-to-Talk با نرمال‌سازی خودکار اعداد و واژگان در ۶ زبان",
            "Lifetime access to OfflineVoiceCommandAdapter with 6-language spoken digit normalization",
            15.00m, 890_000, 2000, 1),

        // =====================================================================
        // Category 5: Team, B2B, Enterprise & White-Label Tiers (5 Models: #42 .. #46)
        // =====================================================================
        Plan(42, "plan.42.team_5_seats", SubscriptionCategory.TeamAndEnterprise, BillingCycleKind.Annual,
            "۴۲. اشتراک تیمی ۵ کاربره (مناسب استارتاپ‌ها و دفاتر کوچک)", "42. Team Plan — 5 Seats (Annual)",
            "۵ لایسنس کامل Pro + اشتراک‌گذاری قالب‌های `.agentflow.json` بین اعضای تیم",
            "5 full Pro seats + shared .agentflow.json workflow templates across your team",
            199.00m, 11_800_000, 5000, 5),

        Plan(43, "plan.43.business_15_seats", SubscriptionCategory.TeamAndEnterprise, BillingCycleKind.Annual,
            "۴۳. اشتراک شرکتی ۱۵ کاربره (ویژهٔ دفاتر حسابداری، حقوقی و بازرگانی)", "43. Business Office Plan — 15 Seats (Annual)",
            "۱۵ لایسنس کامل + پک‌های مغایرت‌گیری بانکی، قراردادهای دوزبانه و سانسور PII سازمانی",
            "15 Pro seats + Bank Reconciliation, Bilingual Legal Builder & PII Compliance",
            449.00m, 26_500_000, 10000, 15),

        Plan(44, "plan.44.enterprise_50_seats", SubscriptionCategory.TeamAndEnterprise, BillingCycleKind.Annual,
            "۴۴. نسخهٔ سازمانی ۵۰ کاربره با قفل آفلاین و زنجیرهٔ هش حسابرسی", "44. Enterprise Compliance Edition — 50 Seats (Annual)",
            "شامل سیاست سازمانی امضاشده (HMAC-SHA256)، قفل ForceLocalOnlyMode و لاگ زنجیرهٔ هش",
            "50 seats + Signed EnterprisePolicy, Strict Air-Gapped Lock & Tamper-Evident Audit Chain",
            1190.00m, 69_000_000, 20000, 50),

        Plan(45, "plan.45.enterprise_unlimited_site", SubscriptionCategory.TeamAndEnterprise, BillingCycleKind.Annual,
            "۴۵. لایسنس نامحدود درون‌سازمانی (Enterprise Site License — Air-Gapped)", "45. Enterprise Unlimited Site License (Air-Gapped On-Prem)",
            "نصب نامحدود روی تمام سیستم‌های یک سازمان با تضمین ۱۰۰٪ آفلاین و بدون ارتباط اینترنتی",
            "Unlimited seats across a single organization with strict air-gapped offline guarantee",
            2900.00m, 170_000_000, 50000, 999),

        Plan(46, "plan.46.white_label_oem_partner", SubscriptionCategory.TeamAndEnterprise, BillingCycleKind.Annual,
            "۴۶. لایسنس نمایندگی فروش و برندینگ اختصاصی (White-Label / Agency Partner)", "46. White-Label & Agency Reseller Partner License",
            "امکان عرضهٔ نرم‌افزار و قالب‌های شغلی با نام تجاری و لوگوی شرکت شما + پنل صدور لایسنس",
            "Rebrand and resell workflows & licenses under your own agency brand + Seller Key CLI",
            1490.00m, 88_000_000, 50000, 250)
    ];

    private static SubscriptionPlanModel Plan(
        int number,
        string skuId,
        SubscriptionCategory category,
        BillingCycleKind cycle,
        string titleFa,
        string titleEn,
        string descFa,
        string descEn,
        decimal priceUsd,
        long priceToman,
        int maxFiles,
        int seats) =>
        new(
            PlanNumber: number,
            SkuId: skuId,
            Category: category,
            BillingCycle: cycle,
            TitleFa: titleFa,
            TitleEn: titleEn,
            DescriptionFa: descFa,
            DescriptionEn: descEn,
            PriceUsd: priceUsd,
            PriceToman: priceToman,
            MaxFilesPerBatch: maxFiles,
            IncludedSeatCount: seats,
            IncludedHighlightsFa: [descFa, $"سقف پردازش هر نوبت: {maxFiles:N0} فایل", $"تعداد کاربر مجاز: {seats} نشست"],
            IncludedHighlightsEn: [descEn, $"Batch capacity: {maxFiles:N0} files", $"Included seats: {seats}"]);
}
