using Agent.Core.Security;
using Agent.Core.Resources;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills;

/// <summary>
/// Safe, top-level-only copy skill for the first Windows MVP. It does not recurse,
/// delete, move, overwrite, execute files, or follow reparse-point files.
/// </summary>
public sealed class FileCopySkill
{
    public const int MaximumFilesPerRun = 500;
    public const long MaximumCopyBytesPerSecond = 10L * 1024 * 1024;
    private const int BufferSize = 64 * 1024;

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

        var extension = NormalizeExtension(request.Extension);
        var searchPattern = extension == "*" ? "*" : $"*.{extension}";
        var comparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var candidates = new List<FileCopyCandidate>();

        foreach (var path in Directory.EnumerateFiles(sourceDirectory, searchPattern, SearchOption.TopDirectoryOnly))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
            {
                continue;
            }

            if (candidates.Count >= MaximumFilesPerRun)
            {
                throw new InvalidOperationException(
                    $"این نسخه در هر اجرا حداکثر {MaximumFilesPerRun} فایل را کپی می‌کند؛ فیلتر را محدودتر کنید.");
            }

            var name = Path.GetFileName(path);
            var destinationPath = Path.Combine(destinationDirectory, name);
            var exists = File.Exists(destinationPath) || Directory.Exists(destinationPath);
            candidates.Add(new FileCopyCandidate(path, name, new FileInfo(path).Length, destinationPath, exists));
        }

        var ordered = candidates
            .OrderBy(candidate => candidate.Name, comparison)
            .ToArray();

        var requiredBytes = ordered.Where(candidate => !candidate.AlreadyExists).Sum(candidate => candidate.SizeBytes);
        var (destinationAvailableBytes, hasDiskHeadroom) = AssessDestinationSpace(destinationDirectory, requiredBytes);
        return new FileCopyPreview(
            sourceDirectory,
            destinationDirectory,
            extension,
            ordered,
            destinationAvailableBytes,
            hasDiskHeadroom);
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

        foreach (var candidate in currentPreview.Candidates.Where(item => !item.AlreadyExists))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var temporaryPath = Path.Combine(
                currentPreview.DestinationDirectory,
                $".robotic-agent-{Guid.NewGuid():N}.tmp");

            try
            {
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
                    await CopyThrottledAsync(input, output, rateLimiter, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);

                    if (input.Length != candidate.SizeBytes || output.Length != candidate.SizeBytes)
                    {
                        throw new IOException("فایل هنگام کپی تغییر کرده یا کامل خوانده نشده است.");
                    }
                }

                // File.Move without overwrite protects against a collision after preview.
                File.Move(temporaryPath, candidate.DestinationPath);
                copied++;
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

        return new FileCopyExecutionResult(copied, skipped, failures, cancelled);
    }

    private static async Task CopyThrottledAsync(
        Stream input,
        Stream output,
        FileCopyRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        try
        {
            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                await rateLimiter.WaitIfAheadAsync(bytesRead, cancellationToken).ConfigureAwait(false);
            }
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

    private static bool PreviewsMatch(FileCopyPreview approved, FileCopyPreview current)
    {
        if (!PathGuard.PathsEqual(approved.SourceDirectory, current.SourceDirectory) ||
            !PathGuard.PathsEqual(approved.DestinationDirectory, current.DestinationDirectory) ||
            !string.Equals(approved.Extension, current.Extension, StringComparison.OrdinalIgnoreCase) ||
            approved.HasDiskHeadroom != current.HasDiskHeadroom ||
            approved.Candidates.Count != current.Candidates.Count)
        {
            return false;
        }

        for (var index = 0; index < approved.Candidates.Count; index++)
        {
            var first = approved.Candidates[index];
            var second = current.Candidates[index];
            if (!string.Equals(first.Name, second.Name, StringComparison.Ordinal) ||
                first.SizeBytes != second.SizeBytes ||
                first.AlreadyExists != second.AlreadyExists)
            {
                return false;
            }
        }

        return true;
    }
}
