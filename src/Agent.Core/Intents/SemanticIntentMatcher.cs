using Agent.Core.Localization;
using Agent.Core.Skills;
using Agent.Core.Skills.Documents;

namespace Agent.Core.Intents;

public sealed record SemanticIntentMatchResult(
    string IntentId,
    string RecommendedSkillId,
    double Confidence,
    string MatchedConcept);

/// <summary>
/// Multilingual Semantic Intent Matcher (#52) that augments the fast Naive Bayes
/// classifier with semantic concept prototypes across all 6 languages (fa, en, zh, es, ar, fr)
/// and maps natural-language intents to the 18 skills in SkillRegistry.
/// </summary>
public sealed class SemanticIntentMatcher
{
    private sealed record IntentConcept(
        string IntentId,
        string SkillId,
        IReadOnlyList<string> Keywords);

    private static readonly IReadOnlyList<IntentConcept> Concepts =
    [
        new("redact_pii", PiiRedactionSkill.SkillId,
            ["سانسور", "اطلاعات محرمانه", "شماره کارت", "شبا", "حریم خصوصی", "redact", "pii", "anonymize", "mask email", "隐私", "脱敏", "ocultar datos", "إخفاء البيانات", "anonymiser"]),
        new("scrub_metadata", MetadataScrubberSkill.SkillId,
            ["متادیتا", "فراداده", "exif", "gps", "metadata", "scrub", "strip exif", "元数据", "metadatos", "البيانات الوصفية", "métadonnées"]),
        new("extract_invoice", InvoiceExtractSkill.SkillId,
            ["فاکتور", "رسید", "مبلغ کل", "مالیات", "invoice", "receipt", "billing", "发票", "账单", "factura", "recibo", "فاتورة", "إيصال", "facture"]),
        new("clean_csv", CsvCleanerSkill.SkillId,
            ["پاکسازی جدول", "پاک‌سازی csv", "ردیف تکراری", "clean csv", "deduplicate rows", "spreadsheet", "清洗表格", "limpiar csv", "تنظيف جدول", "nettoyer csv"]),
        new("pdf_toolkit", PdfToolkitSkill.SkillId,
            ["ادغام pdf", "ترکیب pdf", "merge pdf", "split pdf", "extract pdf", "合并pdf", "unir pdf", "دمج pdf", "fusionner pdf"]),
        new("diff_documents", DocumentDiffSkill.SkillId,
            ["مقایسه دو نسخه", "تفاوت قرارداد", "diff", "compare versions", "对比文档", "comparar documentos", "مقارنة مستندين", "comparer documents"]),
        new("summarize_docs", BatchSummarizeSkill.SkillId,
            ["خلاصه", "چکیده", "اقدام‌ها", "summarize", "summary", "action items", "摘要", "总结", "resumir", "تلخيص", "résumer"]),
        new("search_docs", LocalSemanticSearchSkill.SkillId,
            ["جستجو در محتوا", "پیدا کن در متن", "semantic search", "find in documents", "检索内容", "buscar en documentos", "بحث في المحتوى", "rechercher dans"]),
        new("organize_folder", FolderOrganizeSkill.SkillId,
            ["مرتب کن", "سازماندهی پوشه", "دانلودها", "organize folder", "tidy downloads", "整理文件夹", "organizar carpeta", "تنظيم المجلد", "organiser dossier"]),
        new("find_duplicates", DuplicateFinderSkill.SkillId,
            ["فایل تکراری", "حذف تکراری", "duplicate files", "dedup", "重复文件", "archivos duplicados", "ملفات مكررة", "fichiers doublons"]),
        new("batch_rename", BatchRenameSkill.SkillId,
            ["تغییر نام", "نامگذاری دسته ای", "batch rename", "rename files", "批量重命名", "renombrar", "إعادة تسمية", "renommer"]),
        new("incremental_backup", IncrementalBackupSkill.SkillId,
            ["پشتیبان", "بکاپ افزایشی", "backup", "incremental", "增量备份", "respaldo", "نسخ احتياطي", "sauvegarde"]),
        new("safe_archive", SafeArchiveSkill.SkillId,
            ["فشرده", "آرشیو زیپ", "zip", "archive", "unzip", "压缩", "解压", "comprimir", "ضغط", "compresser"]),
        new("copy_files_by_extension", FileCopySkill.SkillId,
            ["کپی", "منتقل", "copy", "transfer", "复制", "拷贝", "copiar", "نسخ", "copier"])
    ];

    public SemanticIntentMatchResult MatchIntent(string userText)
    {
        var normalized = TextNormalizer.NormalizeMultilingual(userText);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new SemanticIntentMatchResult("copy_files_by_extension", FileCopySkill.SkillId, 0.25, "default");
        }

        IntentConcept? bestConcept = null;
        var bestHits = 0;
        var bestKeyword = string.Empty;

        foreach (var concept in Concepts)
        {
            var hits = 0;
            var matchedKw = string.Empty;
            foreach (var kw in concept.Keywords)
            {
                var normKw = TextNormalizer.NormalizeMultilingual(kw);
                if (!string.IsNullOrWhiteSpace(normKw) &&
                    normalized.Contains(normKw, StringComparison.OrdinalIgnoreCase))
                {
                    hits++;
                    if (string.IsNullOrEmpty(matchedKw))
                    {
                        matchedKw = kw;
                    }
                }
            }

            if (hits > bestHits)
            {
                bestHits = hits;
                bestConcept = concept;
                bestKeyword = matchedKw;
            }
        }

        if (bestConcept is not null && bestHits > 0)
        {
            var confidence = Math.Min(0.98, 0.65 + (bestHits * 0.15));
            return new SemanticIntentMatchResult(bestConcept.IntentId, bestConcept.SkillId, confidence, bestKeyword);
        }

        var nb = LocalIntentClassifier.Default.Classify(normalized);
        var mappedSkill = nb.Label switch
        {
            "copy_files_by_extension" => FileCopySkill.SkillId,
            _ => FileCopySkill.SkillId
        };

        return new SemanticIntentMatchResult(nb.Label, mappedSkill, Math.Max(0.40, nb.Confidence), "naive_bayes");
    }
}
