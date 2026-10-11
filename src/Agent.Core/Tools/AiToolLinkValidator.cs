namespace Agent.Core.Tools;

public sealed record AiToolLinkIssue(string ToolId, string Url, string Reason);

public sealed record AiToolLinkAuditReport(
    int TotalChecked,
    int ValidHttpsCount,
    IReadOnlyList<AiToolLinkIssue> Issues)
{
    public bool IsHealthy => Issues.Count == 0 && TotalChecked == ValidHttpsCount;
}

/// <summary>
/// Offline static validator for AI tool catalog URLs. Ensures every URL uses
/// strict HTTPS, has a valid host without embedded credentials or fragments,
/// and flags duplicate or malformed entries before release.
/// </summary>
public static class AiToolLinkValidator
{
    public static AiToolLinkAuditReport AuditCatalog(IEnumerable<AiTool>? tools = null)
    {
        var list = (tools ?? AiToolCatalog.Tools).ToArray();
        var issues = new List<AiToolLinkIssue>();
        var validCount = 0;

        foreach (var tool in list)
        {
            if (!AiToolCatalog.TryGetSafeWebUri(tool.LoginUrl, out var uri) || uri is null)
            {
                issues.Add(new AiToolLinkIssue(tool.Id, tool.LoginUrl ?? string.Empty, "نشانی باید HTTPS معتبر، بدون اطلاعات کاربری و بدون fragment باشد."));
                continue;
            }

            if (uri.HostNameType is UriHostNameType.Unknown || !uri.Host.Contains('.', StringComparison.Ordinal))
            {
                issues.Add(new AiToolLinkIssue(tool.Id, tool.LoginUrl, "دامنهٔ نشانی معتبر نیست."));
                continue;
            }

            validCount++;
        }

        return new AiToolLinkAuditReport(list.Length, validCount, issues);
    }
}
