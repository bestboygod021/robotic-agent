using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Globalization;

namespace Agent.Core.Skills;

public enum BackupCandidateState
{
    NewFile,
    ModifiedFile,
    Unchanged
}

public sealed record IncrementalBackupRequest(
    string SourceDirectory,
    string BackupDirectory,
    string Extension = "*",
    bool IncludeSubdirectories = true,
    int MaxDepth = 5);

public sealed record IncrementalBackupCandidate(
    string SourcePath,
    string RelativePath,
    string DestinationPath,
    long SizeBytes,
    BackupCandidateState State);

public sealed record IncrementalBackupPreview(
    string SourceDirectory,
    string BackupDirectory,
    IReadOnlyList<IncrementalBackupCandidate> Candidates)
{
    public int NewCount => Candidates.Count(c => c.State == BackupCandidateState.NewFile);
    public int ModifiedCount => Candidates.Count(c => c.State == BackupCandidateState.ModifiedFile);
    public int UnchangedSkippedCount => Candidates.Count(c => c.State == BackupCandidateState.Unchanged);
    public int ReadyCount => NewCount + ModifiedCount;
}

public sealed record IncrementalBackupResult(
    int BackedUpCount,
    int UnchangedSkippedCount,
    IReadOnlyList<string> FailedFileNames,
    bool Cancelled,
    OperationManifest RollbackManifest);

/// <summary>
/// Incremental snapshot backup skill (#19) that copies only new or modified
/// files while preserving existing backup versions (never overwriting in-place)
/// and generating a full RollbackManager Undo manifest (#20).
/// </summary>
public sealed class IncrementalBackupSkill : ISkill<IncrementalBackupRequest, IncrementalBackupPreview, IncrementalBackupResult>
{
    public const string SkillId = "file.backup";
    public const int MaximumFilesPerRun = 500;

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "پشتیبان‌گیری افزایشی هوشمند",
            ["en"] = "Smart Incremental Backup",
            ["zh"] = "智能增量备份",
            ["es"] = "Copia de Seguridad Incremental Inteligente",
            ["ar"] = "نسخ احتياطي تزايدي ذكي",
            ["fr"] = "Sauvegarde Incrémentale Intelligente"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "پشتیبان‌گیری از فایل‌های جدید یا تغییریافته بدون کپی مجدد فایل‌های بدون تغییر، همراه با حفظ نسخه‌های قبلی و قابلیت واگرد.",
            ["en"] = "Backs up only new or modified files, skips unchanged files, preserves prior versions without overwriting, and supports Undo.",
            ["zh"] = "仅备份新增或修改过的文件，跳过未变更文件，保留历史版本且支持一键撤销。",
            ["es"] = "Respalda solo archivos nuevos o modificados, omite los sin cambios y conserva versiones anteriores con opción de deshacer.",
            ["ar"] = "ينسخ احتياطياً الملفات الجديدة أو المعدلة فقط ويتخطى الملفات غير المتغيرة مع حفظ النسخ السابقة وإمكانية التراجع.",
            ["fr"] = "Sauvegarde uniquement les fichiers nouveaux ou modifiés sans écraser les versions existantes, avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public IncrementalBackupPreview Preview(IncrementalBackupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceRoot = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "مبدأ پشتیبان‌گیری");
        var backupRoot = PathGuard.ValidateSelectedFolder(request.BackupDirectory, "مقصد پشتیبان‌گیری");
        if (PathGuard.PathsEqual(sourceRoot, backupRoot))
        {
            throw new InvalidOperationException("پوشهٔ مبدأ و مقصد پشتیبان‌گیری نمی‌توانند یکسان باشند.");
        }

        var ext = FileCopySkill.NormalizeExtension(request.Extension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var maxDepth = request.IncludeSubdirectories
            ? Math.Clamp(request.MaxDepth <= 0 ? 5 : request.MaxDepth, 1, 10)
            : 0;

        var candidates = new List<IncrementalBackupCandidate>();
        ScanDirectory(sourceRoot, sourceRoot, backupRoot, pattern, 0, maxDepth, candidates);
        return new IncrementalBackupPreview(sourceRoot, backupRoot, candidates);
    }

    public async Task<IncrementalBackupResult> ExecuteAsync(
        IncrementalBackupRequest request,
        IncrementalBackupPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        if (approvedPreview.ReadyCount == 0)
        {
            return new IncrementalBackupResult(
                0,
                approvedPreview.UnchangedSkippedCount,
                [],
                false,
                new OperationManifest(Guid.NewGuid().ToString("N"), SkillId, DateTimeOffset.UtcNow, []));
        }

        var currentPreview = Preview(request);
        if (currentPreview.ReadyCount != approvedPreview.ReadyCount ||
            currentPreview.Candidates.Count != approvedPreview.Candidates.Count)
        {
            throw new InvalidOperationException("وضعیت فایل‌ها از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var backedUp = 0;
        var failed = new List<string>();
        var cancelled = false;
        var records = new List<OperationRecord>();
        var createdDirs = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var item in currentPreview.Candidates.Where(c => c.State != BackupCandidateState.Unchanged))
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
                    if (createdDirs.Add(targetDir))
                    {
                        records.Add(new OperationRecord(
                            OperationRecordKind.DirectoryCreated,
                            OriginalPath: null,
                            TargetPath: targetDir,
                            SizeBytes: 0));
                    }
                }

                File.Copy(item.SourcePath, item.DestinationPath, overwrite: false);
                File.SetLastWriteTimeUtc(item.DestinationPath, File.GetLastWriteTimeUtc(item.SourcePath));
                var sha256 = await RollbackManager.ComputeFileSha256HexAsync(item.DestinationPath, context.CancellationToken).ConfigureAwait(false);

                records.Add(new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: item.SourcePath,
                    TargetPath: item.DestinationPath,
                    SizeBytes: item.SizeBytes,
                    ContentSha256Hex: sha256));
                backedUp++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(item.RelativePath);
            }
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return new IncrementalBackupResult(
            backedUp,
            currentPreview.UnchangedSkippedCount,
            failed,
            cancelled,
            manifest);
    }

    private static void ScanDirectory(
        string sourceRoot,
        string currentDir,
        string backupRoot,
        string pattern,
        int depth,
        int maxDepth,
        List<IncrementalBackupCandidate> candidates)
    {
        foreach (var filePath in Directory.EnumerateFiles(currentDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            if (candidates.Count >= MaximumFilesPerRun)
            {
                throw new InvalidOperationException($"در هر نوبت حداکثر {MaximumFilesPerRun} فایل پشتیبان‌گیری می‌شود.");
            }

            var sourceInfo = new FileInfo(filePath);
            var relativePath = Path.GetRelativePath(sourceRoot, filePath);
            var baseBackupPath = Path.Combine(backupRoot, relativePath);

            if (!File.Exists(baseBackupPath))
            {
                candidates.Add(new IncrementalBackupCandidate(
                    SourcePath: filePath,
                    RelativePath: relativePath,
                    DestinationPath: baseBackupPath,
                    SizeBytes: sourceInfo.Length,
                    State: BackupCandidateState.NewFile));
                continue;
            }

            var existingInfo = new FileInfo(baseBackupPath);
            var isUnchanged = existingInfo.Length == sourceInfo.Length &&
                              Math.Abs((sourceInfo.LastWriteTimeUtc - existingInfo.LastWriteTimeUtc).TotalSeconds) < 2.0;

            if (isUnchanged)
            {
                candidates.Add(new IncrementalBackupCandidate(
                    SourcePath: filePath,
                    RelativePath: relativePath,
                    DestinationPath: baseBackupPath,
                    SizeBytes: sourceInfo.Length,
                    State: BackupCandidateState.Unchanged));
                continue;
            }

            // Never overwrite existing backup in-place: write a timestamped snapshot copy.
            var dir = Path.GetDirectoryName(baseBackupPath) ?? backupRoot;
            var stem = Path.GetFileNameWithoutExtension(baseBackupPath);
            var ext = Path.GetExtension(baseBackupPath);
            var stamp = sourceInfo.LastWriteTimeUtc.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var snapshotPath = Path.Combine(dir, $"{stem}_v{stamp}{ext}");
            var counter = 1;
            while (File.Exists(snapshotPath))
            {
                snapshotPath = Path.Combine(dir, $"{stem}_v{stamp}_{counter}{ext}");
                counter++;
            }

            candidates.Add(new IncrementalBackupCandidate(
                SourcePath: filePath,
                RelativePath: relativePath,
                DestinationPath: snapshotPath,
                SizeBytes: sourceInfo.Length,
                State: BackupCandidateState.ModifiedFile));
        }

        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var subDir in Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly))
        {
            var attrs = File.GetAttributes(subDir);
            if ((attrs & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
            {
                continue;
            }

            ScanDirectory(sourceRoot, subDir, backupRoot, pattern, depth + 1, maxDepth, candidates);
        }
    }
}
