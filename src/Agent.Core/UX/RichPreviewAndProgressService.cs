using Agent.Core.AI;
using Agent.Core.Localization;
using Agent.Core.Skills;
using Agent.Core.Storage;
using System.Text.Json;

namespace Agent.Core.UX;

public sealed record LiveProgressSnapshot(
    int CompletedItems,
    int TotalItems,
    double PercentComplete,
    double ItemsPerSecond,
    TimeSpan EstimatedRemaining);

public sealed record FriendlyErrorExplanation(
    string ErrorCode,
    string UserTitle,
    string ActionableRecoveryTip);

/// <summary>
/// Provides Rich Preview Diff formatting (#86), Live Progress &amp; ETA calculation (#87),
/// 6-language Friendly Error Translation (#89), and PII-Free Local Diagnostics Export (#90).
/// </summary>
public static class RichPreviewAndProgressService
{
    public static string FormatBeforeAfterBadge(string beforeValue, string afterValue, bool isReversible = true)
    {
        var undoBadge = isReversible ? "[Undo Ready ✓]" : "[Permanent !]";
        return $"{beforeValue}  ──►  {afterValue}   {undoBadge}";
    }

    public static LiveProgressSnapshot CalculateLiveProgress(
        int completedItems,
        int totalItems,
        TimeSpan elapsed)
    {
        var safeTotal = Math.Max(1, totalItems);
        var clampedCompleted = Math.Clamp(completedItems, 0, safeTotal);
        var pct = Math.Round((double)clampedCompleted / safeTotal * 100.0, 1);

        var seconds = Math.Max(0.1, elapsed.TotalSeconds);
        var rate = Math.Round(clampedCompleted / seconds, 2);
        var remainingItems = safeTotal - clampedCompleted;
        var etaSeconds = rate > 0.01 ? remainingItems / rate : 0;

        return new LiveProgressSnapshot(
            CompletedItems: clampedCompleted,
            TotalItems: safeTotal,
            PercentComplete: pct,
            ItemsPerSecond: rate,
            EstimatedRemaining: TimeSpan.FromSeconds(Math.Clamp(etaSeconds, 0, 86400)));
    }

    public static FriendlyErrorExplanation TranslateException(
        Exception exception,
        string languageCode = CultureCatalog.DefaultLanguageCode)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);

        if (exception is UnauthorizedAccessException)
        {
            return lang switch
            {
                "fa" => new("ERR_ACCESS_DENIED", "دسترسی به پوشه یا فایل مسدود است", "لطفاً یک پوشهٔ معمولی کاربر (مانند Documents یا Desktop) انتخاب کنید و از پوشه‌های سیستمی استفاده نکنید."),
                "ar" => new("ERR_ACCESS_DENIED", "تم رفض الوصول إلى المجلد أو الملف", "يرجى اختيار مجلد مستخدم عادي وتجنب مجلدات النظام المحمية."),
                "zh" => new("ERR_ACCESS_DENIED", "文件夹或文件访问被拒绝", "请选择普通用户文件夹（如文档或桌面），切勿选择系统受保护目录。"),
                "es" => new("ERR_ACCESS_DENIED", "Acceso denegado a la carpeta o archivo", "Seleccione una carpeta de usuario normal y evite directorios del sistema."),
                "fr" => new("ERR_ACCESS_DENIED", "Accès refusé au dossier ou fichier", "Veuillez choisir un dossier utilisateur standard et éviter les dossiers système."),
                _ => new("ERR_ACCESS_DENIED", "Folder or file access denied", "Please choose a standard user folder (such as Documents or Desktop) and avoid protected system directories.")
            };
        }

        if (exception is DirectoryNotFoundException or FileNotFoundException)
        {
            return lang switch
            {
                "fa" => new("ERR_NOT_FOUND", "پوشه یا فایل مورد نظر پیدا نشد", "مسیر انتخاب‌شده را بررسی کنید و مطمئن شوید پوشه حذف یا جابه‌جا نشده است."),
                "ar" => new("ERR_NOT_FOUND", "لم يتم العثور على المجلد أو الملف", "تحقق من المسار المحدد وتأكد من وجود المجلد."),
                "zh" => new("ERR_NOT_FOUND", "未找到指定的文件夹或文件", "请检查所选路径并确认文件夹未被移动或删除。"),
                "es" => new("ERR_NOT_FOUND", "Carpeta o archivo no encontrado", "Verifique la ruta seleccionada y asegúrese de que exista."),
                "fr" => new("ERR_NOT_FOUND", "Dossier ou fichier introuvable", "Vérifiez le chemin sélectionné et assurez-vous qu’il existe."),
                _ => new("ERR_NOT_FOUND", "Folder or file not found", "Verify the selected path and ensure the folder has not been moved or deleted.")
            };
        }

        return lang switch
        {
            "fa" => new("ERR_GENERAL", "عملیات متوقف شد", "جزئیات پیش‌نمایش را بررسی کرده و دوباره تلاش کنید."),
            _ => new("ERR_GENERAL", "Operation stopped", "Please review the preview details and try again.")
        };
    }

    public static string ExportPiiFreeDiagnosticsJson(
        SkillRegistry registry,
        AuditChainVerificationResult auditStatus,
        string activeLanguageCode,
        string? customNote = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(auditStatus);

        var anonymizer = new LocalAnonymizerProxy();
        var scrubbedNote = string.IsNullOrWhiteSpace(customNote)
            ? string.Empty
            : anonymizer.Anonymize(customNote).AnonymizedText;

        var report = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            activeLanguage = CultureCatalog.NormalizeLanguageCode(activeLanguageCode),
            registeredSkillsCount = registry.Descriptors.Count,
            auditLogChainValid = auditStatus.IsValid,
            auditLogVerifiedEntries = auditStatus.VerifiedCount,
            scrubbedUserNote = scrubbedNote
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }
}
