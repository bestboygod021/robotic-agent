using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agent.Core.AI;

public sealed record AnonymizationSession(
    string OriginalText,
    string AnonymizedText,
    IReadOnlyDictionary<string, string> PlaceholderToOriginal,
    int RedactedTokenCount);

/// <summary>
/// Local Anonymizer & De-Anonymizer Proxy (#56): replaces local file paths,
/// emails, phone numbers, IBANs, credit cards, and IPv4 addresses with safe
/// placeholders (&lt;PATH_1&gt;, &lt;EMAIL_1&gt;, etc.) before cloud transmission and
/// restores them locally into the proposed plan arguments.
/// </summary>
public sealed class LocalAnonymizerProxy
{
    private static readonly Regex WindowsOrUnixPathPattern = new(
        @"(?:[a-zA-Z]:[\\/](?:[^\s""'<>|]+[\\/]?)+|/(?:home|Users|var|tmp|mnt|opt|srv)/[^\s""'<>|]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailPattern = new(
        @"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IbanPattern = new(
        @"\b(?:IR\d{24}|[A-Z]{2}\d{2}[A-Z0-9]{11,30})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CardCandidatePattern = new(
        @"\b(?:\d[ \-]?){15}\d\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(?:\+\d{1,3}[\s\-]?|\b09\d{2}[\s\-]?|\(\d{3}\)[\s\-]?)\d{3}[\s\-]?\d{3,4}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IpV4Pattern = new(
        @"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public AnonymizationSession Anonymize(string inputText)
    {
        if (string.IsNullOrEmpty(inputText))
        {
            return new AnonymizationSession(string.Empty, string.Empty, new Dictionary<string, string>(), 0);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var reverseMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        string ReplaceMatch(string rawValue, string prefix)
        {
            if (reverseMap.TryGetValue(rawValue, out var existingToken))
            {
                return existingToken;
            }

            counters.TryGetValue(prefix, out var idx);
            idx++;
            counters[prefix] = idx;
            var token = $"<{prefix}_{idx.ToString(CultureInfo.InvariantCulture)}>";
            map[token] = rawValue;
            reverseMap[rawValue] = token;
            return token;
        }

        var working = WindowsOrUnixPathPattern.Replace(inputText, m => ReplaceMatch(m.Value, "PATH"));
        working = EmailPattern.Replace(working, m => ReplaceMatch(m.Value, "EMAIL"));
        working = IbanPattern.Replace(working, m => ReplaceMatch(m.Value, "IBAN"));
        working = CardCandidatePattern.Replace(working, m => ReplaceMatch(m.Value, "CARD"));
        working = IpV4Pattern.Replace(working, m => ReplaceMatch(m.Value, "IP"));
        working = PhonePattern.Replace(working, m => ReplaceMatch(m.Value, "PHONE"));

        return new AnonymizationSession(
            OriginalText: inputText,
            AnonymizedText: working,
            PlaceholderToOriginal: map,
            RedactedTokenCount: map.Count);
    }

    public string DeAnonymizeText(string textWithPlaceholders, AnonymizationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrEmpty(textWithPlaceholders) || session.PlaceholderToOriginal.Count == 0)
        {
            return textWithPlaceholders;
        }

        var output = textWithPlaceholders;
        foreach (var (placeholder, original) in session.PlaceholderToOriginal)
        {
            output = output.Replace(placeholder, original, StringComparison.Ordinal);
        }

        return output;
    }

    public AiPlanProposal DeAnonymizePlan(AiPlanProposal anonymizedPlan, AnonymizationSession session)
    {
        ArgumentNullException.ThrowIfNull(anonymizedPlan);
        ArgumentNullException.ThrowIfNull(session);

        if (session.PlaceholderToOriginal.Count == 0)
        {
            return anonymizedPlan;
        }

        var restoredSteps = new List<AiProposedStep>(anonymizedPlan.Steps.Count);
        foreach (var step in anonymizedPlan.Steps)
        {
            var rawJson = step.Arguments.GetRawText();
            foreach (var (placeholder, original) in session.PlaceholderToOriginal)
            {
                var escapedOriginal = JsonEncodedText.Encode(original).ToString();
                rawJson = rawJson.Replace(placeholder, escapedOriginal, StringComparison.Ordinal);
            }

            using var doc = JsonDocument.Parse(rawJson);
            restoredSteps.Add(new AiProposedStep(step.SkillId, doc.RootElement.Clone()));
        }

        var restoredSummary = DeAnonymizeText(anonymizedPlan.Summary, session);
        return new AiPlanProposal(restoredSummary, restoredSteps, anonymizedPlan.DurationEstimate);
    }
}
