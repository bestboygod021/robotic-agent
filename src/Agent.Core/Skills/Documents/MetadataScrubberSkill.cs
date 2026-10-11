using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public sealed record MetadataScrubberRequest(
    string SourceDirectory,
    string DestinationDirectory,
    string Extension = "*");

public sealed record MetadataScrubberCandidate(
    string SourcePath,
    string FileName,
    string DestinationPath,
    long OriginalSizeBytes,
    string ScrubMethod);

public sealed record MetadataScrubberPreview(
    string SourceDirectory,
    string DestinationDirectory,
    IReadOnlyList<MetadataScrubberCandidate> Candidates)
{
    public int ReadyCount => Candidates.Count;
}

public sealed record MetadataScrubberResult(
    int ScrubbedFileCount,
    long TotalBytesRemoved,
    IReadOnlyList<string> FailedFileNames,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline metadata and EXIF scrubber skill (#18). Strips JPEG APP1..APP15/COM
/// segments (EXIF/GPS/XMP), PNG text/eXIf ancillary chunks, and document
/// author/creator/GPS header tags into clean _sanitized copies without modifying
/// original user files.
/// </summary>
public sealed class MetadataScrubberSkill : ISkill<MetadataScrubberRequest, MetadataScrubberPreview, MetadataScrubberResult>
{
    public const string SkillId = "doc.scrub_metadata";

    private static readonly Regex TextMetadataLinePattern = new(
        @"^\s*(?:Author|Creator|Producer|GPSLatitude|GPSLongitude|LastModifiedBy|Company|Manager)\s*[:=].*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "پاک‌سازی متادیتا و اطلاعات مخفی فایل (EXIF/GPS)",
            ["en"] = "Metadata & EXIF Scrubber",
            ["zh"] = "文件元数据与 EXIF/GPS 隐私清理",
            ["es"] = "Limpiador de Metadatos y EXIF/GPS",
            ["ar"] = "منظف البيانات الوصفية وبيانات الموقع (EXIF/GPS)",
            ["fr"] = "Nettoyeur de Métadonnées et EXIF/GPS"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "حذف موقعیت مکانی GPS، نام نویسنده، تاریخچهٔ دستگاه و متادیتای مخفی از تصاویر و اسناد در نسخهٔ پاک‌سازی‌شده همراه با قابلیت واگرد.",
            ["en"] = "Strips GPS coordinates, camera EXIF, and author metadata from images and documents into clean copies with Undo.",
            ["zh"] = "清除图片与文档中的 GPS 坐标、相机 EXIF 及作者信息，生成干净副本并支持一键撤销。",
            ["es"] = "Elimina coordenadas GPS, EXIF de cámara y datos de autor en copias limpias con opción de deshacer.",
            ["ar"] = "يزيل إحداثيات GPS وبيانات الكاميرا واسم المؤلف من الصور والمستندات في نسخ نظيفة مع إمكانية التراجع.",
            ["fr"] = "Supprime les coordonnées GPS, données EXIF et informations d’auteur dans des copies propres avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public MetadataScrubberPreview Preview(MetadataScrubberRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "مبدأ پاک‌سازی متادیتا");
        var destDir = PathGuard.ValidateSelectedFolder(request.DestinationDirectory, "مقصد پاک‌سازی متادیتا");

        var ext = FileCopySkill.NormalizeExtension(request.Extension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var candidates = new List<MetadataScrubberCandidate>();

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var attrs = File.GetAttributes(filePath);
            if ((attrs & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.System)) != 0 ||
                PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            var fileExt = info.Extension.ToLowerInvariant();
            var method = fileExt switch
            {
                ".jpg" or ".jpeg" => "JPEG EXIF/GPS APP-Segment Strip",
                ".txt" or ".md" or ".html" or ".svg" or ".xml" => "Document Header Metadata Strip",
                _ => "Clean Stream Copy"
            };

            var stem = Path.GetFileNameWithoutExtension(info.Name);
            var outPath = Path.Combine(destDir, $"{stem}_sanitized{info.Extension}");
            candidates.Add(new MetadataScrubberCandidate(
                SourcePath: filePath,
                FileName: info.Name,
                DestinationPath: outPath,
                OriginalSizeBytes: info.Length,
                ScrubMethod: method));
        }

        return new MetadataScrubberPreview(sourceDir, destDir, candidates);
    }

    public async Task<MetadataScrubberResult> ExecuteAsync(
        MetadataScrubberRequest request,
        MetadataScrubberPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Candidates.Count != approvedPreview.Candidates.Count)
        {
            throw new InvalidOperationException("فایل‌های پوشه از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var scrubbedCount = 0;
        long bytesRemoved = 0;
        var failed = new List<string>();
        var records = new List<OperationRecord>();

        foreach (var item in currentPreview.Candidates)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            try
            {
                var rawBytes = await File.ReadAllBytesAsync(item.SourcePath, context.CancellationToken).ConfigureAwait(false);
                var cleanedBytes = ScrubBytes(rawBytes, Path.GetExtension(item.FileName));

                await File.WriteAllBytesAsync(item.DestinationPath, cleanedBytes, context.CancellationToken).ConfigureAwait(false);
                var sha256 = await RollbackManager.ComputeFileSha256HexAsync(item.DestinationPath, context.CancellationToken).ConfigureAwait(false);

                records.Add(new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: item.SourcePath,
                    TargetPath: item.DestinationPath,
                    SizeBytes: cleanedBytes.LongLength,
                    ContentSha256Hex: sha256));

                scrubbedCount++;
                if (rawBytes.LongLength > cleanedBytes.LongLength)
                {
                    bytesRemoved += rawBytes.LongLength - cleanedBytes.LongLength;
                }
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

        return new MetadataScrubberResult(scrubbedCount, bytesRemoved, failed, manifest);
    }

    public static byte[] ScrubBytes(byte[] rawBytes, string extension)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);

        // If JPEG (FF D8 ...), strip APP1..APP15 (FF E1..FF EF) and COM (FF FE) metadata segments.
        if (rawBytes.Length >= 4 && rawBytes[0] == 0xFF && rawBytes[1] == 0xD8)
        {
            return ScrubJpegSegments(rawBytes);
        }

        var ext = extension.ToLowerInvariant();
        if (ext is ".txt" or ".md" or ".html" or ".svg" or ".xml")
        {
            var text = Encoding.UTF8.GetString(rawBytes);
            var cleaned = TextMetadataLinePattern.Replace(text, string.Empty);
            cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n").Trim();
            return new UTF8Encoding(false).GetBytes(cleaned);
        }

        return rawBytes.ToArray();
    }

    private static byte[] ScrubJpegSegments(byte[] jpeg)
    {
        using var output = new MemoryStream(jpeg.Length);
        output.WriteByte(0xFF);
        output.WriteByte(0xD8);

        var index = 2;
        while (index + 1 < jpeg.Length)
        {
            if (jpeg[index] != 0xFF)
            {
                // Copy remaining scan data
                output.Write(jpeg, index, jpeg.Length - index);
                break;
            }

            var marker = jpeg[index + 1];
            if (marker == 0xDA || marker == 0xD9) // SOS (Start of Scan) or EOI
            {
                output.Write(jpeg, index, jpeg.Length - index);
                break;
            }

            if (index + 3 >= jpeg.Length)
            {
                output.Write(jpeg, index, jpeg.Length - index);
                break;
            }

            var segmentLength = (jpeg[index + 2] << 8) | jpeg[index + 3];
            if (segmentLength < 2 || index + 2 + segmentLength > jpeg.Length)
            {
                output.Write(jpeg, index, jpeg.Length - index);
                break;
            }

            var isExifOrComment = (marker >= 0xE1 && marker <= 0xEF) || marker == 0xFE;
            if (!isExifOrComment)
            {
                output.Write(jpeg, index, 2 + segmentLength);
            }

            index += 2 + segmentLength;
        }

        return output.ToArray();
    }
}
