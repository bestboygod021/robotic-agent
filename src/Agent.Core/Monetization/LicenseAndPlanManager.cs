using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Monetization;

public enum SubscriptionPlanTier
{
    Free,
    ProSubscription,
    LifetimeDeal,
    EnterpriseCompliance
}

public sealed record LicenseEntitlements(
    SubscriptionPlanTier Tier,
    string TierDisplayNameFa,
    string TierDisplayNameEn,
    int MaxFilesPerBatch,
    bool UnlocksAll20Playbooks,
    bool UnlocksFolderWatcherAndScheduler,
    bool UnlocksEnterprisePolicyAndAuditChain,
    DateTimeOffset? ExpiresAtUtc);

/// <summary>
/// Offline-verifiable Freemium / Pro / Lifetime Deal (AppSumo/Gumroad) / Enterprise
/// License &amp; Plan Gate.
/// </summary>
public sealed class LicenseAndPlanManager
{
    public const int FreeTierMaxFilesPerBatch = 50;
    public const int PaidTierMaxFilesPerBatch = 5000;

    private static readonly byte[] DefaultVerificationKey =
        Encoding.UTF8.GetBytes("RoboticAgent.OfflineLicenseSigningKey.2026");

    public LicenseAndPlanManager(SubscriptionPlanTier initialTier = SubscriptionPlanTier.Free)
    {
        CurrentEntitlements = GetEntitlementsForTier(initialTier);
    }

    public LicenseEntitlements CurrentEntitlements { get; private set; }

    public static LicenseEntitlements GetEntitlementsForTier(
        SubscriptionPlanTier tier,
        DateTimeOffset? expiresAtUtc = null) => tier switch
    {
        SubscriptionPlanTier.ProSubscription => new(
            Tier: tier,
            TierDisplayNameFa: "اشتراک حرفه‌ای (Pro)",
            TierDisplayNameEn: "Pro Subscription",
            MaxFilesPerBatch: PaidTierMaxFilesPerBatch,
            UnlocksAll20Playbooks: true,
            UnlocksFolderWatcherAndScheduler: true,
            UnlocksEnterprisePolicyAndAuditChain: false,
            ExpiresAtUtc: expiresAtUtc),

        SubscriptionPlanTier.LifetimeDeal => new(
            Tier: tier,
            TierDisplayNameFa: "لایسنس مادام‌العمر (Lifetime Deal)",
            TierDisplayNameEn: "Lifetime Deal (LTD)",
            MaxFilesPerBatch: PaidTierMaxFilesPerBatch,
            UnlocksAll20Playbooks: true,
            UnlocksFolderWatcherAndScheduler: true,
            UnlocksEnterprisePolicyAndAuditChain: true,
            ExpiresAtUtc: null),

        SubscriptionPlanTier.EnterpriseCompliance => new(
            Tier: tier,
            TierDisplayNameFa: "نسخهٔ سازمانی و حریم خصوصی (Enterprise)",
            TierDisplayNameEn: "Enterprise Compliance Edition",
            MaxFilesPerBatch: PaidTierMaxFilesPerBatch * 2,
            UnlocksAll20Playbooks: true,
            UnlocksFolderWatcherAndScheduler: true,
            UnlocksEnterprisePolicyAndAuditChain: true,
            ExpiresAtUtc: expiresAtUtc),

        _ => new(
            Tier: SubscriptionPlanTier.Free,
            TierDisplayNameFa: "نسخهٔ رایگان (Free)",
            TierDisplayNameEn: "Free Starter Edition",
            MaxFilesPerBatch: FreeTierMaxFilesPerBatch,
            UnlocksAll20Playbooks: false,
            UnlocksFolderWatcherAndScheduler: false,
            UnlocksEnterprisePolicyAndAuditChain: false,
            ExpiresAtUtc: null)
    };

    public static string GenerateSignedLicenseKey(
        SubscriptionPlanTier tier,
        string customerEmailOrId,
        DateTimeOffset? expiresAtUtc = null,
        byte[]? signingKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerEmailOrId);
        var prefix = tier switch
        {
            SubscriptionPlanTier.ProSubscription => "PRO",
            SubscriptionPlanTier.LifetimeDeal => "LTD",
            SubscriptionPlanTier.EnterpriseCompliance => "ENT",
            _ => "FREE"
        };

        var expUnix = expiresAtUtc?.ToUnixTimeSeconds() ?? 0;
        var payload = $"{prefix}:{customerEmailOrId.Trim().ToLowerInvariant()}:{expUnix}";
        var sigHex = ComputeSignatureHex(payload, signingKey ?? DefaultVerificationKey)[..16].ToUpperInvariant();
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        return $"RA-{prefix}-{payloadB64}-{sigHex}";
    }

    public bool TryActivateLicenseKey(
        string licenseKey,
        DateTimeOffset? nowUtc = null,
        byte[]? signingKey = null)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return false;
        }

        var parts = licenseKey.Trim().Split('-', 4);
        if (parts.Length != 4 || !string.Equals(parts[0], "RA", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var payloadBytes = Convert.FromBase64String(parts[2]);
            var payload = Encoding.UTF8.GetString(payloadBytes);
            var expectedSig = ComputeSignatureHex(payload, signingKey ?? DefaultVerificationKey)[..16].ToUpperInvariant();
            if (!string.Equals(parts[3], expectedSig, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fields = payload.Split(':');
            if (fields.Length < 3)
            {
                return false;
            }

            var tier = fields[0].ToUpperInvariant() switch
            {
                "PRO" => SubscriptionPlanTier.ProSubscription,
                "LTD" => SubscriptionPlanTier.LifetimeDeal,
                "ENT" => SubscriptionPlanTier.EnterpriseCompliance,
                _ => SubscriptionPlanTier.Free
            };

            DateTimeOffset? expiresAt = null;
            if (long.TryParse(fields[2], out var expUnix) && expUnix > 0)
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                if (expiresAt <= (nowUtc ?? DateTimeOffset.UtcNow))
                {
                    return false;
                }
            }

            CurrentEntitlements = GetEntitlementsForTier(tier, expiresAt);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void EnsureBatchSizeAllowed(int requestedFilesCount)
    {
        if (requestedFilesCount > CurrentEntitlements.MaxFilesPerBatch)
        {
            throw new InvalidOperationException(
                $"در {CurrentEntitlements.TierDisplayNameFa} حداکثر {CurrentEntitlements.MaxFilesPerBatch} فایل در هر نوبت قابل پردازش است. برای پردازش نامحدود، نسخهٔ Pro یا لایسنس مادام‌العمر (Lifetime) را فعال کنید.");
        }
    }

    private static string ComputeSignatureHex(string payload, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
