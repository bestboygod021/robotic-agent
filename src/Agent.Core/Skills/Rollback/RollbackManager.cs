using System.Security.Cryptography;

namespace Agent.Core.Skills.Rollback;

/// <summary>
/// Executes safe, verifiable transactional rollback (#20) for file operations.
/// Never deletes a created file if the user modified it after the skill ran,
/// and never overwrites an existing file when restoring a moved/renamed file.
/// </summary>
public sealed class RollbackManager
{
    public static async Task<string> ComputeFileSha256HexAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes);
    }

    public async Task<RollbackExecutionResult> UndoAsync(
        OperationManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var reverted = 0;
        var skipped = 0;
        var warnings = new List<string>();

        // Replay in reverse chronological order so files are reverted before created directories.
        for (var index = manifest.Records.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = manifest.Records[index];

            try
            {
                switch (record.Kind)
                {
                    case OperationRecordKind.FileCreatedOrCopied:
                    {
                        if (!File.Exists(record.TargetPath))
                        {
                            skipped++;
                            break;
                        }

                        var info = new FileInfo(record.TargetPath);
                        if (info.Length != record.SizeBytes)
                        {
                            skipped++;
                            warnings.Add($"فایل «{Path.GetFileName(record.TargetPath)}» پس از ایجاد تغییر کرده است و برای ایمنی حذف نشد.");
                            break;
                        }

                        if (!string.IsNullOrWhiteSpace(record.ContentSha256Hex))
                        {
                            var currentHash = await ComputeFileSha256HexAsync(record.TargetPath, cancellationToken).ConfigureAwait(false);
                            if (!string.Equals(currentHash, record.ContentSha256Hex, StringComparison.OrdinalIgnoreCase))
                            {
                                skipped++;
                                warnings.Add($"محتوای فایل «{Path.GetFileName(record.TargetPath)}» تغییر کرده است و حذف نشد.");
                                break;
                            }
                        }

                        File.Delete(record.TargetPath);
                        reverted++;
                        break;
                    }

                    case OperationRecordKind.FileMovedOrRenamed:
                    {
                        if (string.IsNullOrWhiteSpace(record.OriginalPath) || !File.Exists(record.TargetPath))
                        {
                            skipped++;
                            break;
                        }

                        if (File.Exists(record.OriginalPath) || Directory.Exists(record.OriginalPath))
                        {
                            skipped++;
                            warnings.Add($"مسیر اصلی «{Path.GetFileName(record.OriginalPath)}» هم‌اکنون پر است؛ بازگردانی بدون بازنویسی متوقف شد.");
                            break;
                        }

                        var parentDir = Path.GetDirectoryName(record.OriginalPath);
                        if (!string.IsNullOrWhiteSpace(parentDir))
                        {
                            Directory.CreateDirectory(parentDir);
                        }

                        File.Move(record.TargetPath, record.OriginalPath, overwrite: false);
                        reverted++;
                        break;
                    }

                    case OperationRecordKind.DirectoryCreated:
                    {
                        if (Directory.Exists(record.TargetPath) &&
                            !Directory.EnumerateFileSystemEntries(record.TargetPath).Any())
                        {
                            Directory.Delete(record.TargetPath, recursive: false);
                            reverted++;
                        }
                        else
                        {
                            skipped++;
                        }

                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped++;
                warnings.Add($"خطا در بازگردانی «{Path.GetFileName(record.TargetPath)}»: {ex.Message}");
            }
        }

        return new RollbackExecutionResult(manifest.OperationId, reverted, skipped, warnings);
    }
}
