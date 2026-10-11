using Agent.Core.Skills;
using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Security;

public sealed record SkillCatalogVerificationReport(
    bool IsVerified,
    int VerifiedSkillCount,
    string CatalogManifestSha256Hex,
    IReadOnlyList<string> Issues);

/// <summary>
/// Signed Skill Catalog Verifier (#79): computes a canonical SHA-256 manifest
/// over all registered SkillDescriptors in SkillRegistry and verifies that every
/// skill declares valid metadata, risk levels, and rollback support.
/// </summary>
public static class SignedSkillCatalogVerifier
{
    public static SkillCatalogVerificationReport VerifyRegistry(
        SkillRegistry registry,
        string? expectedManifestHashHex = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var issues = new List<string>();
        var sb = new StringBuilder();

        foreach (var descriptor in registry.Descriptors.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (!descriptor.SupportsRollback)
            {
                issues.Add($"Skill '{descriptor.Id}' does not support rollback.");
            }

            if (string.IsNullOrWhiteSpace(descriptor.GetDisplayName("fa")) ||
                string.IsNullOrWhiteSpace(descriptor.GetDisplayName("en")))
            {
                issues.Add($"Skill '{descriptor.Id}' is missing required localized display names.");
            }

            sb.Append(descriptor.Id)
              .Append('|')
              .Append(descriptor.RiskLevel)
              .Append('|')
              .Append(descriptor.RequiresPreviewApproval)
              .Append('|')
              .Append(descriptor.SupportsRollback)
              .Append('\n');
        }

        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(expectedManifestHashHex) &&
            !string.Equals(computedHash, expectedManifestHashHex, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("هش مانیفست رجیستری مهارت‌ها با امضای مورد انتظار هم‌خوانی ندارد.");
        }

        return new SkillCatalogVerificationReport(
            IsVerified: issues.Count == 0,
            VerifiedSkillCount: registry.Descriptors.Count,
            CatalogManifestSha256Hex: computedHash,
            Issues: issues);
    }
}
