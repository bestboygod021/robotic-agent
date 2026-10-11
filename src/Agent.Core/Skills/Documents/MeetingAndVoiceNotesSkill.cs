using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Core.Skills.Documents;

public sealed record MeetingMinutesItem(
    string SourceFileName,
    IReadOnlyList<string> Participants,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> ActionItems,
    IReadOnlyList<string> KeyHighlights);

public sealed record MeetingNotesRequest(
    string SourceDirectory,
    string OutputFilePath,
    string Extension = "txt",
    string LanguageCode = CultureCatalog.DefaultLanguageCode);

public sealed record MeetingNotesPreview(
    string SourceDirectory,
    string OutputFilePath,
    IReadOnlyList<MeetingMinutesItem> Meetings)
{
    public int ReadyCount => Meetings.Count;
    public int TotalDecisions => Meetings.Sum(m => m.Decisions.Count);
    public int TotalActionItems => Meetings.Sum(m => m.ActionItems.Count);
}

public sealed record MeetingNotesResult(
    int ProcessedMeetingsCount,
    int TotalDecisionsCount,
    int TotalActionItemsCount,
    string OutputFilePath,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline Meeting &amp; Voice Notes Assistant Skill (doc.meeting_notes):
/// parses meeting transcripts/notes in 6 languages, identifies speakers/participants,
/// extracts Decisions (مصوبات) and Action Items (اقدام‌ها), and generates formal
/// Meeting Minutes (صورت‌جلسه رسمی) with Undo.
/// </summary>
public sealed class MeetingAndVoiceNotesSkill : ISkill<MeetingNotesRequest, MeetingNotesPreview, MeetingNotesResult>
{
    public const string SkillId = "doc.meeting_notes";

    private static readonly Regex SpeakerPrefixPattern = new(
        @"^(?<speaker>[\p{L}\p{N}\s_\-]{2,25})\s*[:：]\s*(?<utterance>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] DecisionMarkers =
    [
        "مصوبه", "تصمیم", "توافق شد", "مقرر شد",
        "decision", "decided", "agreed", "approved", "resolved",
        "决定", "决议", "同意",
        "decisión", "acordado", "aprobado",
        "قرار", "تم الاتفاق", "تقرر",
        "décision", "convenu", "approuvé"
    ];

    private static readonly string[] ActionMarkers =
    [
        "اقدام", "مسئول", "مهلت", "پیگیری", "انجام دهد",
        "action", "todo", "deadline", "assigned to", "follow up",
        "待办", "行动项", "负责人",
        "tarea", "pendiente", "responsable",
        "إجراء", "مطلوب", "مسؤول",
        "action", "échéance", "responsable"
    ];

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "دستیار هوشمند جلسات و تولید صورت‌جلسهٔ رسمی (مصوبات و اقدام‌ها)",
            ["en"] = "Smart Meeting Minutes, Decisions & Action Items Extractor",
            ["zh"] = "智能会议纪要生成（决议与行动项提取）",
            ["es"] = "Asistente de Actas de Reunión (Decisiones y Tareas)",
            ["ar"] = "مساعد الاجتماعات الذكي واستخراج القرارات والمهام",
            ["fr"] = "Assistant Comptes-Rendus de Réunion (Décisions et Actions)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "تحلیل متن یا رونوشت جلسات، شناسایی شرکت‌کنندگان، تفکیک مصوبات (Decisions) و کارهای محول‌شده (Action Items) و تولید صورت‌جلسهٔ استاندارد همراه با واگرد.",
            ["en"] = "Analyzes meeting transcripts offline, identifies speakers, extracts decisions and action items, and generates formal minutes with Undo.",
            ["zh"] = "离线分析会议记录，识别发言人，提取会议决议与待办事项并生成正式纪要。",
            ["es"] = "Analiza transcripciones de reuniones sin conexión, identifica participantes y extrae decisiones y tareas.",
            ["ar"] = "يحلل نصوص الاجتماعات محلياً ويحدد المتحدثين ويستخرج القرارات والمهام التنفيذية مع إمكانية التراجع.",
            ["fr"] = "Analyse les transcriptions de réunion hors-ligne, identifie les participants et extrait décisions et actions."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public MeetingNotesPreview Preview(MeetingNotesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceDir = PathGuard.ValidateSelectedFolder(request.SourceDirectory, "پوشهٔ یادداشت‌های جلسه");
        var outFull = Path.GetFullPath(request.OutputFilePath);
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(outFull)!, "پوشهٔ خروجی صورت‌جلسه");

        var ext = FileCopySkill.NormalizeExtension(request.Extension);
        var pattern = ext == "*" ? "*" : $"*.{ext}";
        var meetings = new List<MeetingMinutesItem>();

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (PathGuard.IsSuspiciousDoubleExtension(filePath, out _) || PathGuard.PathsEqual(filePath, outFull))
            {
                continue;
            }

            var rawText = File.ReadAllText(filePath, Encoding.UTF8);
            meetings.Add(ParseMeetingTranscript(Path.GetFileName(filePath), rawText));
        }

        return new MeetingNotesPreview(sourceDir, outFull, meetings);
    }

    public async Task<MeetingNotesResult> ExecuteAsync(
        MeetingNotesRequest request,
        MeetingNotesPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Meetings.Count != approvedPreview.Meetings.Count)
        {
            throw new InvalidOperationException("فایل‌های جلسه از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("# صورت‌جلسهٔ رسمی و مصوبات / Official Meeting Minutes");
        sb.AppendLine();

        foreach (var m in currentPreview.Meetings)
        {
            sb.AppendLine($"## {m.SourceFileName}");
            if (m.Participants.Count > 0)
            {
                sb.AppendLine($"- **شرکت‌کنندگان / Participants:** {string.Join("، ", m.Participants)}");
            }

            sb.AppendLine("### مصوبات جلسه / Decisions");
            if (m.Decisions.Count == 0)
            {
                sb.AppendLine("- (موردی ثبت نشده / None recorded)");
            }
            else
            {
                foreach (var d in m.Decisions)
                {
                    sb.AppendLine($"- [✓] {d}");
                }
            }

            sb.AppendLine("### اقدام‌های اجرایی / Action Items");
            if (m.ActionItems.Count == 0)
            {
                sb.AppendLine("- (موردی ثبت نشده / None recorded)");
            }
            else
            {
                foreach (var a in m.ActionItems)
                {
                    sb.AppendLine($"- [ ] {a}");
                }
            }

            sb.AppendLine();
        }

        await File.WriteAllTextAsync(currentPreview.OutputFilePath, sb.ToString(), new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
        var info = new FileInfo(currentPreview.OutputFilePath);
        var sha256 = await RollbackManager.ComputeFileSha256HexAsync(currentPreview.OutputFilePath, context.CancellationToken).ConfigureAwait(false);

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records:
            [
                new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: currentPreview.SourceDirectory,
                    TargetPath: currentPreview.OutputFilePath,
                    SizeBytes: info.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new MeetingNotesResult(
            currentPreview.Meetings.Count,
            currentPreview.TotalDecisions,
            currentPreview.TotalActionItems,
            currentPreview.OutputFilePath,
            manifest);
    }

    public static MeetingMinutesItem ParseMeetingTranscript(string fileName, string rawText)
    {
        var participants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var decisions = new List<string>();
        var actions = new List<string>();
        var highlights = new List<string>();

        foreach (var rawLine in rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var speakerMatch = SpeakerPrefixPattern.Match(line);
            if (speakerMatch.Success)
            {
                var speaker = speakerMatch.Groups["speaker"].Value.Trim();
                if (!DecisionMarkers.Contains(speaker, StringComparer.OrdinalIgnoreCase) &&
                    !ActionMarkers.Contains(speaker, StringComparer.OrdinalIgnoreCase))
                {
                    participants.Add(speaker);
                }
            }

            var norm = TextNormalizer.NormalizeMultilingual(line);
            if (DecisionMarkers.Any(m => norm.Contains(TextNormalizer.NormalizeMultilingual(m), StringComparison.OrdinalIgnoreCase)))
            {
                decisions.Add(line);
            }
            else if (ActionMarkers.Any(m => norm.Contains(TextNormalizer.NormalizeMultilingual(m), StringComparison.OrdinalIgnoreCase)))
            {
                actions.Add(line);
            }
            else if (line.Length >= 15 && highlights.Count < 5)
            {
                highlights.Add(line);
            }
        }

        return new MeetingMinutesItem(
            SourceFileName: fileName,
            Participants: participants.ToArray(),
            Decisions: decisions,
            ActionItems: actions,
            KeyHighlights: highlights);
    }
}
