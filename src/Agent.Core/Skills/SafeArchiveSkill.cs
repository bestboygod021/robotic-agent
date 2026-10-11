using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.IO.Compression;

namespace Agent.Core.Skills;

public enum ArchiveOperationMode
{
    CreateZip,
    ExtractZip
}

public sealed record SafeArchiveRequest(
    ArchiveOperationMode Mode,
    string SourcePath,
    string DestinationPath,
    string ExtensionFilter = "*");

public sealed record SafeArchiveEntryPreview(
    string EntryName,
    string ResolvedTargetPath,
    long UncompressedSizeBytes,
    bool AlreadyExists);

public sealed record SafeArchivePreview(
    ArchiveOperationMode Mode,
    string SourcePath,
    string DestinationPath,
    IReadOnlyList<SafeArchiveEntryPreview> Entries,
    long TotalUncompressedBytes)
{
    public int ReadyCount => Entries.Count(e => !e.AlreadyExists);
}

public sealed record SafeArchiveResult(
    int ProcessedEntries,
    int SkippedEntries,
    string OutputPath,
    OperationManifest RollbackManifest);

/// <summary>
/// Safe ZIP archive creation and extraction skill (#15) with built-in protection
/// against Zip-Slip path traversal, Zip-Bomb expansion ratio attacks, deceptive
/// double extensions (#74), and full transactional Undo (#20).
/// </summary>
public sealed class SafeArchiveSkill : ISkill<SafeArchiveRequest, SafeArchivePreview, SafeArchiveResult>
{
    public const string SkillId = "file.archive";
    public const int MaximumArchiveEntries = 1000;
    public const long MaximumTotalUncompressedBytes = 512L * 1024 * 1024; // 512 MiB cap
    public const double MaximumCompressionRatio = 100.0;

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "فشرده‌سازی و استخراج امن آرشیو",
            ["en"] = "Safe Archive (ZIP) Guard",
            ["zh"] = "安全压缩与解压（防 Zip-Slip/炸弹）",
            ["es"] = "Archivo Comprimido Seguro (Anti Zip-Slip)",
            ["ar"] = "ضغط واستخراج آمن للأرشيف",
            ["fr"] = "Archivage ZIP Sécurisé (Anti Zip-Slip)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "ساخت و بازکردن فایل‌های ZIP با محافظت در برابر حملات Zip-Slip (خروج از پوشه) و Zip-Bomb (حجم انفجاری) همراه با پیش‌نمایش و واگرد.",
            ["en"] = "Creates and extracts ZIP archives with strict Zip-Slip path traversal and Zip-Bomb ratio guards, preview, and Undo.",
            ["zh"] = "创建与解压 ZIP 归档，内置路径穿越（Zip-Slip）与压缩炸弹（Zip-Bomb）防护、强制预览与撤销。",
            ["es"] = "Crea y extrae archivos ZIP con protección contra Zip-Slip y Zip-Bomb, vista previa y deshacer.",
            ["ar"] = "إنشاء واستخراج ملفات ZIP مع حماية صارمة ضد هجمات اجتياز المسار والقنابل المضغوطة ومعاينة وتراجع.",
            ["fr"] = "Crée et extrait des archives ZIP avec protection contre Zip-Slip et Zip-Bomb, aperçu et annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public SafeArchivePreview Preview(SafeArchiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Mode == ArchiveOperationMode.CreateZip
            ? PreviewCreateZip(request)
            : PreviewExtractZip(request);
    }

    public async Task<SafeArchiveResult> ExecuteAsync(
        SafeArchiveRequest request,
        SafeArchivePreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        if (approvedPreview.ReadyCount == 0)
        {
            throw new InvalidOperationException("پیش‌نمایش آرشیو هیچ مورد آمادهٔ اجرا ندارد.");
        }

        var currentPreview = Preview(request);
        if (currentPreview.Entries.Count != approvedPreview.Entries.Count ||
            currentPreview.TotalUncompressedBytes != approvedPreview.TotalUncompressedBytes)
        {
            throw new InvalidOperationException("فایل‌های آرشیو از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        return request.Mode == ArchiveOperationMode.CreateZip
            ? await ExecuteCreateZipAsync(currentPreview, context.CancellationToken).ConfigureAwait(false)
            : await ExecuteExtractZipAsync(currentPreview, context.CancellationToken).ConfigureAwait(false);
    }

    private static SafeArchivePreview PreviewCreateZip(SafeArchiveRequest request)
    {
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourcePath, "مبدأ آرشیو");
        var destZipFullPath = Path.GetFullPath(request.DestinationPath);
        var destDir = Path.GetDirectoryName(destZipFullPath)
            ?? throw new ArgumentException("مسیر فایل ZIP مقصد معتبر نیست.", nameof(request));

        PathGuard.ValidateSelectedFolder(destDir, "پوشهٔ مقصد آرشیو");
        if (!destZipFullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("فایل آرشیو مقصد باید پسوند .zip داشته باشد.", nameof(request));
        }

        if (File.Exists(destZipFullPath) || Directory.Exists(destZipFullPath))
        {
            throw new InvalidOperationException("فایل ZIP مقصد از قبل وجود دارد؛ بازنویسی مجاز نیست.");
        }

        var ext = FileCopySkill.NormalizeExtension(request.ExtensionFilter);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var entries = new List<SafeArchiveEntryPreview>();
        long totalBytes = 0;

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            if (entries.Count >= MaximumArchiveEntries)
            {
                throw new InvalidOperationException($"حداکثر {MaximumArchiveEntries} فایل در یک آرشیو مجاز است.");
            }

            var info = new FileInfo(filePath);
            totalBytes = checked(totalBytes + info.Length);
            if (totalBytes > MaximumTotalUncompressedBytes)
            {
                throw new InvalidOperationException("مجموع حجم فایل‌ها از سقف مجاز آرشیو (۵۱۲ مگابایت) بیشتر است.");
            }

            entries.Add(new SafeArchiveEntryPreview(
                EntryName: info.Name,
                ResolvedTargetPath: filePath,
                UncompressedSizeBytes: info.Length,
                AlreadyExists: false));
        }

        return new SafeArchivePreview(ArchiveOperationMode.CreateZip, sourceDir, destZipFullPath, entries, totalBytes);
    }

    private static SafeArchivePreview PreviewExtractZip(SafeArchiveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourcePath) || !File.Exists(request.SourcePath))
        {
            throw new FileNotFoundException("فایل ZIP مبدأ پیدا نشد.", request.SourcePath);
        }

        var zipFullPath = Path.GetFullPath(request.SourcePath);
        var zipDir = Path.GetDirectoryName(zipFullPath)
            ?? throw new ArgumentException("مسیر فایل ZIP معتبر نیست.", nameof(request));
        PathGuard.ValidateSelectedFolder(zipDir, "پوشهٔ فایل ZIP");
        var destinationDir = PathGuard.ValidateSelectedFolder(request.DestinationPath, "مقصد استخراج");

        var entries = new List<SafeArchiveEntryPreview>();
        long totalUncompressed = 0;

        using var archive = ZipFile.OpenRead(zipFullPath);
        if (archive.Entries.Count > MaximumArchiveEntries)
        {
            throw new InvalidOperationException($"تعداد فایل‌های داخل ZIP ({archive.Entries.Count}) بیش از سقف امنیتی ({MaximumArchiveEntries}) است.");
        }

        var normalizedDestRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDir)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue; // Directory placeholder entry
            }

            // Zip-Slip guard: ensure full resolved path stays strictly inside destinationDir
            var candidateTarget = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
            if (!candidateTarget.StartsWith(normalizedDestRoot, comparison))
            {
                throw new InvalidOperationException($"حملهٔ احتمالی Zip-Slip تشخیص داده شد: مسیر «{entry.FullName}» خارج از پوشهٔ مقصد است.");
            }

            if (PathGuard.IsSuspiciousDoubleExtension(entry.Name, out var reason))
            {
                throw new InvalidOperationException(reason);
            }

            // Zip-Bomb guard: check individual compression ratio and total uncompressed size
            if (entry.CompressedLength > 0 && entry.Length > 1024 * 1024)
            {
                var ratio = entry.Length / (double)entry.CompressedLength;
                if (ratio > MaximumCompressionRatio)
                {
                    throw new InvalidOperationException($"نسبت فشرده‌سازی غیرعادی (Zip-Bomb) در فایل «{entry.FullName}» تشخیص داده شد.");
                }
            }

            totalUncompressed = checked(totalUncompressed + entry.Length);
            if (totalUncompressed > MaximumTotalUncompressedBytes)
            {
                throw new InvalidOperationException("حجم بازشدهٔ آرشیو از سقف امنیتی ۵۱۲ مگابایت فراتر می‌رود (احتمال Zip-Bomb).");
            }

            var exists = File.Exists(candidateTarget) || Directory.Exists(candidateTarget);
            entries.Add(new SafeArchiveEntryPreview(
                EntryName: entry.FullName,
                ResolvedTargetPath: candidateTarget,
                UncompressedSizeBytes: entry.Length,
                AlreadyExists: exists));
        }

        return new SafeArchivePreview(ArchiveOperationMode.ExtractZip, zipFullPath, destinationDir, entries, totalUncompressed);
    }

    private static async Task<SafeArchiveResult> ExecuteCreateZipAsync(
        SafeArchivePreview preview,
        CancellationToken cancellationToken)
    {
        var tempZip = $"{preview.DestinationPath}.{Guid.NewGuid():N}.tmp";
        var count = 0;

        try
        {
            await using (var zipStream = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var item in preview.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(item.ResolvedTargetPath, item.EntryName, CompressionLevel.Optimal);
                    count++;
                }
            }

            File.Move(tempZip, preview.DestinationPath, overwrite: false);
            var zipInfo = new FileInfo(preview.DestinationPath);
            var sha256 = await RollbackManager.ComputeFileSha256HexAsync(preview.DestinationPath, cancellationToken).ConfigureAwait(false);

            var manifest = new OperationManifest(
                OperationId: Guid.NewGuid().ToString("N"),
                SkillId: SkillId,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                Records:
                [
                    new OperationRecord(
                        OperationRecordKind.FileCreatedOrCopied,
                        OriginalPath: preview.SourcePath,
                        TargetPath: preview.DestinationPath,
                        SizeBytes: zipInfo.Length,
                        ContentSha256Hex: sha256)
                ]);

            return new SafeArchiveResult(count, 0, preview.DestinationPath, manifest);
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }
        }
    }

    private static async Task<SafeArchiveResult> ExecuteExtractZipAsync(
        SafeArchivePreview preview,
        CancellationToken cancellationToken)
    {
        var extracted = 0;
        var skipped = 0;
        var records = new List<OperationRecord>();
        var createdDirs = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var normalizedDestRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(preview.DestinationPath)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        using var archive = ZipFile.OpenRead(preview.SourcePath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var targetPath = Path.GetFullPath(Path.Combine(preview.DestinationPath, entry.FullName));
            if (!targetPath.StartsWith(normalizedDestRoot, comparison))
            {
                throw new InvalidOperationException("مسیر فایل استخراجی خارج از پوشهٔ مجاز است.");
            }

            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                skipped++;
                continue;
            }

            var parentDir = Path.GetDirectoryName(targetPath)!;
            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
                if (createdDirs.Add(parentDir))
                {
                    records.Add(new OperationRecord(
                        OperationRecordKind.DirectoryCreated,
                        OriginalPath: null,
                        TargetPath: parentDir,
                        SizeBytes: 0));
                }
            }

            entry.ExtractToFile(targetPath, overwrite: false);
            var sha256 = await RollbackManager.ComputeFileSha256HexAsync(targetPath, cancellationToken).ConfigureAwait(false);
            var info = new FileInfo(targetPath);

            records.Add(new OperationRecord(
                OperationRecordKind.FileCreatedOrCopied,
                OriginalPath: null,
                TargetPath: targetPath,
                SizeBytes: info.Length,
                ContentSha256Hex: sha256));
            extracted++;
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return new SafeArchiveResult(extracted, skipped, preview.DestinationPath, manifest);
    }
}
