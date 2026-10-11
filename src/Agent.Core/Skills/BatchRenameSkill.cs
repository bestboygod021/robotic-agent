using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills;

public sealed record BatchRenameRequest(
    string DirectoryPath,
    string Extension = "*",
    string? Prefix = null,
    string? Suffix = null,
    string? ReplaceFrom = null,
    string? ReplaceTo = null,
    bool IncludeSequenceNumber = true,
    int StartSequenceNumber = 1,
    int NumberPadding = 2,
    bool IncludeModifiedDatePrefix = false);

public sealed record BatchRenameCandidate(
    string OriginalPath,
    string OriginalName,
    string NewPath,
    string NewName,
    long SizeBytes,
    bool HasCollision);

public sealed record BatchRenamePreview(
    string DirectoryPath,
    IReadOnlyList<BatchRenameCandidate> Candidates)
{
    public int ReadyCount => Candidates.Count(item => !item.HasCollision && !string.Equals(item.OriginalName, item.NewName, StringComparison.Ordinal));
    public int CollisionCount => Candidates.Count(item => item.HasCollision);
}

public sealed record BatchRenameResult(
    int RenamedCount,
    int SkippedCount,
    IReadOnlyList<string> FailedFileNames,
    bool Cancelled,
    OperationManifest RollbackManifest);

/// <summary>
/// Smart batch rename skill (#12) with mandatory old->new preview table,
/// collision detection, invalid filename character stripping, and full Undo manifest.
/// </summary>
public sealed class BatchRenameSkill : ISkill<BatchRenameRequest, BatchRenamePreview, BatchRenameResult>
{
    public const string SkillId = "file.rename";
    public const int MaximumFilesPerRun = 500;
    private static readonly Regex UnsafeFileNameChars = new(@"[<>:""/\\|?*\x00-\x1F]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تغییر نام دسته‌ای هوشمند",
            ["en"] = "Smart Batch Rename",
            ["zh"] = "智能批量重命名",
            ["es"] = "Renombrado Masivo Inteligente",
            ["ar"] = "إعادة تسمية جماعية ذكية",
            ["fr"] = "Renommage par Lots Intelligent"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تغییر نام گروهی فایل‌ها بر اساس پیشوند، پسوند، جایگزینی متن، تاریخ و شماره‌گذاری همراه با جدول پیش‌نمایش و قابلیت واگرد (Undo).",
            ["en"] = "Batch renames files using prefix, suffix, text replacement, date, and sequence numbering with preview and Undo.",
            ["zh"] = "按前缀、后缀、文本替换、日期和序号批量重命名文件，支持预览与一键撤销。",
            ["es"] = "Renombra archivos por lotes con prefijo, sufijo, reemplazo, fecha y numeración con vista previa y deshacer.",
            ["ar"] = "إعادة تسمية الملفات دفعة واحدة باستخدام البادئة واللاحقة والتاريخ والترقيم مع معاينة وتراجع.",
            ["fr"] = "Renomme des fichiers par lots avec préfixe, suffixe, remplacement, date et numérotation avec aperçu et annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public BatchRenamePreview Preview(BatchRenameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var directory = PathGuard.ValidateSelectedFolder(request.DirectoryPath, "تغییر نام");
        var extension = FileCopySkill.NormalizeExtension(request.Extension);
        var searchPattern = extension == "*" ? "*" : $"*.{extension}";
        var comparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var files = Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var attrs = File.GetAttributes(path);
                return (attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) == 0 &&
                       !PathGuard.IsSuspiciousDoubleExtension(path, out _);
            })
            .OrderBy(Path.GetFileName, comparison)
            .ToArray();

        if (files.Length > MaximumFilesPerRun)
        {
            throw new InvalidOperationException($"در هر نوبت حداکثر {MaximumFilesPerRun} فایل قابل تغییر نام است.");
        }

        var reservedTargetNames = new HashSet<string>(comparison);
        var candidates = new List<BatchRenameCandidate>(files.Length);
        var sequence = Math.Max(0, request.StartSequenceNumber);
        var padding = Math.Clamp(request.NumberPadding, 1, 6);

        foreach (var filePath in files)
        {
            var info = new FileInfo(filePath);
            var originalName = info.Name;
            var stem = Path.GetFileNameWithoutExtension(originalName);
            var ext = info.Extension;

            if (!string.IsNullOrEmpty(request.ReplaceFrom))
            {
                stem = stem.Replace(request.ReplaceFrom, request.ReplaceTo ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            var prefix = SanitizeNameSegment(request.Prefix);
            var suffix = SanitizeNameSegment(request.Suffix);
            var datePrefix = request.IncludeModifiedDatePrefix
                ? info.LastWriteTimeUtc.ToString("yyyy-MM-dd_", CultureInfo.InvariantCulture)
                : string.Empty;
            var seqSuffix = request.IncludeSequenceNumber
                ? $"_{sequence.ToString($"D{padding}", CultureInfo.InvariantCulture)}"
                : string.Empty;

            var newStem = SanitizeNameSegment($"{datePrefix}{prefix}{stem}{suffix}{seqSuffix}");
            if (string.IsNullOrWhiteSpace(newStem))
            {
                newStem = $"file_{sequence.ToString($"D{padding}", CultureInfo.InvariantCulture)}";
            }

            var newName = $"{newStem}{ext}";
            var newPath = Path.Combine(directory, newName);
            var unchanged = string.Equals(originalName, newName, comparison);
            var collision = !unchanged &&
                (File.Exists(newPath) || Directory.Exists(newPath) || !reservedTargetNames.Add(newName));

            candidates.Add(new BatchRenameCandidate(
                OriginalPath: filePath,
                OriginalName: originalName,
                NewPath: newPath,
                NewName: newName,
                SizeBytes: info.Length,
                HasCollision: collision));

            sequence++;
        }

        return new BatchRenamePreview(directory, candidates);
    }

    public Task<BatchRenameResult> ExecuteAsync(
        BatchRenameRequest request,
        BatchRenamePreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        if (approvedPreview.ReadyCount == 0)
        {
            throw new InvalidOperationException("پیش‌نمایش تأییدشده هیچ فایل آمادهٔ تغییر نام ندارد.");
        }

        var currentPreview = Preview(request);
        if (!PreviewsMatch(approvedPreview, currentPreview))
        {
            throw new InvalidOperationException("محتوای پوشه از زمان پیش‌نمایش تغییر کرده است؛ دوباره پیش‌نمایش بگیرید.");
        }

        var renamed = 0;
        var skipped = currentPreview.CollisionCount;
        var failed = new List<string>();
        var cancelled = false;
        var records = new List<OperationRecord>();

        foreach (var item in currentPreview.Candidates)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            if (item.HasCollision || string.Equals(item.OriginalName, item.NewName, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                if (File.Exists(item.NewPath) || Directory.Exists(item.NewPath))
                {
                    skipped++;
                    continue;
                }

                File.Move(item.OriginalPath, item.NewPath, overwrite: false);
                renamed++;
                records.Add(new OperationRecord(
                    OperationRecordKind.FileMovedOrRenamed,
                    OriginalPath: item.OriginalPath,
                    TargetPath: item.NewPath,
                    SizeBytes: item.SizeBytes));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(item.OriginalName);
            }
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return Task.FromResult(new BatchRenameResult(renamed, skipped, failed, cancelled, manifest));
    }

    private static string SanitizeNameSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.Empty;
        }

        return UnsafeFileNameChars.Replace(segment.Trim(), "_");
    }

    private static bool PreviewsMatch(BatchRenamePreview first, BatchRenamePreview second)
    {
        if (!PathGuard.PathsEqual(first.DirectoryPath, second.DirectoryPath) ||
            first.Candidates.Count != second.Candidates.Count)
        {
            return false;
        }

        for (var i = 0; i < first.Candidates.Count; i++)
        {
            var left = first.Candidates[i];
            var right = second.Candidates[i];
            if (left.OriginalName != right.OriginalName ||
                left.NewName != right.NewName ||
                left.SizeBytes != right.SizeBytes ||
                left.HasCollision != right.HasCollision)
            {
                return false;
            }
        }

        return true;
    }
}
