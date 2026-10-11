using Agent.Core.Tools;

namespace Agent.Core.Monetization;

/// <summary>
/// Affiliate &amp; Partner Revenue Link Builder: appends transparent UTM and partner
/// referral codes to the 200 AI Tool Catalog URLs while enforcing HTTPS safety.
/// </summary>
public static class AffiliateLinkBuilder
{
    public const string DefaultPartnerCode = "robotic_agent";

    public static string BuildMonetizedLaunchUrl(
        AiToolEntry tool,
        string partnerReferralCode = DefaultPartnerCode,
        bool enableAffiliateTagging = true)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var rawUrl = tool.EffectiveLaunchUrl;
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("نشانی ابزار باید یک لینک امن HTTPS باشد.");
        }

        if (!enableAffiliateTagging || string.IsNullOrWhiteSpace(partnerReferralCode))
        {
            return uri.ToString();
        }

        var safeRef = Uri.EscapeDataString(partnerReferralCode.Trim());
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return $"{uri}{separator}utm_source=robotic_agent&utm_medium=desktop_hub&ref={safeRef}";
    }
}
