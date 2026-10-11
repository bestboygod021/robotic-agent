using Agent.Core.Abstractions;
using Agent.Core.Skills.Documents;

namespace Agent.Core.Skills;

/// <summary>
/// Thread-safe registry of allowlisted, preview-enforced skills available to the
/// orchestrator and AI planning validator. Unknown or unsigned skill IDs are rejected.
/// </summary>
public sealed class SkillRegistry
{
    private readonly Dictionary<string, ISkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    public SkillRegistry(IEnumerable<ISkill>? initialSkills = null)
    {
        if (initialSkills is not null)
        {
            foreach (var skill in initialSkills)
            {
                Register(skill);
            }
        }
    }

    public static SkillRegistry CreateDefault() =>
        new(
        [
            new FileCopySkill(),
            new BatchRenameSkill(),
            new FolderOrganizeSkill(),
            new DuplicateFinderSkill(),
            new SafeArchiveSkill(),
            new IncrementalBackupSkill(),
            new BatchConvertSkill(),
            new MetadataScrubberSkill(),
            new OfflineOcrSkill(),
            new InvoiceExtractSkill(),
            new PdfToolkitSkill(),
            new CsvCleanerSkill(),
            new DocumentDiffSkill(),
            new FolderReportSkill(),
            new TemplateMergeSkill(),
            new LocalSemanticSearchSkill(),
            new BatchSummarizeSkill(),
            new PiiRedactionSkill(),
            new SmartClipboardSkill(),
            new WebTaskAutomationSkill(),
            new MediaAndPdfOptimizeSkill(),
            new SubtitleStudioSkill(),
            new MeetingAndVoiceNotesSkill(),
            new FinancialReconciliationSkill()
        ]);

    public IReadOnlyList<SkillDescriptor> Descriptors =>
        _skills.Values
            .Select(skill => skill.Descriptor)
            .OrderBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public void Register(ISkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(skill.Descriptor);

        if (string.IsNullOrWhiteSpace(skill.Descriptor.Id))
        {
            throw new ArgumentException("شناسهٔ مهارت نمی‌تواند خالی باشد.", nameof(skill));
        }

        if (!skill.Descriptor.RequiresPreviewApproval &&
            skill.Descriptor.RiskLevel != SkillRiskLevel.ReadOnly)
        {
            throw new InvalidOperationException(
                $"مهارت «{skill.Descriptor.Id}» تغییر در سیستم ایجاد می‌کند و باید نیازمند پیش‌نمایش و تأیید باشد.");
        }

        if (_skills.ContainsKey(skill.Descriptor.Id))
        {
            throw new InvalidOperationException($"مهارت با شناسهٔ «{skill.Descriptor.Id}» قبلاً ثبت شده است.");
        }

        _skills[skill.Descriptor.Id] = skill;
    }

    public bool IsAllowedSkill(string? skillId) =>
        !string.IsNullOrWhiteSpace(skillId) && _skills.ContainsKey(skillId);

    public bool TryGetSkill(string? skillId, out ISkill? skill)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            skill = null;
            return false;
        }

        return _skills.TryGetValue(skillId, out skill);
    }

    public TSkill GetRequiredSkill<TSkill>(string skillId) where TSkill : class, ISkill
    {
        if (!TryGetSkill(skillId, out var skill) || skill is not TSkill typedSkill)
        {
            throw new KeyNotFoundException($"مهارت مجاز «{skillId}» در رجیستری پیدا نشد.");
        }

        return typedSkill;
    }
}
