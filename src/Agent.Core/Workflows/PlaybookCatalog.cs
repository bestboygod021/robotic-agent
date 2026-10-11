using Agent.Core.Skills;
using Agent.Core.Skills.Documents;

namespace Agent.Core.Workflows;

/// <summary>
/// Library of 20 ready-made SMB Playbooks (#47) across Accounting, HR, Legal,
/// Content, Customer Support, and IT/DevOps in all 6 supported languages.
/// </summary>
public static class PlaybookCatalog
{
    public static IReadOnlyList<WorkflowDefinition> All { get; } = CreatePlaybooks();

    public static WorkflowDefinition GetById(string playbookId) =>
        All.FirstOrDefault(p => string.Equals(p.Id, playbookId, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Playbook '{playbookId}' not found.");

    private static IReadOnlyList<WorkflowDefinition> CreatePlaybooks() =>
    [
        // 1. Accounting: Invoice OCR -> Extract CSV -> Clean CSV -> Archive
        Create(
            id: "playbook.accounting.invoice_pipeline",
            category: "Accounting",
            titles: L("پایپ‌لاین خودکار فاکتورهای ماهانه به اکسل/CSV", "Monthly Invoice Extraction & Clean CSV Pipeline", "月度发票提取与清洗流水线", "Pipeline Mensual de Facturas a CSV Limpio", "خط معالجة الفواتير الشهرية إلى CSV نظيف", "Pipeline Mensuel d’Extraction et Nettoyage de Factures"),
            descriptions: L("استخراج خودکار اطلاعات فاکتورها، پاک‌سازی ردیف‌های تکراری در CSV و آرشیو امن اسناد", "Extracts invoice fields to CSV, deduplicates rows, and archives original files", "自动提取发票字段至 CSV、去重并安全归档", "Extrae datos de facturas a CSV, elimina duplicados y archiva", "يستخرج بيانات الفواتير إلى CSV ويزيل التكرار ويؤرشف الملفات", "Extrait les factures en CSV, déduplique et archive"),
            steps:
            [
                Step("s1", InvoiceExtractSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputCsvPath"] = "${outputDir}/invoices_raw.csv", ["extension"] = "txt" }),
                Step("s2", CsvCleanerSkill.SkillId, new() { ["sourceFilePath"] = "${prev.OutputPath}", ["outputFilePath"] = "${outputDir}/invoices_clean.csv" }),
                Step("s3", SafeArchiveSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["archiveFilePath"] = "${outputDir}/accounting_bundle.zip" }, new(WorkflowConditionKind.PreviousCountAtLeast, 1))
            ]),

        // 2. Accounting: Quarterly Tax Folder Audit & Backup
        Create(
            id: "playbook.accounting.tax_audit_backup",
            category: "Accounting",
            titles: L("ممیزی پوشهٔ مالیاتی و پشتیبان‌گیری افزایشی", "Tax Folder Audit Report & Incremental Backup", "税务文件夹审计报告与增量备份", "Auditoría de Carpeta Fiscal y Respaldo Incremental", "تدقيق مجلد الضرائب والنسخ الاحتياطي التزايدي", "Audit du Dossier Fiscal et Sauvegarde Incrémentale"),
            descriptions: L("تولید گزارش تحلیلی از اسناد مالی و تهیهٔ نسخهٔ پشتیبان افزایشی", "Generates an inventory report of tax records and runs incremental backup", "生成财务文档统计报告并执行增量备份", "Genera informe de inventario fiscal y copia incremental", "ينشئ تقرير جرد للمستندات المالية ويجري نسخاً احتياطياً تزايدياً", "Génère un rapport d’inventaire fiscal et une sauvegarde incrémentale"),
            steps:
            [
                Step("s1", FolderReportSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputReportPath"] = "${outputDir}/tax_inventory.md" }),
                Step("s2", IncrementalBackupSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}" })
            ]),

        // 3. Accounting: Expense Receipt OCR & Summary
        Create(
            id: "playbook.accounting.receipt_ocr_summary",
            category: "Accounting",
            titles: L("استخراج متن رسیدهای هزینه (OCR) و خلاصه‌سازی", "Expense Receipt OCR & Executive Summary", "报销单据 OCR 提取与汇总", "OCR de Recibos de Gastos y Resumen", "استخراج نصوص إيصالات المصروفات وتلخيصها", "OCR des Reçus de Dépenses et Résumé"),
            descriptions: L("استخراج لایهٔ متنی رسیدها و تولید چکیدهٔ هزینه‌ها", "Extracts text from receipts offline and summarizes key items", "离线提取收据文本并生成要点摘要", "Extrae texto de recibos sin conexión y resume puntos clave", "يستخرج نصوص الإيصالات محلياً ويلخص البنود الرئيسية", "Extrait le texte des reçus hors-ligne et résume les points clés"),
            steps:
            [
                Step("s1", OfflineOcrSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "pdf" }),
                Step("s2", BatchSummarizeSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputSummaryPath"] = "${outputDir}/receipts_summary.md", ["extension"] = "txt" })
            ]),

        // 4. Accounting: Duplicate Invoice Cleanup & Rename
        Create(
            id: "playbook.accounting.dedup_rename",
            category: "Accounting",
            titles: L("حذف فاکتورهای تکراری و نام‌گذاری استاندارد", "Duplicate Invoice Cleanup & Standard Naming", "重复发票清理与标准重命名", "Limpieza de Facturas Duplicadas y Renombrado", "إزالة الفواتير المكررة وإعادة التسمية القياسية", "Suppression des Factures en Double et Renommage"),
            descriptions: L("شناسایی فایل‌های تکراری بر اساس SHA-256 و نام‌گذاری یکپارچهٔ فاکتورها", "Quarantines byte-identical duplicates and applies standard prefixes", "基于 SHA-256 隔离重复文件并统一命名", "Aísla duplicados exactos y aplica prefijos estándar", "يعزل الفواتير المكررة ويطبق تسمية موحدة", "Isole les doublons SHA-256 et applique un préfixe standard"),
            steps:
            [
                Step("s1", DuplicateFinderSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}" }),
                Step("s2", FileCopySkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "*" })
            ]),

        // 5. Legal & Compliance: GDPR/PII Sanitization & Metadata Scrubbing
        Create(
            id: "playbook.legal.gdpr_sanitization",
            category: "Legal",
            titles: L("پاک‌سازی کامل اطلاعات هویتی (PII) و فرادادهٔ مخفی", "Full GDPR PII Redaction & Metadata Scrubber", "GDPR 个人敏感信息脱敏与元数据清除", "Redacción Completa de PII y Limpieza de Metadatos", "إخفاء البيانات الشخصية وإزالة البيانات الوصفية المخفية", "Anonymisation Complète PII et Nettoyage de Métadonnées"),
            descriptions: L("ماسک کردن اطلاعات محرمانه و حذف متادیتای مخفی پیش از ارسال اسناد به خارج از سازمان", "Redacts PII and strips hidden metadata into _sanitized copies", "脱敏敏感信息并清除隐藏元数据，生成安全副本", "Oculta PII y elimina metadatos ocultos en copias seguras", "يحجب البيانات الحساسة ويزيل البيانات الوصفية في نسخ آمنة", "Masque les données personnelles et supprime les métadonnées cachées"),
            steps:
            [
                Step("s1", PiiRedactionSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "txt" }),
                Step("s2", MetadataScrubberSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "md" })
            ]),

        // 6. Legal & Compliance: Contract Archival & PDF Bundle
        Create(
            id: "playbook.legal.contract_bundle",
            category: "Legal",
            titles: L("ادغام پیوست‌های قرارداد در PDF و آرشیو امن", "Contract Appendix PDF Merge & Safe Zip", "合同附件 PDF 合并与安全归档", "Fusión PDF de Anexos de Contrato y Archivo Seguro", "دمج ملاحق العقود في PDF وأرشفتها بأمان", "Fusion PDF des Annexes de Contrat et Archivage"),
            descriptions: L("ادغام اسناد قرارداد در یک فایل PDF یکپارچه و فشرده‌سازی امن", "Merges contract files into a single PDF and creates a verified ZIP archive", "将合同文件合并为单一 PDF 并创建安全压缩包", "Une documentos contractuales en un PDF y crea un ZIP seguro", "يدمج ملفات العقد في PDF واحد وينشئ أرشيف ZIP آمن", "Fusionne les contrats en un PDF unique et crée une archive ZIP"),
            steps:
            [
                Step("s1", PdfToolkitSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputFilePath"] = "${outputDir}/merged_contract.pdf" }),
                Step("s2", SafeArchiveSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["archiveFilePath"] = "${outputDir}/contract_package.zip" })
            ]),

        // 7. Legal: Legal Discovery Search & Summary
        Create(
            id: "playbook.legal.discovery_search_summary",
            category: "Legal",
            titles: L("جست‌وجوی مفهومی در پرونده‌ها و استخراج تعهدات", "Legal Discovery Search & Obligation Summary", "法律卷宗语义检索与义务摘要", "Búsqueda Legal y Resumen de Obligaciones", "البحث الدلالي في الملفات القانونية وتلخيص الالتزامات", "Recherche Juridique et Résumé des Obligations"),
            descriptions: L("یافتن بندهای مرتبط در پرونده‌ها و استخراج خلاصه و اقدام‌های حقوقی", "Searches legal documents offline and extracts key obligations and action items", "本地检索法律文档并提取核心义务与待办事项", "Busca en documentos legales y extrae obligaciones clave", "يبحث في المستندات القانونية محلياً ويستخرج الالتزامات والمهام", "Recherche dans les dossiers juridiques et extrait les obligations"),
            steps:
            [
                Step("s1", LocalSemanticSearchSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["query"] = "قرارداد تعهد", ["extension"] = "txt" }),
                Step("s2", BatchSummarizeSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputSummaryPath"] = "${outputDir}/legal_obligations.md", ["extension"] = "txt" })
            ]),

        // 8. HR & Admin: Candidate Resume Anonymizer (Blind Hiring)
        Create(
            id: "playbook.hr.blind_hiring_sanitizer",
            category: "HR",
            titles: L("ناشناس‌سازی رزومه‌ها برای استخدام عادلانه (Blind Hiring)", "Candidate Resume Anonymizer for Blind Hiring", "候选人简历匿名化（公平招聘）", "Anonimizador de Currículums para Selección Imparcial", "إخفاء هوية السير الذاتية للتوظيف العادل", "Anonymisation de CV pour Recrutement Équitable"),
            descriptions: L("حذف ایمیل، تلفن و مشخصات تماس از رزومه‌ها پیش از ارسال به مدیران فنی", "Redacts contact PII from resumes before technical evaluation", "在技术评审前隐去简历中的邮箱与电话等个人信息", "Oculta datos de contacto de los CV antes de la evaluación técnica", "يحجب بيانات الاتصال من السير الذاتية قبل التقييم الفني", "Masque les coordonnées des CV avant l’évaluation technique"),
            steps:
            [
                Step("s1", PiiRedactionSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "txt" }),
                Step("s2", FolderReportSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["outputReportPath"] = "${outputDir}/candidate_batch_report.md" })
            ]),

        // 9. HR & Admin: Employee Onboarding Pack Backup & HTML Portal
        Create(
            id: "playbook.hr.onboarding_portal_pack",
            category: "HR",
            titles: L("تبدیل راهنماهای استخدامی به پورتال HTML و آرشیو", "Onboarding Guides to HTML Portal & Archive", "入职指南转 HTML 门户与归档", "Guías de Incorporación a Portal HTML y Archivo", "تحويل أدلة التوظيف إلى بوابة HTML وأرشفتها", "Conversion des Guides d’Intégration en Portail HTML"),
            descriptions: L("تبدیل مستندات Markdown منابع انسانی به صفحات HTML مستقل دوجهته و بسته‌بندی ZIP", "Converts HR Markdown guides into standalone RTL/LTR HTML pages and zips them", "将 HR Markdown 指南转换为独立 HTML 页面并打包", "Convierte guías Markdown de RR. HH. en HTML independiente y las comprime", "يحول أدلة الموارد البشرية إلى صفحات HTML مستقلة ويضغطها", "Convertit les guides RH Markdown en pages HTML autonomes et les compresse"),
            steps:
            [
                Step("s1", BatchConvertSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "md" }),
                Step("s2", SafeArchiveSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["archiveFilePath"] = "${outputDir}/onboarding_html_pack.zip" })
            ]),

        // 10. HR & Admin: Downloads & Desktop Smart Organizer
        Create(
            id: "playbook.hr.downloads_smart_cleanup",
            category: "HR",
            titles: L("مرتب‌سازی هوشمند پوشهٔ دانلودها و حذف تکراری‌ها", "Downloads Folder Smart Cleanup & Deduplication", "下载文件夹智能整理与去重", "Limpieza Inteligente y Deduplicación de Descargas", "تنظيم مجلد التنزيلات الذكي وإزالة المكررات", "Tri Intelligent et Déduplication du Dossier Téléchargements"),
            descriptions: L("قرنطینهٔ فایل‌های تکراری و دسته‌بندی خودکار در ۶ پوشهٔ استاندارد", "Moves duplicate files to staging and organizes remaining files into 6 standard folders", "隔离重复文件并将剩余文件归类至 6 个标准文件夹", "Aísla duplicados y organiza los archivos en 6 carpetas estándar", "يعزل الملفات المكررة وينظم الباقي في 6 مجلدات قياسية", "Isole les doublons et classe les fichiers dans 6 dossiers standards"),
            steps:
            [
                Step("s1", DuplicateFinderSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}" }),
                Step("s2", FolderOrganizeSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}" })
            ]),

        // 11. Content & Media: Photo Privacy Scrubber & Archive
        Create(
            id: "playbook.content.photo_privacy_publish",
            category: "Content",
            titles: L("حذف اطلاعات GPS/EXIF تصاویر پیش از انتشار در وب", "Photo EXIF/GPS Privacy Scrubber for Web Publishing", "图片 EXIF/GPS 隐私清除与发布打包", "Limpiador de Privacidad EXIF/GPS para Publicación Web", "إزالة بيانات الموقع والكاميرا من الصور قبل النشر", "Nettoyage EXIF/GPS des Photos Avant Publication Web"),
            descriptions: L("پاک‌سازی مختصات مکانی و سریال دوربین از تصاویر JPEG و تهیهٔ آرشیو آمادهٔ انتشار", "Strips EXIF/GPS segments from JPEG photos and creates a clean publishing zip", "清除 JPEG 照片中的 GPS 与相机信息并打包发布", "Elimina datos GPS/EXIF de fotos JPEG y crea un paquete limpio", "يزيل بيانات الموقع الجغرافي من صور JPEG وينشئ حزمة جاهزة للنشر", "Supprime les données GPS/EXIF des photos JPEG et crée une archive propre"),
            steps:
            [
                Step("s1", MetadataScrubberSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "jpg" }),
                Step("s2", SafeArchiveSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["archiveFilePath"] = "${outputDir}/web_ready_photos.zip" })
            ]),

        // 12. Content & Media: Markdown Blog Series to HTML & Backup
        Create(
            id: "playbook.content.markdown_to_html_series",
            category: "Content",
            titles: L("تبدیل دسته‌ای مقالات Markdown به HTML و پشتیبان‌گیری", "Batch Markdown Articles to HTML & Backup", "Markdown 文章批量转 HTML 与备份", "Artículos Markdown en Lote a HTML y Respaldo", "تحويل مقالات Markdown دفعة واحدة إلى HTML والنسخ الاحتياطي", "Conversion par Lots d’Articles Markdown en HTML et Sauvegarde"),
            descriptions: L("تبدیل پیش‌نویس‌های مقاله به HTML استاندارد با فونت وزیرمتن/چندزبانه و گزارش پوشه", "Converts Markdown drafts to styled HTML and generates a content inventory report", "将 Markdown 草稿转换为排版良好的 HTML 并生成内容盘点报告", "Convierte borradores Markdown a HTML estilizado y genera informe", "يحول مسودات المقالات إلى HTML منسق وينشئ تقرير محتوى", "Convertit les brouillons Markdown en HTML stylisé et génère un rapport"),
            steps:
            [
                Step("s1", BatchConvertSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "md" }),
                Step("s2", FolderReportSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["outputReportPath"] = "${outputDir}/published_inventory.md" })
            ]),

        // 13. Content & Media: Research Notes Summarizer & Action List
        Create(
            id: "playbook.content.research_digest",
            category: "Content",
            titles: L("چکیده‌سازی یادداشت‌های پژوهشی و استخراج ایده‌ها", "Research Notes Digest & Action Items", "研究笔记摘要与行动清单", "Resumen de Notas de Investigación y Tareas", "تلخيص الملاحظات البحثية واستخراج الأفكار التنفيذية", "Synthèse des Notes de Recherche et Liste d’Actions"),
            descriptions: L("تولید خلاصهٔ اجرایی از ده‌ها فایل یادداشت و کپی نسخه‌های اصلی در پوشهٔ مقصد", "Summarizes research notes and copies source files into the project output folder", "汇总多份研究笔记并将源文件备份至输出目录", "Resume notas de investigación y respalda archivos fuente", "يلخص الملاحظات البحثية وينسخ الملفات المصدرية", "Résume les notes de recherche et copie les fichiers sources"),
            steps:
            [
                Step("s1", BatchSummarizeSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputSummaryPath"] = "${outputDir}/research_digest.md", ["extension"] = "txt" }),
                Step("s2", FileCopySkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "txt" })
            ]),

        // 14. Customer Support: Ticket Log PII Sanitizer & Summary
        Create(
            id: "playbook.support.ticket_sanitized_digest",
            category: "Support",
            titles: L("سانسور اطلاعات مشتریان در تیکت‌ها و خلاصه‌سازی مشکلات", "Support Ticket PII Sanitizer & Issue Digest", "客服工单隐私脱敏与问题汇总", "Anonimizador de Tickets de Soporte y Resumen", "إخفاء بيانات العملاء في التذاكر وتلخيص المشكلات", "Anonymisation des Tickets Support et Synthèse des Problèmes"),
            descriptions: L("ماسک کردن شماره کارت و تلفن مشتریان در تیکت‌ها و تولید گزارش خلاصهٔ مشکلات", "Redacts customer PII from support tickets and generates an executive summary", "脱敏客服工单中的客户敏感信息并生成问题摘要", "Oculta datos personales en tickets de soporte y resume incidencias", "يحجب بيانات العملاء الحساسة في التذاكر وينشئ ملخصاً تنفيذياً", "Masque les données clients dans les tickets et génère un résumé"),
            steps:
            [
                Step("s1", PiiRedactionSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "txt" }),
                Step("s2", BatchSummarizeSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["outputSummaryPath"] = "${outputDir}/tickets_summary.md", ["extension"] = "txt" })
            ]),

        // 15. Customer Support: CRM Export CSV Cleaner & Backup
        Create(
            id: "playbook.support.crm_csv_hygiene",
            category: "Support",
            titles: L("پاک‌سازی خروجی CSV باشگاه مشتریان (CRM) و آرشیو", "CRM Export CSV Hygiene & Safe Archive", "CRM 导出 CSV 清洗与安全归档", "Limpieza de CSV Exportado de CRM y Archivo", "تنظيف ملفات CSV المصدرة من CRM وأرشفتها", "Nettoyage d’Export CSV CRM et Archivage"),
            descriptions: L("استانداردسازی حروف و ارقام فارسی/عربی در فایل‌های مشتریان و فشرده‌سازی امن", "Normalizes multilingual characters/digits in CRM CSV exports and archives them", "规范化 CRM 导出表格中的多语言字符与数字并安全归档", "Normaliza caracteres y dígitos en exportaciones CRM y archiva", "يوحد الحروف والأرقام في جداول العملاء ويؤرشفها بأمان", "Normalise les caractères et chiffres des exports CRM et les archive"),
            steps:
            [
                Step("s1", FileCopySkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "csv" }),
                Step("s2", SafeArchiveSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["archiveFilePath"] = "${outputDir}/crm_export_backup.zip" })
            ]),

        // 16. Customer Support: Knowledge Base PDF Compilation
        Create(
            id: "playbook.support.kb_pdf_book",
            category: "Support",
            titles: L("تجمیع راهنماهای پشتیبانی در یک کتابچهٔ PDF", "Knowledge Base Compilation into Single PDF", "知识库文档合并为单一 PDF 手册", "Compilación de Base de Conocimiento en un PDF", "تجميع أدلة الدعم الفني في كتيب PDF موحد", "Compilation de la Base de Connaissances en un PDF Unique"),
            descriptions: L("ادغام تمام برگه‌های راهنمای پشتیبانی در یک فایل PDF قابل چاپ", "Merges support articles into a single offline PDF handbook", "将多篇支持文档合并为一本离线 PDF 手册", "Une artículos de soporte en un manual PDF sin conexión", "يدمج مقالات الدعم في كتيب PDF واحد يعمل دون اتصال", "Fusionne les articles de support en un manuel PDF hors-ligne"),
            steps:
            [
                Step("s1", PdfToolkitSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputFilePath"] = "${outputDir}/support_handbook.pdf" }),
                Step("s2", FolderReportSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputReportPath"] = "${outputDir}/kb_inventory.md" })
            ]),

        // 17. IT & DevOps: Log Sanitizer (IP/Email Redaction) & Zip Bundle
        Create(
            id: "playbook.it.log_sanitizer_bundle",
            category: "IT",
            titles: L("پاک‌سازی آدرس‌های IP و ایمیل از لاگ‌ها پیش از ارسال به پشتیبانی", "Server Log IP/Email Sanitizer & Support Bundle", "服务器日志 IP/邮箱脱敏与支持打包", "Limpiador de IP/Email en Logs y Paquete de Soporte", "إخفاء عناوين IP والبريد من السجلات قبل إرسالها للدعم", "Masquage IP/Email des Journaux Serveur et Archive Support"),
            descriptions: L("ماسک کردن IPv4 و ایمیل‌های داخل فایل‌های لاگ و ساخت بستهٔ ZIP", "Redacts IPv4 addresses and emails from log files and bundles them into a ZIP", "隐去日志文件中的 IPv4 地址与邮箱并打包为 ZIP", "Oculta direcciones IPv4 y correos de los logs y crea un ZIP", "يحجب عناوين IP والبريد الإلكتروني من ملفات السجل ويضغطها", "Masque les adresses IPv4 et e-mails des logs et crée une archive ZIP"),
            steps:
            [
                Step("s1", PiiRedactionSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}", ["extension"] = "log" }),
                Step("s2", SafeArchiveSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["archiveFilePath"] = "${outputDir}/sanitized_logs.zip" })
            ]),

        // 18. IT & DevOps: Daily Incremental Workspace Backup & Storage Report
        Create(
            id: "playbook.it.daily_incremental_backup",
            category: "IT",
            titles: L("پشتیبان‌گیری افزایشی روزانه از پروژه و گزارش فضا", "Daily Incremental Workspace Backup & Storage Report", "工作区每日增量备份与存储报告", "Respaldo Incremental Diario del Proyecto e Informe", "النسخ الاحتياطي التزايدي اليومي للمشروع وتقرير المساحة", "Sauvegarde Incrémentale Quotidienne et Rapport d’Espace"),
            descriptions: L("کپی فقط فایل‌های تغییریافتهٔ پروژه و تولید گزارش تفکیکی حجم پسوندها", "Backs up only modified project files and generates a storage breakdown report", "仅备份已修改的项目文件并生成存储空间分析报告", "Respalda solo archivos modificados y genera informe de espacio", "ينسخ الملفات المعدلة فقط وينشئ تقرير تفصيل المساحة", "Sauvegarde uniquement les fichiers modifiés et génère un rapport d’espace"),
            steps:
            [
                Step("s1", IncrementalBackupSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputDir"] = "${outputDir}" }),
                Step("s2", FolderReportSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["outputReportPath"] = "${outputDir}/backup_report.md" })
            ]),

        // 19. IT & DevOps: Release Notes Summarizer & HTML Changelog
        Create(
            id: "playbook.it.release_notes_publisher",
            category: "IT",
            titles: L("تولید یادداشت انتشار (Release Notes) و تبدیل به HTML", "Release Notes Summarizer & HTML Changelog", "版本发布说明汇总与 HTML 转换", "Generador de Notas de Versión y Changelog HTML", "تلخيص ملاحظات الإصدار وتحويلها إلى HTML", "Synthèse des Notes de Version et Changelog HTML"),
            descriptions: L("خلاصه‌سازی مستندات تغییرات نسخه و تولید صفحهٔ HTML مستقل", "Summarizes release notes and converts Markdown docs to standalone HTML", "汇总版本变更说明并将 Markdown 文档转换为独立 HTML", "Resume notas de versión y convierte documentos Markdown a HTML", "يلخص ملاحظات الإصدار ويحول مستندات Markdown إلى HTML", "Résume les notes de version et convertit les documents Markdown en HTML"),
            steps:
            [
                Step("s1", BatchSummarizeSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputSummaryPath"] = "${outputDir}/release_summary.md", ["extension"] = "md" }),
                Step("s2", BatchConvertSkill.SkillId, new() { ["sourceDir"] = "${outputDir}", ["outputDir"] = "${outputDir}", ["extension"] = "md" })
            ]),

        // 20. IT & DevOps: Incident Post-Mortem Search & Action Extraction
        Create(
            id: "playbook.it.incident_postmortem",
            category: "IT",
            titles: L("تحلیل گزارش‌های رخداد (Post-Mortem) و استخراج اقدام‌های اصلاحی", "Incident Post-Mortem Search & Action Extraction", "故障复盘检索与改进项提取", "Análisis Post-Mortem de Incidentes y Acciones", "تحليل تقارير الحوادث التقنية واستخراج الإجراءات التصحيحية", "Analyse Post-Mortem d’Incidents et Extraction d’Actions"),
            descriptions: L("جست‌وجوی مفهومی در گزارش‌های رخداد و استخراج فهرست اقدام‌های پیشگیرانه", "Searches incident reports and extracts preventative action items into a checklist", "检索故障报告并提取预防性行动清单", "Busca en informes de incidentes y extrae lista de acciones preventivas", "يبحث في تقارير الحوادث ويستخرج قائمة الإجراءات الوقائية", "Recherche dans les rapports d’incident et extrait les actions préventives"),
            steps:
            [
                Step("s1", LocalSemanticSearchSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["query"] = "خطا اقدام", ["extension"] = "txt" }),
                Step("s2", BatchSummarizeSkill.SkillId, new() { ["sourceDir"] = "${sourceDir}", ["outputSummaryPath"] = "${outputDir}/postmortem_actions.md", ["extension"] = "txt" })
            ])
    ];

    private static WorkflowDefinition Create(
        string id,
        string category,
        IReadOnlyDictionary<string, string> titles,
        IReadOnlyDictionary<string, string> descriptions,
        IReadOnlyList<WorkflowStepDefinition> steps) =>
        new(id, "1.0", category, titles, descriptions, steps);

    private static WorkflowStepDefinition Step(
        string stepId,
        string skillId,
        Dictionary<string, string> parameters,
        WorkflowStepCondition? condition = null) =>
        new(stepId, skillId, parameters, condition);

    private static IReadOnlyDictionary<string, string> L(
        string fa, string en, string zh, string es, string ar, string fr) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = fa,
            ["en"] = en,
            ["zh"] = zh,
            ["es"] = es,
            ["ar"] = ar,
            ["fr"] = fr
        };
}
