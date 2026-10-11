using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Security.Cryptography;

namespace Agent.Core.Skills;

public sealed record DuplicateFinderRequest(
    string DirectoryPath,
    bool IncludeSubdirectories = false,
    int MaxDepth = 5);

public sealed record DuplicateFileCandidate(
    string DuplicatePath,
    string DuplicateName,
    string KeptOriginalPath,
    string QuarantineDestinationPath,
    long SizeBytes,
    string Sha256Hex);

public sealed record DuplicateFinderPreview(
    string DirectoryPath,
    string QuarantineDirectoryPath,
    IReadOnlyList<DuplicateFileCandidate> Duplicates)
{
    public int DuplicateCount => Duplicates.Count;
    public long ReclaimableBytes => Duplicates.Sum(item => item.SizeBytes);
}

public sealed record DuplicateFinderResult(
    int QuarantinedCount,
    int SkippedCount,
    IReadOnlyList<string> FailedFileNames,
    bool Cancelled,
    OperationManifest RollbackManifest);

/// <summary>
/// Identifies exact duplicate files using size pre-filtering and SHA-256 hashing
/// (#14). Never permanently deletes user files; moves duplicates into a local
/// quarantine folder with a full RollbackManager Undo manifest (#20).
/// </summary>
public sealed class DuplicateFinderSkill : ISkill<DuplicateFinderRequest, DuplicateFinderPreview, DuplicateFinderResult>
{
    public const string SkillId = "file.deduplicate";
    public const string QuarantineFolderName = ".RoboticAgent-Quarantine";
    public const int MaximumScannedFiles = 1500;

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "شناسایی و قرنطینهٔ فایل‌های تکراری",
            ["en"] = "Duplicate Finder & Safe Quarantine",
            ["zh"] = "重复文件检测与安全隔离",
            ["es"] = "Detector de Duplicados y Cuarentena Segura",
            ["ar"] = "كشف الملفات المكررة والعزل الآمن",
            ["fr"] = "Détection de Doublons et Quarantaine Sécurisée"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "یافتن فایل‌های کاملاً یکسان با مقایسهٔ اندازه و هش SHA-256 و انتقال ایمن نسخه‌های تکراری به پوشهٔ قرنطینه با قابلیت بازگردانی کامل (Undo).",
            ["en"] = "Finds byte-identical duplicates via size + SHA-256 hashing and moves redundant copies to a safe quarantine folder with 1-click Undo.",
            ["zh"] = "通过文件大小与 SHA-256 哈希精准识别重复文件，将冗余副本移入安全隔离区并支持一键恢复。",
            ["es"] = "Encuentra duplicados exactos mediante tamaño y SHA-256 y los mueve a una carpeta de cuarentena con opción de deshacer.",
            ["ar"] = "يكتشف الملفات المتطابقة عبر الحجم وبصمة SHA-256 وينقل النسخ المكررة إلى مجلد عزل آمن مع إمكانية الاستعادة.",
            ["fr"] = "Identifie les doublons exacts via taille et SHA-256 et les déplace en quarantaine sécurisée avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public DuplicateFinderPreview Preview(DuplicateFinderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var directory = PathGuard.ValidateSelectedFolder(request.DirectoryPath, "بررسی تکراری‌ها");
        var quarantineRoot = Path.Combine(directory, QuarantineFolderName);
        var maxDepth = request.IncludeSubdirectories
            ? Math.Clamp(request.MaxDepth <= 0 ? 5 : request.MaxDepth, 1, 10)
            : 0;

        var allFiles = new List<FileInfo>();
        CollectCandidateFiles(directory, quarantineRoot, 0, maxDepth, allFiles);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var reservedQuarantinePaths = new HashSet<string>(comparison);
        var duplicates = new List<DuplicateFileCandidate>();

        // Pre-filter by exact byte length (> 0) so unique-sized files never need hashing.
        var sameSizeGroups = allFiles
            .Where(info => info.Length > 0)
            .GroupBy(info => info.Length)
            .Where(group => group.Count() > 1);

        foreach (var sizeGroup in sameSizeGroups)
        {
            var byHash = new Dictionary<string, List<FileInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in sizeGroup.OrderBy(f => f.FullName, comparison))
            {
                var hash = ComputeSha256Hex(info.FullName);
                if (!byHash.TryGetValue(hash, out var list))
                {
                    list = [];
                    byHash[hash] = list;
                }

                list.Add(info);
            }

            foreach (var (hash, identicalFiles) in byHash)
            {
                if (identicalFiles.Count < 2)
                {
                    continue;
                }

                var keptOriginal = identicalFiles[0];
                for (var i = 1; i < identicalFiles.Count; i++)
                {
                    var dup = identicalFiles[i];
                    var baseQuarantinePath = Path.Combine(quarantineRoot, dup.Name);
                    var quarantineTarget = ResolveUniquePath(baseQuarantinePath, reservedQuarantinePaths);
                    reservedQuarantinePaths.Add(quarantineTarget);

                    duplicates.Add(new DuplicateFileCandidate(
                        DuplicatePath: dup.FullName,
                        DuplicateName: dup.Name,
                        KeptOriginalPath: keptOriginal.FullName,
                        QuarantineDestinationPath: quarantineTarget,
                        SizeBytes: dup.Length,
                        Sha256Hex: hash));
                }
            }
        }

        return new DuplicateFinderPreview(directory, quarantineRoot, duplicates);
    }

    public Task<DuplicateFinderResult> ExecuteAsync(
        DuplicateFinderRequest request,
        DuplicateFinderPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        if (approvedPreview.DuplicateCount == 0)
        {
            throw new InvalidOperationException("فایل تکراری در پیش‌نمایش تأییدشده وجود ندارد.");
        }

        var currentPreview = Preview(request);
        if (currentPreview.DuplicateCount != approvedPreview.DuplicateCount)
        {
            throw new InvalidOperationException("محتوای پوشه از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var quarantined = 0;
        var skipped = 0;
        var failed = new List<string>();
        var cancelled = false;
        var records = new List<OperationRecord>();
        var quarantineCreated = false;

        foreach (var item in currentPreview.Duplicates)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            try
            {
                if (!File.Exists(item.KeptOriginalPath) || !File.Exists(item.DuplicatePath))
                {
                    skipped++;
                    continue;
                }

                if (!Directory.Exists(currentPreview.QuarantineDirectoryPath))
                {
                    Directory.CreateDirectory(currentPreview.QuarantineDirectoryPath);
                    quarantineCreated = true;
                }

                File.Move(item.DuplicatePath, item.QuarantineDestinationPath, overwrite: false);
                quarantined++;
                records.Add(new OperationRecord(
                    OperationRecordKind.FileMovedOrRenamed,
                    OriginalPath: item.DuplicatePath,
                    TargetPath: item.QuarantineDestinationPath,
                    SizeBytes: item.SizeBytes,
                    ContentSha256Hex: item.Sha256Hex));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(item.DuplicateName);
            }
        }

        if (quarantineCreated)
        {
            records.Insert(0, new OperationRecord(
                OperationRecordKind.DirectoryCreated,
                OriginalPath: null,
                TargetPath: currentPreview.QuarantineDirectoryPath,
                SizeBytes: 0));
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return Task.FromResult(new DuplicateFinderResult(quarantined, skipped, failed, cancelled, manifest));
    }

    private static void CollectCandidateFiles(
        string currentDir,
        string quarantineRoot,
        int depth,
        int maxDepth,
        List<FileInfo> results)
    {
        if (PathGuard.PathsEqual(currentDir, quarantineRoot))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(currentDir, "*", SearchOption.TopDirectoryOnly))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
            {
                continue;
            }

            if (results.Count >= MaximumScannedFiles)
            {
                throw new InvalidOperationException($"حداکثر {MaximumScannedFiles} فایل در هر نوبت بررسی تکراری‌ها پویش می‌شود.");
            }

            results.Add(new FileInfo(filePath));
        }

        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var subDir in Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly))
        {
            var attrs = File.GetAttributes(subDir);
            if ((attrs & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.PathsEqual(subDir, quarantineRoot))
            {
                continue;
            }

            CollectCandidateFiles(subDir, quarantineRoot, depth + 1, maxDepth, results);
        }
    }

    private static string ComputeSha256Hex(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string ResolveUniquePath(string basePath, HashSet<string> reservedPaths)
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
