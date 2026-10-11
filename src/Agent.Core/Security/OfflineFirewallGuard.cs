using Agent.Core.AI;
using System.Net;

namespace Agent.Core.Security;

/// <summary>
/// Strict Offline Firewall Guard (#75): enforces air-gapped network blocking when
/// AiExecutionMode.LocalOnly is active, rejecting any non-loopback URI or socket target.
/// </summary>
public sealed class OfflineFirewallGuard
{
    public AiExecutionMode CurrentMode { get; set; } = AiExecutionMode.LocalOnly;

    public void EnsureUriAllowed(string uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("نشانی شبکه نامعتبر است.", nameof(uriText));
        }

        EnsureUriAllowed(uri);
    }

    public void EnsureUriAllowed(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (CurrentMode != AiExecutionMode.LocalOnly)
        {
            return;
        }

        var isLoopback = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                         (IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip));

        if (!isLoopback)
        {
            throw new UnauthorizedAccessException(
                $"فایروال داخلی حالت آفلاین (OfflineFirewallGuard): اتصال به میزبان خارجی «{uri.Host}» در حالت LocalOnly مسدود شد.");
        }
    }
}
