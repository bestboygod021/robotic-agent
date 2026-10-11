using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Plugins;

public sealed record CustomSkillPluginManifest(
    string PluginId,
    string Version,
    string PublisherName,
    string TitleFa,
    string TitleEn,
    string DescriptionFa,
    string DescriptionEn,
    decimal PriceUsd,
    int DeveloperRevenueSharePercent,
    IReadOnlyList<string> BundledPlaybookIds,
    string? SignatureHex = null);

/// <summary>
/// Plugin &amp; Custom Skill Marketplace SDK: enables third-party developers and
/// industry experts to publish signed workflow/skill packs with a 70/30 revenue
/// share model and cryptographic manifest verification.
/// </summary>
public static class PluginMarketplaceCatalog
{
    public const int StandardDeveloperSharePercent = 70;

    private static readonly byte[] MarketplaceSigningKey =
        Encoding.UTF8.GetBytes("RoboticAgent.MarketplaceSigningKey.2026");

    public static IReadOnlyList<CustomSkillPluginManifest> FeaturedPlugins { get; } = CreateFeaturedPlugins();

    public static string SignPluginManifest(CustomSkillPluginManifest manifest, byte[]? signingKey = null)
    {
        var canonical = $"{manifest.PluginId}|{manifest.Version}|{manifest.PublisherName}|{manifest.PriceUsd:0.00}|{string.Join(",", manifest.BundledPlaybookIds)}";
        using var hmac = new HMACSHA256(signingKey ?? MarketplaceSigningKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool VerifyPluginManifest(CustomSkillPluginManifest manifest, byte[]? signingKey = null)
    {
        if (string.IsNullOrWhiteSpace(manifest.SignatureHex))
        {
            return false;
        }

        var expected = SignPluginManifest(manifest, signingKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(manifest.SignatureHex.ToLowerInvariant()));
    }

    private static IReadOnlyList<CustomSkillPluginManifest> CreateFeaturedPlugins()
    {
        CustomSkillPluginManifest Signed(CustomSkillPluginManifest raw) =>
            raw with { SignatureHex = SignPluginManifest(raw) };

        return
        [
            Signed(new(
                PluginId: "plugin.cpa_tax_pro",
                Version: "1.2.0",
                PublisherName: "FinTech Automation Labs",
                TitleFa: "پک حرفه‌ای حسابداران رسمی و ممیزی مالیاتی",
                TitleEn: "CPA Tax Audit & Bank Reconciliation Pro Pack",
                DescriptionFa: "شامل قالب‌های مغایرت‌گیری بانکی، استخراج فاکتورهای چندارزی و گزارش‌های فصلی مالیاتی",
                DescriptionEn: "Includes automated bank reconciliation, multi-currency invoice extraction, and quarterly tax audit reports",
                PriceUsd: 19.00m,
                DeveloperRevenueSharePercent: StandardDeveloperSharePercent,
                BundledPlaybookIds: ["playbook.accounting.invoice_pipeline", "playbook.accounting.tax_audit_backup"])),

            Signed(new(
                PluginId: "plugin.legal_shield_gdpr",
                Version: "1.0.0",
                PublisherName: "LexPrivacy Solutions",
                TitleFa: "پک سپر حقوقی، قراردادهای دوزبانه و ناشناس‌سازی پرونده‌ها",
                TitleEn: "Legal Shield, Bilingual Contracts & GDPR Discovery Pack",
                DescriptionFa: "شامل قالب‌های قرارداد دوزبانه، سانسور خودکار PII در لوایح و مقایسهٔ نسخه‌های قرارداد",
                DescriptionEn: "Includes bilingual NDA/Contract templates, court filing PII redaction, and contract version diffing",
                PriceUsd: 24.00m,
                DeveloperRevenueSharePercent: StandardDeveloperSharePercent,
                BundledPlaybookIds: ["playbook.legal.gdpr_sanitization", "playbook.legal.contract_bundle"])),

            Signed(new(
                PluginId: "plugin.ecommerce_seller_suite",
                Version: "1.1.0",
                PublisherName: "CommerceFlow Studio",
                TitleFa: "پک اتوماسیون فروشندگان اینترنتی و تولیدکنندگان محتوا",
                TitleEn: "E-Commerce Seller & Media Localization Suite",
                DescriptionFa: "بهینه‌سازی دسته‌ای عکس محصولات، حذف متادیتای دوربین، همگام‌سازی زیرنویس ویدیوها و تمیزکاری لیست سفارش‌ها",
                DescriptionEn: "Batch product photo optimization, EXIF scrubbing, video subtitle syncing, and order CSV cleaning",
                PriceUsd: 15.00m,
                DeveloperRevenueSharePercent: StandardDeveloperSharePercent,
                BundledPlaybookIds: ["playbook.content.photo_privacy_publish", "playbook.support.crm_csv_hygiene"]))
        ];
    }
}
