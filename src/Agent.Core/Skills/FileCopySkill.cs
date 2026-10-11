using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Resources;
using Agent.Core.Skills.Rollback;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills;

/// <summary>
/// Safe copy skill supporting top-level or bounded-depth recursive traversal (#11),
/// flexible non-destructive conflict policies (#17), deceptive double-extension
/// blocking (#74), and transactional rollback manifests (#20).
/// </summary>
public sealed class FileCopySkill : ISkill<FileCopyRequest, FileCopyPreview, FileCopyExecutionResult>
{
    public const string SkillId = "file.copy";
    public const int MaximumFilesPerRun = 500;
    public const int DefaultSubdirectoryDepth = 5;
    public const int MaximumSubdirectoryDepth = 10;
    public const long MaximumCopyBytesPerSecond = 10L * 1024 * 1024;
    private const int BufferSize = 64 * 1024;

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "کپی امن فایل‌ها",
            ["en"] = "Safe File Copy",
            ["zh"] = "安全文件复制",
            ["es"] = "Copia Segura de Archivos",
            ["ar"] = "نسخ آمن للملفات",
            ["fr"] = "Copie Sécurisée de Fichiers"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "کپی فایل‌های هم‌پسوند در پوشهٔ مقصد با پیش‌نمایش، پیمایش امن زیرپوشه‌ها، سیاست برخورد بدون بازنویسی و قابلیت واگرد (Undo).",
            ["en"] = "Copies matching files with mandatory preview, bounded subfolder traversal, non-overwriting conflict resolution, and transactional Undo.",
            ["zh"] = "带强制预览、受限子目录遍历、无损冲突处理与事务级撤销的文件安全复制。",
            ["es"] = "Copia archivos con vista previa obligatoria, recorrido seguro de subcarpetas, resolución de conflictos sin sobrescribir y deshacer.",
            ["ar"] = "نسخ الملفات مع معاينة إلزامية، واجتياز آمن للمجلدات الفرعية، ومعالجة التعارض دون استبدال، وإمكانية التراجع.",
            ["fr"] = "Copie sécurisée avec aperçu obligatoire, parcours limité des sous-dossiers, gestion des conflits sans écrasement et annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public Task<FileCopyExecutionResult> ExecuteAsync(
        FileCopyRequest request,
        FileCopyPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExecuteAsync(
            request,
            approvedPreview,
            progress: null,
            context.CancellationToken,
            context.MaxDiskIoBytesPerSecond);
    }

    private static readonly Regex SafeExtension = new(
        @"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,11}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public FileCopyPreview Preview(FileCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceDirectory = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "مبدأ");
        var destinationDirectory = PathGuard.ValidateSelectedFolder(request.DestinationDirectory, "مقصد");
        if (PathGuard.PathsEqual(sourceDirectory, destinationDirectory))
        {
            throw new InvalidOperationException("پوشهٔ مبدأ و مقصد نمی‌توانند یکسان باشند.");
        }

        var maxDepth = request.IncludeSubdirectories
            ? Math.Clamp(request.MaxDepth <= 0 ? DefaultSubdirectoryDepth : request.MaxDepth, 1, MaximumSubdirectoryDepth)
            : 0;

        if (request.IncludeSubdirectories && AreDirectoriesNested(sourceDirectory, destinationDirectory))
        {
            throw new InvalidOperationException("هنگام پیمایش زیرپوشه‌ها، پوشهٔ مبدأ و مقصد نمی‌توانند داخل یکدیگر باشند.");
        }

        var extension = NormalizeExtension(request.Extension);
        var searchPattern = extension == "*" ? "*" : $"*.{extension}";
        var comparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var candidates = new List<FileCopyCandidate>();
        var reservedDestinationPaths = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        EnumerateSafeFiles(
            sourceRoot: sourceDirectory,
            currentDirectory: sourceDirectory,
            destinationRoot: destinationDirectory,
            searchPattern: searchPattern,
            currentDepth: 0,
            maxDepth: maxDepth,
            conflictPolicy: request.ConflictPolicy,
            reservedDestinationPaths: reservedDestinationPaths,
            candidates: candidates);

        var ordered = candidates
            .OrderBy(candidate => string.IsNullOrEmpty(candidate.RelativePath) ? candidate.Name : candidate.RelativePath, comparison)
            .ToArray();

        var requiredBytes = ordered.Where(candidate => !candidate.AlreadyExists).Sum(candidate => candidate.SizeBytes);
        var (destinationAvailableBytes, hasDiskHeadroom) = AssessDestinationSpace(destinationDirectory, requiredBytes);
        return new FileCopyPreview(
            sourceDirectory,
            destinationDirectory,
            extension,
            ordered,
            destinationAvailableBytes,
            hasDiskHeadroom,
            request.IncludeSubdirectories,
            maxDepth,
            request.ConflictPolicy);
    }

    public async Task<FileCopyExecutionResult> ExecuteAsync(
        FileCopyRequest request,
        FileCopyPreview approvedPreview,
        IProgress<FileCopyProgress>? progress,
        CancellationToken cancellationToken,
        long maxBytesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        if (approvedPreview.ReadyCount == 0)
        {
            throw new InvalidOperationException("پیش‌نمایش تأییدشده فایل آمادهٔ کپی ندارد یا فضای دیسک کافی نیست.");
        }

        if (maxBytesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytesPerSecond), "A positive disk I/O budget is required.");
        }

        var effectiveBytesPerSecond = Math.Min(maxBytesPerSecond, MaximumCopyBytesPerSecond);

        // Revalidate immediately before acting so a stale preview cannot silently run.
        var currentPreview = Preview(request);
        if (!currentPreview.HasDiskHeadroom)
        {
            throw new InvalidOperationException("فضای آزاد مقصد برای حفظ حداقل ذخیرهٔ دیسک کافی نیست؛ کپی متوقف شد.");
        }

        if (!PreviewsMatch(approvedPreview, currentPreview))
        {
            throw new InvalidOperationException("محتوای پوشه از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var rateLimiter = new FileCopyRateLimiter(effectiveBytesPerSecond);
        var copied = 0;
        var skipped = currentPreview.ExistingCount;
        var failures = new List<string>();
        var completed = 0;
        var cancelled = false;
        var rollbackRecords = new List<OperationRecord>();
        var createdDirectories = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var candidate in currentPreview.Candidates.Where(item => !item.AlreadyExists))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var targetDirectory = Path.GetDirectoryName(candidate.DestinationPath) ?? currentPreview.DestinationDirectory;
            var temporaryPath = Path.Combine(
                targetDirectory,
                $".robotic-agent-{Guid.NewGuid():N}.tmp");

            try
            {
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                    if (createdDirectories.Add(targetDirectory))
                    {
                        rollbackRecords.Add(new OperationRecord(
                            OperationRecordKind.DirectoryCreated,
                            OriginalPath: null,
                            TargetPath: targetDirectory,
                            SizeBytes: 0));
                    }
                }

                if (File.Exists(candidate.DestinationPath) || Directory.Exists(candidate.DestinationPath))
                {
                    skipped++;
                    continue;
                }

                var sourceAttributes = File.GetAttributes(candidate.SourcePath);
                if ((sourceAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.System)) != 0)
                {
                    failures.Add(candidate.Name);
                    continue;
                }

                var (_, hasDiskHeadroom) = AssessDestinationSpace(
                    currentPreview.DestinationDirectory,
                    candidate.SizeBytes);
                if (!hasDiskHeadroom)
                {
                    failures.Add(candidate.Name);
                    continue;
                }

                string sha256Hex;
                await using (var input = new FileStream(
                    candidate.SourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    sha256Hex = await CopyThrottledAndHashAsync(input, output, rateLimiter, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);

                    if (input.Length != candidate.SizeBytes || output.Length != candidate.SizeBytes)
                    {
                        throw new IOException("فایل هنگام کپی تغییر کرده یا کامل خوانده نشده است.");
                    }
                }

                // File.Move without overwrite protects against a collision after preview.
                File.Move(temporaryPath, candidate.DestinationPath);
                copied++;
                rollbackRecords.Add(new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: candidate.SourcePath,
                    TargetPath: candidate.DestinationPath,
                    SizeBytes: candidate.SizeBytes,
                    ContentSha256Hex: sha256Hex));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }
            catch (IOException) when (File.Exists(candidate.DestinationPath) || Directory.Exists(candidate.DestinationPath))
            {
                skipped++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                failures.Add(candidate.Name);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // A leftover temp file is safer than hiding the original operation result.
                }

                completed++;
                progress?.Report(new FileCopyProgress(completed, currentPreview.ReadyCount, candidate.Name));
            }
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: rollbackRecords);

        return new FileCopyExecutionResult(copied, skipped, failures, cancelled, manifest);
    }

    private static void EnumerateSafeFiles(
        string sourceRoot,
        string currentDirectory,
        string destinationRoot,
        string searchPattern,
        int currentDepth,
        int maxDepth,
        FileConflictPolicy conflictPolicy,
        HashSet<string> reservedDestinationPaths,
        List<FileCopyCandidate> candidates)
    {
        foreach (var path in Directory.EnumerateFiles(currentDirectory, searchPattern, SearchOption.TopDirectoryOnly))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
            {
                continue;
            }

            if (PathGuard.IsSuspiciousDoubleExtension(path, out _))
            {
                continue;
            }

            if (candidates.Count >= MaximumFilesPerRun)
            {
                throw new InvalidOperationException(
                    $"این نسخه در هر اجرا حداکثر {MaximumFilesPerRun} فایل را کپی می‌کند؛ فیلتر را محدودتر کنید.");
            }

            var fileInfo = new FileInfo(path);
            var relativePath = Path.GetRelativePath(sourceRoot, path);
            var baseDestinationPath = Path.Combine(destinationRoot, relativePath);
            var (resolvedDestinationPath, alreadyExists, wasAutoRenamed) = ResolveConflictDestination(
                fileInfo,
                baseDestinationPath,
                conflictPolicy,
                reservedDestinationPaths);

            reservedDestinationPaths.Add(resolvedDestinationPath);
            var displayName = currentDepth == 0 ? fileInfo.Name : relativePath;
            candidates.Add(new FileCopyCandidate(
                SourcePath: path,
                Name: displayName,
                SizeBytes: fileInfo.Length,
                DestinationPath: resolvedDestinationPath,
                AlreadyExists: alreadyExists,
                RelativePath: relativePath,
                WasAutoRenamed: wasAutoRenamed));
        }

        if (currentDepth >= maxDepth)
        {
            return;
        }

        foreach (var subDir in Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var dirAttributes = File.GetAttributes(subDir);
            if ((dirAttributes & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
            {
                continue;
            }

            EnumerateSafeFiles(
                sourceRoot,
                subDir,
                destinationRoot,
                searchPattern,
                currentDepth + 1,
                maxDepth,
                conflictPolicy,
                reservedDestinationPaths,
                candidates);
        }
    }

    private static (string DestinationPath, bool AlreadyExists, bool WasAutoRenamed) ResolveConflictDestination(
        FileInfo sourceInfo,
        string baseDestinationPath,
        FileConflictPolicy conflictPolicy,
        HashSet<string> reservedDestinationPaths)
    {
        var collisionExists = File.Exists(baseDestinationPath) ||
                              Directory.Exists(baseDestinationPath) ||
                              reservedDestinationPaths.Contains(baseDestinationPath);

        if (!collisionExists)
        {
            return (baseDestinationPath, false, false);
        }

        if (conflictPolicy == FileConflictPolicy.SkipExisting)
        {
            return (baseDestinationPath, true, false);
        }

        if (conflictPolicy == FileConflictPolicy.CopyOnlyIfNewerWithSuffix && File.Exists(baseDestinationPath))
        {
            var existingWriteUtc = File.GetLastWriteTimeUtc(baseDestinationPath);
            if (sourceInfo.LastWriteTimeUtc <= existingWriteUtc)
            {
                return (baseDestinationPath, true, false);
            }
        }

        var directory = Path.GetDirectoryName(baseDestinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(baseDestinationPath);
        var extension = Path.GetExtension(baseDestinationPath);

        for (var counter = 1; counter <= 9999; counter++)
        {
            var numberedName = $"{stem} ({counter}){extension}";
            var candidatePath = Path.Combine(directory, numberedName);
            if (!File.Exists(candidatePath) &&
                !Directory.Exists(candidatePath) &&
                !reservedDestinationPaths.Contains(candidatePath))
            {
                return (candidatePath, false, true);
            }
        }

        return (baseDestinationPath, true, false);
    }

    private static bool AreDirectoriesNested(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normFirst = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)) + Path.DirectorySeparatorChar;
        var normSecond = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)) + Path.DirectorySeparatorChar;
        return normFirst.StartsWith(normSecond, comparison) || normSecond.StartsWith(normFirst, comparison);
    }

    private static async Task<string> CopyThrottledAndHashAsync(
        Stream input,
        Stream output,
        FileCopyRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.Sha256);
        try
        {
            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                sha256.AppendData(buffer.AsSpan(0, bytesRead));
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                await rateLimiter.WaitIfAheadAsync(bytesRead, cancellationToken).ConfigureAwait(false);
            }

            return Convert.ToHexString(sha256.GetHashAndReset());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static (long AvailableBytes, bool HasHeadroom) AssessDestinationSpace(
        string destinationDirectory,
        long requiredBytes)
    {
        var root = Path.GetPathRoot(destinationDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return (0, false);
        }

        try
        {
            var drive = new DriveInfo(root);
            if (!drive.IsReady || (drive.DriveType is not DriveType.Fixed and not DriveType.Removable))
            {
                return (0, false);
            }

            var volume = new StorageVolumeInfo(
                drive.Name,
                drive.DriveType.ToString(),
                drive.DriveFormat,
                drive.TotalSize,
                drive.AvailableFreeSpace,
                IsSystemVolume: false);
            return (volume.AvailableBytes, new ResourceGovernor().HasWriteHeadroom(volume, requiredBytes));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (0, false);
        }
    }

    private sealed class FileCopyRateLimiter
    {
        private readonly long _maximumBytesPerSecond;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _transferredBytes;

        public FileCopyRateLimiter(long maximumBytesPerSecond)
        {
            _maximumBytesPerSecond = maximumBytesPerSecond;
        }

        public async Task WaitIfAheadAsync(long newlyTransferredBytes, CancellationToken cancellationToken)
        {
            _transferredBytes = checked(_transferredBytes + newlyTransferredBytes);
            var expectedSeconds = _transferredBytes / (double)_maximumBytesPerSecond;
            var delaySeconds = expectedSeconds - _stopwatch.Elapsed.TotalSeconds;
            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static string NormalizeExtension(string extension)
    {
        var value = (extension ?? string.Empty).Trim();
        if (value is "*" or "*.*")
        {
            return "*";
        }

        if (value.StartsWith("*.", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        else if (value.Length > 0 && value[0] == '.')
        {
            value = value[1..];
        }

        if (!SafeExtension.IsMatch(value))
        {
            throw new ArgumentException("پسوند نامعتبر است؛ فقط حروف و عدد وارد کنید یا * را برای همهٔ فایل‌ها بنویسید.", nameof(extension));
        }

        return value.ToLowerInvariant();
    }

    private static bool PreviewsMatch(FileCopyPreview first, FileCopyPreview second)
    {
        if (!PathGuard.PathsEqual(first.SourceDirectory, second.SourceDirectory) ||
            !PathGuard.PathsEqual(first.DestinationDirectory, second.DestinationDirectory) ||
            !string.Equals(first.Extension, second.Extension, StringComparison.OrdinalIgnoreCase) ||
            first.IncludeSubdirectories != second.IncludeSubdirectories ||
            first.MaxDepth != second.MaxDepth ||
            first.ConflictPolicy != second.ConflictPolicy ||
            first.Candidates.Count != second.Candidates.Count)
        {
            return false;
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        for (var index = 0; index < first.Candidates.Count; index++)
        {
            var left = first.Candidates[index];
            var right = second.Candidates[index];
            if (!string.Equals(left.SourcePath, right.SourcePath, pathComparison) ||
                !string.Equals(left.DestinationPath, right.DestinationPath, pathComparison) ||
                left.SizeBytes != right.SizeBytes ||
                left.AlreadyExists != right.AlreadyExists)
            {
                return false;
            }
        }

        return true;
    }
}
