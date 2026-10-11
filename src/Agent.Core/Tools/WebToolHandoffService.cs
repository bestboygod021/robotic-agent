using Agent.Core.Localization;
using System.Text.RegularExpressions;

namespace Agent.Core.Tools;

public sealed record WebToolHandoffPackage(
    string ToolId,
    string ToolName,
    Uri LaunchUri,
    string SanitizedPromptText,
    int RedactedItemsCount,
    bool ContainedSensitiveData);

/// <summary>
/// Prepares a safe, locally scrubbed prompt package before the user opens an
/// external web AI tool in their browser. Strips local file-system paths,
/// email addresses, phone numbers, and payment card numbers on-device.
/// </summary>
public sealed class WebToolHandoffService
{
    private static readonly Regex EmailPattern = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WindowsPathPattern = new(
        @"(?:[A-Za-z]:\\|\\\\[^\s\\]+\\)[^\s""'<>|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UnixPathPattern = new(
        @"(?<![A-Za-z0-9])/(?:home|Users|var|etc|tmp|mnt|opt)/[^\s""'<>|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CardNumberPattern = new(
        @"\b(?:\d[ -]*?){13,19}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(?:\+\d{1,3}[-.\s]?)?(?:\(?\d{3,4}\)?[-.\s]?)\d{3}[-.\s]?\d{4}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public WebToolHandoffPackage PrepareSafeHandoffPackage(
        AiTool tool,
        string? userDraftText,
        string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (!AiToolCatalog.TryGetSafeWebUri(tool.LoginUrl, out var uri) || uri is null)
        {
            throw new InvalidOperationException($"نشانی ابزار «{tool.Name}» معتبر نیست.");
        }

        var lang = CultureCatalog.NormalizeLanguageCode(languageCode);
        var starterPrompt = tool.GetPromptTemplate(lang);
        var redactedCount = 0;
        var draft = userDraftText?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(draft))
        {
            draft = ReplaceAndCount(draft, WindowsPathPattern, "[REDACTED_PATH]", ref redactedCount);
            draft = ReplaceAndCount(draft, UnixPathPattern, "[REDACTED_PATH]", ref redactedCount);
            draft = ReplaceAndCount(draft, EmailPattern, "[REDACTED_EMAIL]", ref redactedCount);
            draft = ReplaceAndCount(draft, CardNumberPattern, "[REDACTED_CARD]", ref redactedCount);
            draft = ReplaceAndCount(draft, PhonePattern, "[REDACTED_PHONE]", ref redactedCount);
        }

        var combinedPrompt = string.IsNullOrWhiteSpace(draft)
            ? starterPrompt
            : $"{starterPrompt}\n\n{draft}";

        return new WebToolHandoffPackage(
            ToolId: tool.Id,
            ToolName: tool.Name,
            LaunchUri: uri,
            SanitizedPromptText: combinedPrompt,
            RedactedItemsCount: redactedCount,
            ContainedSensitiveData: redactedCount > 0);
    }

    private static string ReplaceAndCount(string input, Regex pattern, string placeholder, ref int count)
    {
        var matches = 0;
        var result = pattern.Replace(input, _ =>
        {
            matches++;
            return placeholder;
        });
        count += matches;
        return result;
    }
}
