using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;

namespace Agent.Core.Skills;

public sealed record FolderOrganizeRequest(
    string DirectoryPath,
    string LanguageCode = CultureCatalog.DefaultLanguageCode,
    bool UseLocalizedFolderNames = false);

public sealed record FolderOrganizeCandidate(
    string SourcePath,
    string FileName,
    string CategoryKey,
    string TargetFolderName,
    string DestinationPath,
    long SizeBytes);

public sealed record FolderOrganizePreview(
    string DirectoryPath,
    IReadOnlyList<FolderOrganizeCandidate> Candidates)
{
    public int ReadyCount => Candidates.Count;
}

public sealed record FolderOrganizeResult(
    int MovedCount,
    int SkippedCount,
    IReadOnlyList<string> FailedFileNames,
    bool Cancelled,
    OperationManifest RollbackManifest);

/// <summary>
/// Organizes top-level files in a cluttered folder (e.g., Downloads) into 6
/// structured category folders (#13) with mandatory preview, non-overwriting
/// auto-numbering, and full transactional Undo (#20).
/// </summary>
public sealed class FolderOrganizeSkill : ISkill<FolderOrganizeRequest, FolderOrganizePreview, FolderOrganizeResult>
{
    public const string SkillId = "file.organize";
    public const int MaximumFilesPerRun = 500;

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CategoryFolderNames =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Documents"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Documents", ["fa"] = "اسناد", ["zh"] = "文档", ["es"] = "Documentos", ["ar"] = "المستندات", ["fr"] = "Documents"
            },
            ["Images"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Images", ["fa"] = "تصاویر", ["zh"] = "图片", ["es"] = "Imágenes", ["ar"] = "الصور", ["fr"] = "Images"
            },
            ["Videos"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Videos", ["fa"] = "ویدئوها", ["zh"] = "视频", ["es"] = "Videos", ["ar"] = "الفيديوهات", ["fr"] = "Vidéos"
            },
            ["Audio"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Audio", ["fa"] = "صوت و موسیقی", ["zh"] = "音频", ["es"] = "Audio", ["ar"] = "الصوتيات", ["fr"] = "Audio"
            },
            ["Archives"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Archives", ["fa"] = "فایل‌های فشرده", ["zh"] = "压缩包", ["es"] = "Archivos Comprimidos", ["ar"] = "الأرشيف", ["fr"] = "Archives"
            },
            ["CodeAndData"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "CodeAndData", ["fa"] = "کد و داده", ["zh"] = "代码与数据", ["es"] = "Código y Datos", ["ar"] = "البرمجة والبيانات", ["fr"] = "Code et Données"
            }
        };

    private static readonly Dictionary<string, string> ExtensionToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "Documents", ["doc"] = "Documents", ["docx"] = "Documents", ["txt"] = "Documents",
        ["rtf"] = "Documents", ["odt"] = "Documents", ["ppt"] = "Documents", ["pptx"] = "Documents",
        ["xls"] = "Documents", ["xlsx"] = "Documents", ["epub"] = "Documents",

        ["jpg"] = "Images", ["jpeg"] = "Images", ["png"] = "Images", ["gif"] = "Images",
        ["webp"] = "Images", ["bmp"] = "Images", ["svg"] = "Images", ["heic"] = "Images", ["ico"] = "Images",

        ["mp4"] = "Videos", ["mkv"] = "Videos", ["mov"] = "Videos", ["avi"] = "Videos", ["webm"] = "Videos",

        ["mp3"] = "Audio", ["wav"] = "Audio", ["ogg"] = "Audio", ["flac"] = "Audio", ["m4a"] = "Audio", ["aac"] = "Audio",

        ["zip"] = "Archives", ["7z"] = "Archives", ["rar"] = "Archives", ["tar"] = "Archives", ["gz"] = "Archives",

        ["json"] = "CodeAndData", ["csv"] = "CodeAndData", ["tsv"] = "CodeAndData", ["xml"] = "CodeAndData",
        ["yaml"] = "CodeAndData", ["yml"] = "CodeAndData", ["sql"] = "CodeAndData", ["md"] = "CodeAndData",
        ["cs"] = "CodeAndData", ["py"] = "CodeAndData", ["js"] = "CodeAndData", ["ts"] = "CodeAndData", ["html"] = "CodeAndData"
    };

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "سازمان‌دهی خودکار پوشه",
            ["en"] = "Auto-Organize Folder",
            ["zh"] = "文件夹自动整理",
            ["es"] = "Organización Automática de Carpeta",
            ["ar"] = "تنظيم تلقائي للمجلد",
            ["fr"] = "Organisation Automatique de Dossier"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "مرتب‌سازی فایل‌های پراکنده در ۶ زیرپوشهٔ استاندارد (اسناد، تصاویر، ویدئوها، صوت، آرشیوها و کد/داده) با پیش‌نمایش و قابلیت واگرد (Undo).",
            ["en"] = "Sorts loose files into 6 structured subfolders (Documents, Images, Videos, Audio, Archives, CodeAndData) with preview and Undo.",
            ["zh"] = "将零散文件按类型归类到 6 个标准子文件夹（文档、图片、视频、音频、压缩包、代码与数据），支持预览与撤销。",
            ["es"] = "Clasifica archivos sueltos en 6 subcarpetas temáticas con vista previa y opción de deshacer.",
            ["ar"] = "ترتيب الملفات المبعثرة في 6 مجلدات فرعية قياسية مع معاينة وإمكانية التراجع.",
            ["fr"] = "Trie les fichiers en 6 sous-dossiers thématiques avec aperçu obligatoire et annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public FolderOrganizePreview Preview(FolderOrganizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var directory = PathGuard.ValidateSelectedFolder(request.DirectoryPath, "سازمان‌دهی");
        var lang = request.UseLocalizedFolderNames
            ? CultureCatalog.NormalizeLanguageCode(request.LanguageCode)
            : "en";

        var comparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var reservedDestinations = new HashSet<string>(comparison);
        var candidates = new List<FolderOrganizeCandidate>();

        foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, comparison))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            var ext = Path.GetExtension(filePath).TrimStart('.');
            if (string.IsNullOrWhiteSpace(ext) || !ExtensionToCategory.TryGetValue(ext, out var categoryKey))
            {
                continue;
            }

            if (candidates.Count >= MaximumFilesPerRun)
            {
                throw new InvalidOperationException($"در هر نوبت حداکثر {MaximumFilesPerRun} فایل سازمان‌دهی می‌شود.");
            }

            var targetFolderName = CategoryFolderNames[categoryKey].TryGetValue(lang, out var localizedFolder)
                ? localizedFolder
                : categoryKey;
            var targetFolder = Path.Combine(directory, targetFolderName);
            var fileName = Path.GetFileName(filePath);
            var resolvedPath = ResolveNonCollidingPath(Path.Combine(targetFolder, fileName), reservedDestinations);
            reservedDestinations.Add(resolvedPath);

            candidates.Add(new FolderOrganizeCandidate(
                SourcePath: filePath,
                FileName: fileName,
                CategoryKey: categoryKey,
                TargetFolderName: targetFolderName,
                DestinationPath: resolvedPath,
                SizeBytes: new FileInfo(filePath).Length));
        }

        return new FolderOrganizePreview(directory, candidates);
    }

    public Task<FolderOrganizeResult> ExecuteAsync(
        FolderOrganizeRequest request,
        FolderOrganizePreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        if (approvedPreview.ReadyCount == 0)
        {
            throw new InvalidOperationException("فایل قابل سازمان‌دهی در پوشهٔ انتخاب‌شده وجود ندارد.");
        }

        var currentPreview = Preview(request);
        if (currentPreview.Candidates.Count != approvedPreview.Candidates.Count)
        {
            throw new InvalidOperationException("محتوای پوشه از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var moved = 0;
        var skipped = 0;
        var failed = new List<string>();
        var cancelled = false;
        var records = new List<OperationRecord>();
        var createdFolders = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var item in currentPreview.Candidates)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            try
            {
                var targetDir = Path.GetDirectoryName(item.DestinationPath)!;
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    if (createdFolders.Add(targetDir))
                    {
                        records.Add(new OperationRecord(
                            OperationRecordKind.DirectoryCreated,
                            OriginalPath: null,
                            TargetPath: targetDir,
                            SizeBytes: 0));
                    }
                }

                if (File.Exists(item.DestinationPath) || Directory.Exists(item.DestinationPath))
                {
                    skipped++;
                    continue;
                }

                File.Move(item.SourcePath, item.DestinationPath, overwrite: false);
                moved++;
                records.Add(new OperationRecord(
                    OperationRecordKind.FileMovedOrRenamed,
                    OriginalPath: item.SourcePath,
                    TargetPath: item.DestinationPath,
                    SizeBytes: item.SizeBytes));
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

        return Task.FromResult(new FolderOrganizeResult(moved, skipped, failed, cancelled, manifest));
    }

    private static string ResolveNonCollidingPath(string basePath, HashSet<string> reservedPaths)
    {
        if (!File.Exists(basePath) && !Directory.Exists(basePath) && !reservedPaths.Contains(basePath))
        {
            return basePath;
        }

        var dir = Path.GetDirectoryName(basePath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(basePath);
        var ext = Path.GetExtension(basePath);

        for (var i = 1; i <= 9999; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate) && !reservedPaths.Contains(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{stem}_{Guid.NewGuid():N}{ext}");
    }
}
