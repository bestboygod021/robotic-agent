using Agent.Core.Monetization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Core.Admin;

public sealed record SiteThemeAndBrandingConfig(
    string BrandName,
    string TopBannerTextFa,
    string TopBannerTextEn,
    bool ShowTopBanner,
    string HeroBadgeFa,
    string HeroBadgeEn,
    string HeroTitleFa,
    string HeroTitleEn,
    string HeroSubtitleFa,
    string HeroSubtitleEn,
    string PrimaryCtaTextFa,
    string PrimaryCtaTextEn,
    string PrimaryColorHex,
    string AccentColorHex,
    string BackgroundColorHex,
    string FooterCopyrightFa,
    string FooterCopyrightEn,
    string SeoMetaTitle,
    string SeoMetaDescription,
    string GlobalAffiliatePartnerId,
    bool MaintenanceModeEnabled);

public sealed record SiteSectionVisibilityConfig(
    bool ShowHeroSection,
    bool ShowFeaturesSection,
    bool ShowRoiCalculatorSection,
    bool ShowPricingSection,
    bool ShowFaqSection,
    bool ShowFooterSection);

public sealed record GatewayAdminConfig(
    PaymentGatewayProviderId GatewayId,
    bool IsEnabled,
    bool IsSandboxMode,
    string MerchantIdOrApiKey,
    string CallbackUrl,
    string CustomDisplayNameFa);

public sealed record PlanPriceOverrideConfig(
    string SkuId,
    bool IsEnabled,
    string CustomTitleFa,
    decimal PriceUsd,
    long PriceToman,
    int MaxFilesPerBatch,
    int DiscountPercent);

public sealed record DiscountCouponConfig(
    string CouponCode,
    int DiscountPercent,
    bool IsActive,
    string DescriptionFa);

/// <summary>
/// Complete Site Administration &amp; CMS Manager allowing the site owner to modify
/// every part of the website (Branding, Theme, Hero, Sections, all 46 Subscription
/// Plans, all 6 Payment Gateways, Discount Coupons, SEO, and Affiliate tags) with
/// full JSON import/export and instant reset.
/// </summary>
public sealed class SiteAdminCmsManager
{
    private readonly Dictionary<PaymentGatewayProviderId, GatewayAdminConfig> _gateways = new();
    private readonly Dictionary<string, PlanPriceOverrideConfig> _planOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DiscountCouponConfig> _coupons = [];

    public SiteAdminCmsManager()
    {
        ResetToDefaults();
    }

    public SiteThemeAndBrandingConfig Branding { get; set; } = CreateDefaultBranding();
    public SiteSectionVisibilityConfig SectionVisibility { get; set; } = new(true, true, true, true, true, true);

    public IReadOnlyCollection<GatewayAdminConfig> GatewayConfigs => _gateways.Values;
    public IReadOnlyCollection<PlanPriceOverrideConfig> PlanOverrides => _planOverrides.Values;
    public IReadOnlyList<DiscountCouponConfig> Coupons => _coupons;

    public void ResetToDefaults()
    {
        Branding = CreateDefaultBranding();
        SectionVisibility = new SiteSectionVisibilityConfig(true, true, true, true, true, true);

        _gateways.Clear();
        foreach (var gw in PaymentGatewayOrchestrator.AllGateways)
        {
            _gateways[gw.Id] = new GatewayAdminConfig(
                GatewayId: gw.Id,
                IsEnabled: true,
                IsSandboxMode: false,
                MerchantIdOrApiKey: $"LIVE_KEY_{gw.Code.ToUpperInvariant()}_2026",
                CallbackUrl: $"https://robotic-agent.ir/payment/callback/{gw.Code}",
                CustomDisplayNameFa: gw.DisplayNameFa);
        }

        _planOverrides.Clear();
        foreach (var plan in SubscriptionCatalog46.AllPlans)
        {
            _planOverrides[plan.SkuId] = new PlanPriceOverrideConfig(
                SkuId: plan.SkuId,
                IsEnabled: true,
                CustomTitleFa: plan.TitleFa,
                PriceUsd: plan.PriceUsd,
                PriceToman: plan.PriceToman,
                MaxFilesPerBatch: plan.MaxFilesPerBatch,
                DiscountPercent: 0);
        }

        _coupons.Clear();
        _coupons.Add(new DiscountCouponConfig("NOROOZ30", 30, true, "تخفیف ۳۰٪ ویژهٔ جشنواره"));
        _coupons.Add(new DiscountCouponConfig("VIP20", 20, true, "تخفیف ۲۰٪ مشتریان وفادار"));
        _coupons.Add(new DiscountCouponConfig("STUDENT50", 50, true, "تخفیف ۵۰٪ ویژهٔ دانشجویان و اساتید"));
    }

    public void UpdateGatewayConfig(
        PaymentGatewayProviderId gatewayId,
        bool isEnabled,
        bool isSandboxMode,
        string merchantIdOrApiKey,
        string callbackUrl)
    {
        var current = _gateways[gatewayId];
        _gateways[gatewayId] = current with
        {
            IsEnabled = isEnabled,
            IsSandboxMode = isSandboxMode,
            MerchantIdOrApiKey = merchantIdOrApiKey ?? string.Empty,
            CallbackUrl = callbackUrl ?? string.Empty
        };
    }

    public void UpdatePlanOverride(
        string skuId,
        bool isEnabled,
        string customTitleFa,
        decimal priceUsd,
        long priceToman,
        int maxFilesPerBatch,
        int discountPercent = 0)
    {
        if (priceUsd < 0 || priceToman < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceToman), "قیمت اشتراک نمی‌تواند منفی باشد.");
        }

        _planOverrides[skuId] = new PlanPriceOverrideConfig(
            SkuId: skuId,
            IsEnabled: isEnabled,
            CustomTitleFa: string.IsNullOrWhiteSpace(customTitleFa) ? SubscriptionCatalog46.GetBySku(skuId).TitleFa : customTitleFa.Trim(),
            PriceUsd: priceUsd,
            PriceToman: priceToman,
            MaxFilesPerBatch: Math.Max(1, maxFilesPerBatch),
            DiscountPercent: Math.Clamp(discountPercent, 0, 95));
    }

    public void ApplyBulkDiscountAcrossAll46Plans(int discountPercent)
    {
        var clamped = Math.Clamp(discountPercent, 0, 90);
        foreach (var basePlan in SubscriptionCatalog46.AllPlans)
        {
            var factor = (100m - clamped) / 100m;
            var newUsd = Math.Round(basePlan.PriceUsd * factor, 2);
            var newToman = (long)(Math.Round(basePlan.PriceToman * factor / 1000m) * 1000m);
            _planOverrides[basePlan.SkuId] = new PlanPriceOverrideConfig(
                SkuId: basePlan.SkuId,
                IsEnabled: true,
                CustomTitleFa: basePlan.TitleFa,
                PriceUsd: newUsd,
                PriceToman: newToman,
                MaxFilesPerBatch: basePlan.MaxFilesPerBatch,
                DiscountPercent: clamped);
        }
    }

    public void UpsertCoupon(string code, int discountPercent, bool isActive, string descriptionFa)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("کد تخفیف نمی‌تواند خالی باشد.", nameof(code));
        }

        var normalized = code.Trim().ToUpperInvariant();
        _coupons.RemoveAll(c => string.Equals(c.CouponCode, normalized, StringComparison.OrdinalIgnoreCase));
        _coupons.Add(new DiscountCouponConfig(normalized, Math.Clamp(discountPercent, 1, 95), isActive, descriptionFa));
    }

    public string ExportCompleteSiteConfigJson()
    {
        var snapshot = new
        {
            exportedAtUtc = DateTimeOffset.UtcNow,
            branding = Branding,
            sectionVisibility = SectionVisibility,
            gateways = _gateways.Values.OrderBy(g => g.GatewayId).ToArray(),
            plans = _planOverrides.Values.ToArray(),
            coupons = _coupons.ToArray()
        };

        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    private static SiteThemeAndBrandingConfig CreateDefaultBranding() => new(
        BrandName: "🤖 Robotic Agent v2.0",
        TopBannerTextFa: "🔥 جشنوارهٔ فروش ویژه: ۳۰٪ تخفیف روی تمامی ۴۶ مدل اشتراک با کد تخفیف NOROOZ30 — پرداخت آنی با زرین‌پال، زیبال، آیدی‌پی و درگاه‌های ارزی!",
        TopBannerTextEn: "🔥 Special Offer: 30% OFF across all 46 Subscription Plans with code NOROOZ30 — Instant License Delivery!",
        ShowTopBanner: true,
        HeroBadgeFa: "۶ زبان زندهٔ دنیا | ۲۴ مهارت آفلاین | ۲۰ قالب شغلی | ۴۶ مدل اشتراک | ۶ درگاه پرداخت",
        HeroBadgeEn: "6 Global Languages | 24 Offline Skills | 20 SMB Playbooks | 46 Plans | 6 Payment Gateways",
        HeroTitleFa: "کارهای تکراری فایل، PDF، اکسل و سانسور اطلاعات محرمانه را در چند ثانیه خودکار کنید",
        HeroTitleEn: "Automate Repetitive File, PDF, Spreadsheet & Privacy Tasks in Seconds — 100% Locally",
        HeroSubtitleFa: "بدون آپلود فایل‌های محرمانه در سایت‌های ناشناس! پیش‌نمایش اجباری قبل از اجرا، واگرد تک‌کلیکی با بررسی هش SHA-256، اسکنر ریسک حریم خصوصی و مربی پرامپت‌ساز هوشمند.",
        HeroSubtitleEn: "Never upload confidential documents to untrusted websites again. Mandatory dry-run preview, 1-click SHA-256 transactional Undo, Privacy Leak Risk Scanner, and Interactive Prompt Coach.",
        PrimaryCtaTextFa: "💳 ورود به پنل کاربری و خرید ۴۶ مدل اشتراک (۶ درگاه)",
        PrimaryCtaTextEn: "💳 Open User Portal & 46 Subscription Plans",
        PrimaryColorHex: "#2563EB",
        AccentColorHex: "#10B981",
        BackgroundColorHex: "#0B1120",
        FooterCopyrightFa: "© 2026 Robotic Agent — تمامی حقوق محفوظ است. طراحی‌شده با معماری امنیت محلی (Local-First).",
        FooterCopyrightEn: "© 2026 Robotic Agent — All rights reserved. Built with Local-First Privacy Architecture.",
        SeoMetaTitle: "Robotic Agent | دستیار هوشمند دسکتاپ، ۲۴ مهارت آفلاین و ۴۶ مدل اشتراک",
        SeoMetaDescription: "خودکارسازی آفلاین فایل، PDF، اکسل، زیرنویس، صورت‌جلسه و سانسور اطلاعات محرمانه با ۶ درگاه پرداخت ایرانی و بین‌المللی.",
        GlobalAffiliatePartnerId: "robotic_official",
        MaintenanceModeEnabled: false);
}
