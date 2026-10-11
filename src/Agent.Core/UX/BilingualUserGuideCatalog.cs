namespace Agent.Core.UX;

public sealed record UserGuideSection(
    string SectionId,
    int Order,
    string BadgeIcon,
    string TitleFa,
    string TitleEn,
    string SummaryFa,
    string SummaryEn,
    IReadOnlyList<string> StepsFa,
    IReadOnlyList<string> StepsEn,
    string ProTipFa,
    string ProTipEn)
{
    public string GetTitle(string lang) =>
        string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? TitleEn : TitleFa;

    public string GetSummary(string lang) =>
        string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? SummaryEn : SummaryFa;

    public IReadOnlyList<string> GetSteps(string lang) =>
        string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? StepsEn : StepsFa;

    public string GetProTip(string lang) =>
        string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? ProTipEn : ProTipFa;
}

/// <summary>
/// Complete Bilingual (Persian &amp; English) User Guide &amp; Interactive Tutorial Catalog
/// covering all 21 skills, 20 SMB playbooks, 200 AI tools, Prompt Coach, Drop-Zone,
/// Privacy/Undo, and Pro/Monetization features.
/// </summary>
public static class BilingualUserGuideCatalog
{
    public static IReadOnlyList<UserGuideSection> AllSections { get; } =
    [
        new(
            SectionId: "guide.quickstart",
            Order: 1,
            BadgeIcon: "🚀",
            TitleFa: "۱. شروع سریع و فلسفهٔ پیش‌نمایش و واگرد (Preview & Undo)",
            TitleEn: "1. Quick Start & Mandatory Preview / 1-Click Undo",
            SummaryFa: "رباتیک ایجنت هیچ فایلی را بدون اجازهٔ شما تغییر نمی‌دهد. هر دستور ابتدا یک «پیش‌نمایش دقیق» می‌سازد و پس از اجرا نیز با یک کلیک قابل بازگشت (Undo) است.",
            SummaryEn: "Robotic Agent never modifies files without your explicit consent. Every command generates an exact preview first and supports 1-click transactional Undo after execution.",
            StepsFa:
            [
                "گام ۱: از منوی بالای پنجرهٔ اصلی، زبان دلخواه خود (فارسی، انگلیسی، چینی، اسپانیایی، عربی یا فرانسوی) را انتخاب کنید.",
                "گام ۲: پوشهٔ مبدأ و پوشهٔ مقصد را انتخاب کنید یا پوشه را مستقیماً به داخل پنجره بکشید و رها کنید (Drag & Drop).",
                "گام ۳: درخواست خود را به زبان ساده بنویسید (مثلاً: «فایل‌های PDF بزرگ‌تر از ۱ مگابایت در زیرپوشه‌ها را کپی کن»).",
                "گام ۴: روی «بررسی و پیش‌نمایش» کلیک کنید، فهرست تغییرات را ببینید و سپس «تأیید و اجرا» را بزنید.",
                "گام ۵: در صورت پشیمانی، کافی است روی دکمهٔ «واگرد آخرین عملیات (Undo)» کلیک کنید تا همه‌چیز با بررسی هش SHA-256 به حالت اول برگردد."
            ],
            StepsEn:
            [
                "Step 1: Choose your preferred UI language (Persian, English, Chinese, Spanish, Arabic, or French) from the top bar.",
                "Step 2: Select your Source and Destination folders, or simply Drag & Drop a folder onto the window.",
                "Step 3: Type your request in plain language (e.g., 'Copy PDF files larger than 1 MB including subfolders').",
                "Step 4: Click 'Preview', inspect the exact candidate files and disk space, then click 'Approve & Run'.",
                "Step 5: Need to revert? Click 'Undo Last Operation' to restore every file verified by SHA-256 digest."
            ],
            ProTipFa: "نکتهٔ حرفه‌ای: با کلید میان‌بر Ctrl+K می‌توانید پالت فرمان سریع را باز کنید و در میان ۲۴۰ قابلیت و ابزار جست‌وجو کنید.",
            ProTipEn: "Pro Tip: Press Ctrl+K anytime to open the instant Command Palette across all 240 skills, playbooks, and AI tools."),

        new(
            SectionId: "guide.file_skills",
            Order: 2,
            BadgeIcon: "📁",
            TitleFa: "۲. مهارت‌های هوشمند مدیریت فایل، مرتب‌سازی و پشتیبان‌گیری",
            TitleEn: "2. Smart File Management, Organization & Incremental Backup",
            SummaryFa: "۶ مهارت تخصصی برای رهایی از شلوغی پوشهٔ Downloads و Desktop بدون خطر حذف شدن فایل‌ها.",
            SummaryEn: "6 specialized file skills to declutter Downloads and Desktop folders safely without risking data loss.",
            StepsFa:
            [
                "کپی هوشمند با فیلتر چندگانه (file.copy): فیلتر هم‌زمان چند پسوند، بازهٔ حجم، تاریخ و عمق زیرپوشه تا ۱۰ سطح.",
                "تغییر نام دسته‌ای (file.rename): افزودن پیشوند، پسوند، شمارهٔ ردیف و تاریخ به صدها فایل با جدول پیش‌نمایش «نام قدیم ← نام جدید».",
                "مرتب‌سازی خودکار پوشه (file.organize): دسته‌بندی فایل‌های شلوغ در ۶ پوشهٔ استاندارد (Documents, Images, Videos, Audio, Archives, Code).",
                "یافتن فایل‌های تکراری (file.deduplicate): مقایسهٔ دو مرحله‌ای (حجم + هش SHA-256) و انتقال امن نسخه‌های تکراری به پوشهٔ قرنطینه.",
                "آرشیو امن ZIP (file.archive): فشرده‌سازی و استخراج با محافظت کامل در برابر حملات Zip-Slip و Zip-Bomb.",
                "پشتیبان‌گیری افزایشی (file.backup): کپی کردن فقط فایل‌های جدید یا تغییریافته برای صرفه‌جویی در زمان و فضای دیسک."
            ],
            StepsEn:
            [
                "Multi-Filter File Copy (file.copy): Filter by multiple extensions, size range, date, and subfolder depth up to 10 levels.",
                "Smart Batch Rename (file.rename): Add prefixes, suffixes, counters, and dates with an Old -> New preview table.",
                "Auto-Organize Folder (file.organize): Sort cluttered files into 6 standard folders (Documents, Images, Videos, Audio, Archives, Code).",
                "Duplicate Finder (file.deduplicate): Two-stage size + SHA-256 comparison that moves duplicates safely to a staging folder.",
                "Safe ZIP Archive (file.archive): Compress and extract ZIP archives with built-in Zip-Slip and Zip-Bomb protection.",
                "Incremental Backup (file.backup): Copy only new or modified files to save time and storage."
            ],
            ProTipFa: "نکتهٔ امنیتی: فایل‌های دارای پسوند دوگانهٔ مشکوک (مانند invoice.pdf.exe) به‌طور خودکار شناسایی و مسدود می‌شوند.",
            ProTipEn: "Security Tip: Deceptive double extensions (such as invoice.pdf.exe) are automatically detected and blocked."),

        new(
            SectionId: "guide.document_privacy",
            Order: 3,
            BadgeIcon: "🛡️",
            TitleFa: "۳. پردازش آفلاین اسناد (PDF/CSV/OCR) و سانسور اطلاعات محرمانه (PII)",
            TitleEn: "3. Offline Document Processing (PDF/CSV/OCR) & PII Redaction",
            SummaryFa: "۱۳ مهارت کاملاً آفلاین برای استخراج فاکتور، تمیزکاری اکسل/CSV، مدیریت PDF، کاهش حجم مدارک و حذف اطلاعات حساس پیش از اشتراک‌گذاری.",
            SummaryEn: "13 completely offline document skills for invoice extraction, CSV cleaning, PDF merging, scan optimization, and PII scrubbing.",
            StepsFa:
            [
                "سانسور اطلاعات محرمانه (doc.redact_pii): شناسایی ایمیل، تلفن، شبا (IBAN) و شمارهٔ کارت بانکی (با الگوریتم Luhn). فایل اصلی شما ۱۰۰٪ دست‌نخورده می‌ماند و نسخهٔ پاک‌سازی‌شده با پسوند _sanitized ذخیره می‌شود.",
                "حذف متادیتای مخفی عکس و سند (doc.scrub_metadata): حذف مختصات GPS و سریال دوربین از عکس‌های JPEG در نسخهٔ _sanitized.",
                "استخراج فاکتور به اکسل/CSV (doc.invoice_extract): استخراج شماره فاکتور، تاریخ، فروشنده، مالیات و مبلغ کل در ۶ زبان.",
                "تمیزکاری جدول CSV (doc.csv_clean): حذف ردیف‌های خالی و تکراری و یکسان‌سازی حروف ی/ک و ارقام فارسی/عربی.",
                "جعبهٔ ابزار PDF و بهینه‌سازی مدارک (doc.pdf_toolkit و doc.media_optimize): تبدیل عکس‌های موبایل به یک PDF یکپارچه و ادغام یا استخراج متن PDF بدون اینترنت.",
                "جست‌وجوی معنایی، خلاصه‌سازی و مقایسهٔ دو قرارداد (doc.search, doc.summarize, doc.diff): یافتن اسناد بر اساس مفهوم، استخراج چک‌لیست اقدام‌ها و مقایسهٔ خط‌به‌خط دو نسخه از قرارداد."
            ],
            StepsEn:
            [
                "PII Redaction (doc.redact_pii): Masks emails, phones, IBANs, and Luhn-verified card numbers into a _sanitized copy while keeping your original file 100% untouched.",
                "EXIF & Metadata Scrubber (doc.scrub_metadata): Strips GPS coordinates and camera serials from JPEG photos into _sanitized copies.",
                "Invoice to CSV Extractor (doc.invoice_extract): Extracts invoice number, date, vendor, tax, and total across 6 languages.",
                "CSV Cleaner (doc.csv_clean): Removes empty/duplicate rows and normalizes multilingual digits and characters.",
                "PDF Toolkit & Scan Optimizer (doc.pdf_toolkit & doc.media_optimize): Combines mobile photos into a single PDF and merges/extracts PDF text offline.",
                "Local Semantic Search, Summarizer & Diff (doc.search, doc.summarize, doc.diff): Search inside documents offline, extract action items, and compare contract versions."
            ],
            ProTipFa: "نکتهٔ طلایی: پیش از آپلود هر قرارداد یا لاگ سرور در ابزارهای هوش مصنوعی اینترنتی، ابتدا با مهارت doc.redact_pii نسخهٔ _sanitized آن را بسازید.",
            ProTipEn: "Golden Rule: Always create a _sanitized copy with doc.redact_pii before uploading contracts or logs to any cloud AI website."),

        new(
            SectionId: "guide.workflows_playbooks",
            Order: 4,
            BadgeIcon: "⚡",
            TitleFa: "۴. موتور گردش‌کار چندمرحله‌ای و ۲۰ قالب آمادهٔ شغلی (SMB Playbooks)",
            TitleEn: "4. Multi-Step Workflow Engine & 20 Ready-to-Use SMB Playbooks",
            SummaryFa: "به جای اجرای تک‌به‌تک مهارت‌ها، زنجیره‌ای از کارها را با یک کلیک، با نگهبان پوشه یا بر اساس زمان‌بندی روزانه/هفتگی اجرا کنید.",
            SummaryEn: "Chain multiple skills together with 1-click SMB Playbooks, live Folder Watchers, or automated daily/weekly schedules.",
            StepsFa:
            [
                "انتخاب قالب آماده (Playbook): از میان ۲۰ قالب شغلی (حسابداری، حقوقی، منابع انسانی، تولید محتوا، پشتیبانی و IT) قالب دلخواه را انتخاب کنید.",
                "پیش‌نمایش سرتاسری (End-to-End Dry-Run): پیش از اجرای گردش‌کار، خروجی و تعداد فایل‌های تمام گام‌ها به صورت یکجا شبیه‌سازی و نمایش داده می‌شود.",
                "واگرد زنجیره‌ای (Transactional Rollback): اگر در گام سوم خطایی رخ دهد یا دکمهٔ واگرد را بزنید، گام‌های ۳، ۲ و ۱ به ترتیب معکوس به حالت اول بازگردانده می‌شوند.",
                "اشتراک‌گذاری گردش‌کار (.agentflow.json): گردش‌کارهای سفارشی خود را بدون افشای مسیرهای شخصی ذخیره و با همکاران به اشتراک بگذارید."
            ],
            StepsEn:
            [
                "Pick a Playbook: Choose from 20 built-in SMB Playbooks across Accounting, Legal, HR, Content, Support, and IT.",
                "End-to-End Dry-Run Preview: Simulate every step in the pipeline and inspect resolved variables before any file is touched.",
                "Reverse-Order Transactional Rollback: If step N fails or you click Undo, steps are rolled back in reverse order (N down to 1).",
                "Portable Sharing (.agentflow.json): Export and import custom workflows validated against hardcoded path injection."
            ],
            ProTipFa: "مثال کاربردی حسابداری: قالب «پایپ‌لاین فاکتورهای ماهانه» به صورت خودکار فاکتورها را استخراج می‌کند، جدول CSV را تمیز می‌کند و خروجی را در یک فایل ZIP امن بسته‌بندی می‌نماید.",
            ProTipEn: "Accounting Example: The 'Monthly Invoice Pipeline' playbook extracts invoices to CSV, deduplicates rows, and bundles the results into a verified ZIP."),

        new(
            SectionId: "guide.ai_hub_prompt_coach",
            Order: 5,
            BadgeIcon: "🤖",
            TitleFa: "۵. هاب ۲۰۰ ابزار هوش مصنوعی، مقایسه‌گر و «پرامپت‌ساز تعاملی»",
            TitleEn: "5. 200 AI Tools Hub, Side-by-Side Comparison & Interactive Prompt Coach",
            SummaryFa: "دسترسی به ۲۰۰ ابزار برتر هوش مصنوعی دنیا در ۸ دسته، همراه با پیشنهاددهندهٔ هوشمند، جدول مقایسه و مربی نوشتن پرامپت حرفه‌ای.",
            SummaryEn: "Explore 200 curated AI tools across 8 categories with smart problem matchmaking, side-by-side comparison, and an Interactive Prompt Coach.",
            StepsFa:
            [
                "پیشنهاد هوشمند ابزار (AI Matchmaker): مشکل خود را بنویسید (مثلاً «می‌خواهم صدای جلسه را به متن تبدیل کنم») تا بهترین ابزارهای رایگان و تخصصی به شما پیشنهاد شوند.",
                "مقایسهٔ رودرروی ابزارها: ۲ تا ۴ ابزار را تیک بزنید تا جدول مقایسهٔ پلن رایگان، وضعیت حریم خصوصی، پشتیبانی از زبان فارسی و نیاز به تحریم‌شکن را ببینید.",
                "مربی پرامپت‌ساز تعاملی (Interactive Prompt Coach): هدف خود را به زبان ساده بنویسید؛ برنامه اطلاعات محرمانهٔ آن را با LocalAnonymizerProxy حذف می‌کند و یک پرامپت مهندسی‌شدهٔ کامل تحویل می‌دهد.",
                "افزودن ابزار شخصی و علاقه‌مندی‌ها: ابزارهای محبوب خود را ستاره‌دار کنید یا ابزارهای سازمانی خود را به کاتالوگ اضافه نمایید."
            ],
            StepsEn:
            [
                "AI Tool Matchmaker: Describe your task in plain words to get ranked tool recommendations with privacy and free-tier badges.",
                "Side-by-Side Matrix: Select 2 to 4 tools to compare free tiers, model training policies,离线 capability, and regional access.",
                "Interactive Prompt Coach: Enter your goal in simple words; the coach scrubs any accidental PII and generates a structured expert prompt.",
                "Favorites & Custom Tools: Star your favorite tools or add your team's internal AI endpoints to the local catalog."
            ],
            ProTipFa: "حفاظت در حالت Hybrid: هر زمان از برنامه‌ریز ابری (BYOK) استفاده کنید، ابتدا پنجرهٔ شفافیت (HybridConsentGate) متن ناشناس‌شده را به شما نشان می‌دهد.",
            ProTipEn: "Hybrid Privacy Shield: Before any cloud planner call, HybridConsentGate shows you the exact anonymized payload and token/cost estimate."),

        new(
            SectionId: "guide.dropzone_context_menu",
            Order: 6,
            BadgeIcon: "✨",
            TitleFa: "۶. ساده‌سازی روزمره: منوی کلیک‌راست ویندوز، حباب Drag & Drop و کلید اضطراری",
            TitleEn: "6. Everyday Simplicity: Explorer Right-Click Menu, Magic Drop-Zone & Kill-Switch",
            SummaryFa: "دسترسی فوری به قابلیت‌های برنامه بدون نیاز به تایپ دستور، همراه با کنترل کامل انسانی روی اتوماسیون دسکتاپ.",
            SummaryEn: "Instant access from Windows Explorer right-click menus and drag-and-drop, backed by immediate human-agency safety guards.",
            StepsFa:
            [
                "حباب هوشمند Drag & Drop: کافی است عکس، PDF یا جدول CSV را بکشید و رها کنید؛ برنامه بر اساس پسوند فایل، ۳ اقدام مناسب را فوراً پیشنهاد می‌دهد.",
                "ادغام در کلیک‌راست ویندوز: امکان افزودن گزینهٔ «انجام با رباتیک ایجنت» و «پاک‌سازی اطلاعات محرمانه» به منوی کلیک‌راست پوشه‌ها و فایل‌ها بدون نیاز به دسترسی Administrator.",
                "کلید توقف اضطراری (Kill-Switch) و محافظ حرکت ماوس: در حین اتوماسیون دسکتاپ، به محض اینکه ماوس خود را حرکت دهید یا کلیدی را فشار دهید، اتوماسیون فوراً متوقف می‌شود."
            ],
            StepsEn:
            [
                "Magic Drop-Zone: Drag & drop photos, PDFs, or CSVs; the analyzer automatically suggests the top 3 actions for that file type.",
                "Windows Explorer Context Menu: Register user-scope right-click actions ('Run with Robotic Agent' & 'Sanitize PII/EXIF') without admin rights.",
                "Kill-Switch & Input Interference Guard: Moving your physical mouse or pressing a key immediately pauses desktop UI automation."
            ],
            ProTipFa: "ابزار QR آفلاین: می‌توانید برای وای‌فای دفتر یا کارت ویزیت خود، کد گرافیکی SVG را به‌صورت ۱۰۰٪ آفلاین تولید کنید.",
            ProTipEn: "Offline QR Generator: Generate standalone vector SVG matrix codes for Wi-Fi networks or vCard business cards 100% offline."),

        new(
            SectionId: "guide.monetization_roi",
            Order: 7,
            BadgeIcon: "🏆",
            TitleFa: "۷. داشبورد بازگشت سرمایه (ROI)، کارت بهره‌وری و سطوح کاربری (Free / Pro / Lifetime)",
            TitleEn: "7. ROI Time-Saved Dashboard, Viral Impact Card & Pro / Lifetime Licensing",
            SummaryFa: "مشاهدهٔ دقیق ساعت‌ها و هزینهٔ ذخیره‌شده، ساخت کارت گرافیکی قابل اشتراک‌گذاری و مدیریت لایسنس حرفه‌ای.",
            SummaryEn: "Track hours and dollars saved locally, generate shareable SVG productivity cards, and manage Pro/Lifetime licenses.",
            StepsFa:
            [
                "داشبورد ROI محلی: محاسبهٔ خودکار تعداد فایل‌های پردازش‌شده، ساعت‌های کار اداری صرفه‌جویی‌شده و معادل دلاری آن بر اساس تاریخچهٔ زنجیرهٔ هش.",
                "کارت گرافیکی بهره‌وری (Viral Impact Card): تولید کارت تصویری دوزبانه (SVG) از کارنامهٔ صرفه‌جویی شما برای اشتراک‌گذاری در لینکدین و شبکه‌های اجتماعی.",
                "فعال‌سازی آفلاین لایسنس (Pro / Lifetime Deal / Enterprise): پشتیبانی از کلیدهای لایسنس امضاشده (RA-PRO / RA-LTD / RA-ENT) بدون نیاز به ارتباط دائمی با سرور."
            ],
            StepsEn:
            [
                "Local ROI Dashboard: Automatically calculates total files automated, administrative hours saved, and USD value saved.",
                "Shareable Viral Impact Card: Generates a sleek bilingual SVG report card ready to share on LinkedIn, Telegram, or X.",
                "Offline License Activation (Pro / LTD / Enterprise): Activate signed RA-PRO, RA-LTD, or RA-ENT license keys offline."
            ],
            ProTipFa: "نسخهٔ خط فرمان (agent-cli): کاربران حرفه‌ای و مدیران سیستم می‌توانند تمام مهارت‌ها و عیب‌یابی را از طریق خط فرمان در ویندوز، لینوکس و مک اجرا کنند.",
            ProTipEn: "Cross-Platform CLI (agent-cli): Power users can run skills, playbooks, and diagnostics from the terminal on Windows, Linux, and macOS.")
    ];

    public static IReadOnlyList<UserGuideSection> SearchSections(string? query, string languageCode = "fa")
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return AllSections;
        }

        var q = query.Trim();
        return AllSections
            .Where(s =>
                s.TitleFa.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.TitleEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.SummaryFa.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.SummaryEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.StepsFa.Any(step => step.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                s.StepsEn.Any(step => step.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }
}
