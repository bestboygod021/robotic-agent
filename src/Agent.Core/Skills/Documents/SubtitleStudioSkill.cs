using Agent.Core.Abstractions;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public enum SubtitleOperationMode
{
    CleanAndSyncSrt,
    ExtractPlainTranscript
}

public sealed record SubtitleStudioRequest(
    string SourceDirectory,
    string OutputDirectory,
    SubtitleOperationMode Mode = SubtitleOperationMode.CleanAndSyncSrt,
    int TimeShiftMilliseconds = 0,
    bool RemoveAdvertisingCues = true);

public sealed record SubtitleFilePreview(
    string SourcePath,
    string FileName,
    string OutputPath,
    int TotalCuesCount,
    int AdsRemovedCount,
    int TimeShiftMilliseconds);

public sealed record SubtitleStudioPreview(
    string SourceDirectory,
    string OutputDirectory,
    SubtitleOperationMode Mode,
    IReadOnlyList<SubtitleFilePreview> Files)
{
    public int ReadyCount => Files.Count;
    public int TotalAdsRemoved => Files.Sum(f => f.AdsRemovedCount);
}

public sealed record SubtitleStudioResult(
    int ProcessedFilesCount,
    int TotalAdsRemoved,
    IReadOnlyList<string> OutputPaths,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline Subtitle &amp; Localization Studio Skill (doc.subtitle_studio):
/// cleans SRT/VTT subtitles, normalizes Persian/Arabic characters (ی/ک) without
/// breaking timecodes, removes spam/advertising cues, shifts timestamps (+/- ms),
/// or extracts plain dialogue transcripts with Undo.
/// </summary>
public sealed class SubtitleStudioSkill : ISkill<SubtitleStudioRequest, SubtitleStudioPreview, SubtitleStudioResult>
{
    public const string SkillId = "doc.subtitle_studio";

    private static readonly Regex SrtTimecodeLinePattern = new(
        @"^(?<start>\d{2}:\d{2}:\d{2}[,\.]\d{3})\s*-->\s*(?<end>\d{2}:\d{2}:\d{2}[,\.]\d{3})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] AdMarkers =
    [
        "t.me/", "telegram.me/", "@", "www.", "http://", "https://", "دانلود فیلم", "کانال تلگرام", "subtitle by", "subscene"
    ];

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "استودیو هوشمند زیرنویس (همگام‌سازی زمان، اصلاح ی/ک و حذف تبلیغات SRT)",
            ["en"] = "Subtitle & Localization Studio (SRT Time-Sync, Clean & Extract)",
            ["zh"] = "字幕与本地化工作室（SRT 时间轴同步、去广告与提取）",
            ["es"] = "Estudio de Subtítulos (Sincronización SRT, Limpieza y Extracción)",
            ["ar"] = "استوديو الترجمة والأفلام (مزامنة الوقت وتنظيف ملفات SRT)",
            ["fr"] = "Studio de Sous-Titres (Synchronisation SRT, Nettoyage et Extraction)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تنظیم تأخیر زمانی زیرنویس (+/- میلی‌ثانیه)، اصلاح حروف ی/ک فارسی بدون خراب شدن تایم‌کد، حذف تبلیغات کانال‌ها و استخراج متن خالص همراه با واگرد.",
            ["en"] = "Shifts SRT timestamps (+/- ms), normalizes Persian/Arabic script, strips spam cues, or extracts plain dialogue with Undo.",
            ["zh"] = "调整 SRT 字幕时间轴偏移、清除广告字幕块、规范化多语言字符或提取纯文本对话并支持撤销。",
            ["es"] = "Ajusta tiempos SRT (+/- ms), elimina publicidad de subtítulos y extrae transcripciones limpias con deshacer.",
            ["ar"] = "يضبط توقيت ملفات الترجمة SRT ويوحد الحروف العربية/الفارسية ويزيل الإعلانات مع إمكانية التراجع.",
            ["fr"] = "Décale les codes temporels SRT (+/- ms), supprime les publicités et extrait le texte brut avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public SubtitleStudioPreview Preview(SubtitleStudioRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "پوشهٔ مبدأ زیرنویس");
        var outDir = PathGuard.ValidateSelectedFolder(request.OutputDirectory, "پوشهٔ مقصد زیرنویس");

        var previews = new List<SubtitleFilePreview>();
        foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is not (".srt" or ".vtt"))
            {
                continue;
            }

            if (PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
            {
                continue;
            }

            var raw = File.ReadAllText(filePath, Encoding.UTF8);
            var (_, totalCues, adsRemoved) = ProcessSubtitleContent(raw, request);
            var stem = Path.GetFileNameWithoutExtension(filePath);
            var outExt = request.Mode == SubtitleOperationMode.ExtractPlainTranscript ? ".transcript.txt" : ext;
            var outPath = Path.Combine(outDir, $"{stem}_cleaned{outExt}");

            previews.Add(new SubtitleFilePreview(
                SourcePath: filePath,
                FileName: Path.GetFileName(filePath),
                OutputPath: outPath,
                TotalCuesCount: totalCues,
                AdsRemovedCount: adsRemoved,
                TimeShiftMilliseconds: request.TimeShiftMilliseconds));
        }

        return new SubtitleStudioPreview(sourceDir, outDir, request.Mode, previews);
    }

    public async Task<SubtitleStudioResult> ExecuteAsync(
        SubtitleStudioRequest request,
        SubtitleStudioPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Files.Count != approvedPreview.Files.Count)
        {
            throw new InvalidOperationException("فایل‌های زیرنویس از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var outputPaths = new List<string>();
        var records = new List<OperationRecord>();
        var totalAds = 0;

        foreach (var file in currentPreview.Files)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var raw = await File.ReadAllTextAsync(file.SourcePath, Encoding.UTF8, context.CancellationToken).ConfigureAwait(false);
            var (processedText, _, adsRemoved) = ProcessSubtitleContent(raw, request);
            totalAds += adsRemoved;

            await File.WriteAllTextAsync(file.OutputPath, processedText, new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
            var info = new FileInfo(file.OutputPath);
            var sha256 = await RollbackManager.ComputeFileSha256HexAsync(file.OutputPath, context.CancellationToken).ConfigureAwait(false);

            records.Add(new OperationRecord(
                OperationRecordKind.FileCreatedOrCopied,
                OriginalPath: file.SourcePath,
                TargetPath: file.OutputPath,
                SizeBytes: info.Length,
                ContentSha256Hex: sha256));
            outputPaths.Add(file.OutputPath);
        }

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records: records);

        return new SubtitleStudioResult(outputPaths.Count, totalAds, outputPaths, manifest);
    }

    public static (string OutputText, int TotalCues, int AdsRemoved) ProcessSubtitleContent(
        string rawContent,
        SubtitleStudioRequest request)
    {
        var blocks = Regex.Split(rawContent.Replace("\r\n", "\n", StringComparison.Ordinal).Trim(), @"\n\s*\n");
        var sb = new StringBuilder();
        var cueIndex = 0;
        var totalCues = 0;
        var adsRemoved = 0;

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
            if (lines.Length == 0)
            {
                continue;
            }

            var timeLineIdx = Array.FindIndex(lines, l => SrtTimecodeLinePattern.IsMatch(l));
            if (timeLineIdx < 0)
            {
                continue;
            }

            totalCues++;
            var textLines = lines.Skip(timeLineIdx + 1).ToArray();
            var joinedText = string.Join("\n", textLines);

            if (request.RemoveAdvertisingCues &&
                AdMarkers.Any(marker => joinedText.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                adsRemoved++;
                continue;
            }

            // Normalize Arabic Yeh/Kaf to standard Persian Yeh/Kaf without touching timestamps
            var normalizedDialogue = joinedText
                .Replace('ي', 'ی')
                .Replace('ك', 'ک')
                .Replace('ۀ', 'ه');

            cueIndex++;
            if (request.Mode == SubtitleOperationMode.ExtractPlainTranscript)
            {
                sb.AppendLine(normalizedDialogue.Replace("\n", " ", StringComparison.Ordinal));
            }
            else
            {
                var shiftedTimeLine = ShiftTimecodeLine(lines[timeLineIdx], request.TimeShiftMilliseconds);
                sb.AppendLine(cueIndex.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine(shiftedTimeLine);
                sb.AppendLine(normalizedDialogue);
                sb.AppendLine();
            }
        }

        return (sb.ToString().TrimEnd() + "\n", totalCues, adsRemoved);
    }

    public static string ShiftTimecodeLine(string timecodeLine, int shiftMilliseconds)
    {
        if (shiftMilliseconds == 0)
        {
            return timecodeLine;
        }

        var match = SrtTimecodeLinePattern.Match(timecodeLine);
        if (!match.Success)
        {
            return timecodeLine;
        }

        var start = ParseSrtTimestamp(match.Groups["start"].Value);
        var end = ParseSrtTimestamp(match.Groups["end"].Value);
        var delta = TimeSpan.FromMilliseconds(shiftMilliseconds);

        var newStart = start + delta;
        if (newStart < TimeSpan.Zero)
        {
            newStart = TimeSpan.Zero;
        }

        var newEnd = end + delta;
        if (newEnd < newStart)
        {
            newEnd = newStart + TimeSpan.FromMilliseconds(500);
        }

        return $"{FormatSrtTimestamp(newStart)} --> {FormatSrtTimestamp(newEnd)}";
    }

    private static TimeSpan ParseSrtTimestamp(string ts)
    {
        var normalized = ts.Replace('.', ',');
        return TimeSpan.TryParseExact(normalized, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out var span)
            ? span
            : TimeSpan.Zero;
    }

    private static string FormatSrtTimestamp(TimeSpan span) =>
        $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2},{span.Milliseconds:D3}";
}
