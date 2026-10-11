using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.AI;

public sealed record OutboundPayloadInspection(
    string ProviderId,
    string EndpointHost,
    string AnonymizedPayloadText,
    int RedactedSensitiveItemsCount,
    int EstimatedTokens,
    decimal EstimatedCostUsd,
    string PayloadSha256Hex,
    DateTimeOffset InspectedAtUtc);

/// <summary>
/// Data Payload Inspector & Consent Gate (#55): inspects the exact anonymized
/// text before any Hybrid cloud call, computes its SHA-256 digest, and requires
/// explicit user approval for that exact payload digest. Also enforces strict
/// LocalOnly / Air-Gapped mode.
/// </summary>
public sealed class HybridConsentGate
{
    private readonly HashSet<string> _approvedPayloadDigests = new(StringComparer.OrdinalIgnoreCase);

    public bool StrictLocalOnlyLock { get; set; }

    public OutboundPayloadInspection InspectOutboundPayload(
        string providerId,
        string endpointHost,
        AnonymizationSession anonymizationSession,
        int estimatedTokens,
        decimal estimatedCostUsd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointHost);
        ArgumentNullException.ThrowIfNull(anonymizationSession);

        var digest = ComputeSha256Hex($"{providerId}|{endpointHost}|{anonymizationSession.AnonymizedText}");
        return new OutboundPayloadInspection(
            ProviderId: providerId,
            EndpointHost: endpointHost,
            AnonymizedPayloadText: anonymizationSession.AnonymizedText,
            RedactedSensitiveItemsCount: anonymizationSession.RedactedTokenCount,
            EstimatedTokens: estimatedTokens,
            EstimatedCostUsd: estimatedCostUsd,
            PayloadSha256Hex: digest,
            InspectedAtUtc: DateTimeOffset.UtcNow);
    }

    public void ApprovePayload(OutboundPayloadInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (StrictLocalOnlyLock)
        {
            throw new InvalidOperationException("قفل حالت ۱۰۰٪ آفلاین (Strict LocalOnly) فعال است؛ هیچ داده‌ای نمی‌تواند به سرور ابری ارسال شود.");
        }

        _approvedPayloadDigests.Add(inspection.PayloadSha256Hex);
    }

    public void RevokeAllApprovals() => _approvedPayloadDigests.Clear();

    public void EnsureApprovedAndConsume(OutboundPayloadInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (StrictLocalOnlyLock)
        {
            throw new InvalidOperationException("قفل حالت ۱۰۰٪ آفلاین فعال است؛ ارسال درخواست به ارائه‌دهندهٔ ابری مسدود شد.");
        }

        if (!_approvedPayloadDigests.Remove(inspection.PayloadSha256Hex))
        {
            throw new InvalidOperationException("ارسال داده به ارائه‌دهندهٔ ابری بدون تأیید صریح کاربر در پنجرهٔ شفافیت (HybridConsentGate) مجاز نیست.");
        }
    }

    public static string ComputeSha256Hex(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
