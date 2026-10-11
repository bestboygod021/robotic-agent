using Agent.Core.Localization;
using Agent.Core.Skills;
using Agent.Core.Tools;
using Agent.Core.Workflows;

namespace Agent.Core.UX;

public enum CommandPaletteItemKind
{
    Skill,
    Playbook,
    AiTool
}

public sealed record CommandPaletteItem(
    string Id,
    CommandPaletteItemKind Kind,
    string Title,
    string Subtitle,
    string BadgeText,
    double Score);

/// <summary>
/// Instant Command Palette Service (#81 — Ctrl+K): searches across all 20 skills,
/// 20 SMB Playbooks, and 200 AI Tools (240 total items) in all 6 languages.
/// </summary>
public sealed class CommandPaletteService
{
    private readonly SkillRegistry _skillRegistry;

    public CommandPaletteService(SkillRegistry? skillRegistry = null)
    {
        _skillRegistry = skillRegistry ?? SkillRegistry.CreateDefault();
    }

    public IReadOnlyList<CommandPaletteItem> Search(
        string? query,
        string languageCode = CultureCatalog.DefaultLanguageCode,
        int maxResults = 15)
    {
        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        var normalizedQuery = TextNormalizer.NormalizeMultilingual(query);
        var results = new List<CommandPaletteItem>();

        foreach (var skill in _skillRegistry.Descriptors)
        {
            var title = skill.GetDisplayName(lang);
            var desc = skill.GetDescription(lang);
            var score = ComputeMatchScore(normalizedQuery, skill.Id, title, desc, bonus: 1.3);
            if (score > 0)
            {
                results.Add(new CommandPaletteItem(skill.Id, CommandPaletteItemKind.Skill, title, desc, "Skill", score));
            }
        }

        foreach (var pb in PlaybookCatalog.All)
        {
            var title = pb.GetTitle(lang);
            var desc = pb.GetDescription(lang);
            var score = ComputeMatchScore(normalizedQuery, pb.Id, title, desc, bonus: 1.25);
            if (score > 0)
            {
                results.Add(new CommandPaletteItem(pb.Id, CommandPaletteItemKind.Playbook, title, desc, pb.Category, score));
            }
        }

        foreach (var tool in AiToolCatalog.AllTools)
        {
            var desc = tool.GetLocalizedDescription(lang);
            var score = ComputeMatchScore(normalizedQuery, tool.Id, tool.Name, desc, bonus: 1.0);
            if (score > 0)
            {
                results.Add(new CommandPaletteItem(tool.Id, CommandPaletteItemKind.AiTool, tool.Name, desc, tool.BadgeText, score));
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxResults, 1, 50))
            .ToArray();
    }

    private static double ComputeMatchScore(string normalizedQuery, string id, string title, string subtitle, double bonus)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return 1.0 * bonus;
        }

        var normTitle = TextNormalizer.NormalizeMultilingual(title);
        var normSub = TextNormalizer.NormalizeMultilingual(subtitle);
        var normId = TextNormalizer.NormalizeMultilingual(id);

        double score = 0;
        if (normTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 3.0;
        }

        if (normId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 2.5;
        }

        if (normSub.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 1.5;
        }

        return score * bonus;
    }
}
