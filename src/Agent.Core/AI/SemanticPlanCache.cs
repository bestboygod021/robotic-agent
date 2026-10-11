using Agent.Core.Localization;
using System.Text.RegularExpressions;

namespace Agent.Core.AI;

public sealed record CachedPlanEntry(
    string SignatureKey,
    AiPlanProposal TemplatedPlan,
    DateTimeOffset CachedAtUtc,
    int HitCount);

/// <summary>
/// Semantic Plan Cache (#58): caches validated AiPlanProposal templates keyed by
/// normalized multilingual intent signature (abstracting away specific file paths)
/// so repeated user commands resolve locally in sub-millisecond time.
/// </summary>
public sealed class SemanticPlanCache
{
    private static readonly RegexWhitespace CollapseSpaces = new();

    private readonly LocalAnonymizerProxy _anonymizer = new();
    private readonly Dictionary<string, CachedPlanEntry> _entries = new(StringComparer.Ordinal);
    private readonly int _maxEntries;
    private readonly TimeSpan _ttl;

    public SemanticPlanCache(int maxEntries = 128, TimeSpan? ttl = null)
    {
        _maxEntries = Math.Clamp(maxEntries, 8, 2048);
        _ttl = ttl ?? TimeSpan.FromHours(12);
    }

    public int Count => _entries.Count;

    public string BuildSignatureKey(string userRequest)
    {
        var session = _anonymizer.Anonymize(userRequest);
        var normalized = TextNormalizer.NormalizeMultilingual(session.AnonymizedText);
        return CollapseSpaces.Replace(normalized).Trim();
    }

    public void Store(string userRequest, AiPlanProposal validatedPlan, DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(validatedPlan);
        var session = _anonymizer.Anonymize(userRequest);
        var key = BuildSignatureKey(userRequest);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        // Convert real paths in the plan back to <PATH_1> template tokens for reuse
        var templatedPlan = ToTemplatedPlan(validatedPlan, session);

        if (_entries.Count >= _maxEntries && !_entries.ContainsKey(key))
        {
            var oldestKey = _entries.OrderBy(kv => kv.Value.CachedAtUtc).First().Key;
            _entries.Remove(oldestKey);
        }

        _entries[key] = new CachedPlanEntry(
            SignatureKey: key,
            TemplatedPlan: templatedPlan,
            CachedAtUtc: nowUtc ?? DateTimeOffset.UtcNow,
            HitCount: 0);
    }

    public bool TryGet(string userRequest, out AiPlanProposal? restoredPlan, DateTimeOffset? nowUtc = null)
    {
        restoredPlan = null;
        var currentTime = nowUtc ?? DateTimeOffset.UtcNow;
        var session = _anonymizer.Anonymize(userRequest);
        var key = BuildSignatureKey(userRequest);

        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (currentTime - entry.CachedAtUtc > _ttl)
        {
            _entries.Remove(key);
            return false;
        }

        _entries[key] = entry with { HitCount = entry.HitCount + 1 };
        restoredPlan = _anonymizer.DeAnonymizePlan(entry.TemplatedPlan, session);
        return true;
    }

    private static AiPlanProposal ToTemplatedPlan(AiPlanProposal plan, AnonymizationSession session)
    {
        if (session.PlaceholderToOriginal.Count == 0)
        {
            return plan;
        }

        var steps = new List<AiProposedStep>(plan.Steps.Count);
        foreach (var step in plan.Steps)
        {
            var rawJson = step.Arguments.GetRawText();
            foreach (var (placeholder, original) in session.PlaceholderToOriginal)
            {
                var escapedOriginal = System.Text.Json.JsonEncodedText.Encode(original).ToString();
                rawJson = rawJson.Replace(escapedOriginal, placeholder, StringComparison.Ordinal);
            }

            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            steps.Add(new AiProposedStep(step.SkillId, doc.RootElement.Clone()));
        }

        var summary = plan.Summary;
        foreach (var (placeholder, original) in session.PlaceholderToOriginal)
        {
            summary = summary.Replace(original, placeholder, StringComparison.Ordinal);
        }

        return new AiPlanProposal(summary, steps, plan.DurationEstimate);
    }

    private sealed class RegexWhitespace
    {
        private static readonly Regex Pattern = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public string Replace(string input) => Pattern.Replace(input, " ");
    }
}
