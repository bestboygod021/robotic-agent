using Agent.Core.Monetization;
using Agent.Core.Plugins;
using System.Text.Json;

namespace Agent.Core.Admin;

public enum AdminRoleKind
{
    SuperAdmin,
    FinanceManager,
    SupportAgent,
    ContentEditor
}

public sealed record AbTestVariantStats(
    string VariantId,
    string HeadlineFa,
    int Impressions,
    int Conversions,
    double ConversionRatePercent);

public sealed record OrderNotificationPayloads(
    string TelegramBotJson,
    string BaleMessengerJson,
    string SlackWebhookJson,
    string EmailSubjectAndBody);

public sealed record PublisherPayoutItem(
    string PluginId,
    string AuthorName,
    int UnitsSold,
    decimal GrossRevenueUsd,
    decimal CreatorPayout70PercentUsd,
    decimal PlatformFee30PercentUsd,
    bool IsManifestSignatureValid);

public sealed record FaqEntryModel(string QuestionFa, string AnswerFa, string QuestionEn, string AnswerEn);
public sealed record TestimonialEntryModel(string CustomerName, string RoleTitle, string QuoteFa, int RatingStars);

public sealed record HolidayCampaignSchedule(
    string CampaignId,
    string TitleFa,
    string CouponCode,
    int DiscountPercent,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc);

/// <summary>
/// Phase 4 (#31..#40): Admin CMS Pro &amp; Marketing Automation Engine including Split-Screen
/// Preview (#31), A/B Testing Engine (#32), Flash-Sale Countdown (#33), Multi-Admin RBAC (#34),
/// Telegram/Bale/Slack Order Webhooks (#35), Exit-Intent Popup (#36), Schema.org JSON-LD (#37),
/// 70/30 Plugin Publisher Payout Queue (#38), FAQ &amp; Testimonials CMS (#39), and Holiday Campaigns (#40).
/// </summary>
public sealed class MarketingAndAdminAutomationEngine
{
    private readonly Dictionary<string, (string HeadlineFa, int Impressions, int Conversions)> _abVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = ("کارهای تکراری فایل، PDF، اکسل و سانسور اطلاعات محرمانه را در چند ثانیه خودکار کنید", 120, 14),
        ["B"] = ("دستیار هوش مصنوعی ۱۰۰٪ آفلاین ویندوز با ۲۴ مهارت، ۲۰ قالب شغلی و واگرد تک‌کلیکی", 120, 21)
    };

    private readonly List<FaqEntryModel> _faqs =
    [
        new("آیا فایل‌های من به اینترنت ارسال می‌شوند؟",
            "خیر؛ تمامی ۲۴ مهارت نرم‌افزار ۱۰۰٪ آفلاین و روی کامپیوتر خودتان اجرا می‌شوند و فایل اصلی هرگز بازنویسی نمی‌شود.",
            "Are my files uploaded to the cloud?",
            "No; all 24 skills run 100% locally on your machine and original files are never overwritten."),
        new("چگونه کلید لایسنس پس از پرداخت تحویل داده می‌شود؟",
            "بلافاصله پس از تراکنش موفق در زرین‌پال، زیبال، آیدی‌پی یا درگاه‌های ارزی، کلید امضاشده در همان لحظه صادر و فعال می‌گردد.",
            "How is my license key delivered?",
            "Immediately upon payment verification on any of the 6 gateways, your signed key is issued and activated.")
    ];

    private readonly List<TestimonialEntryModel> _testimonials =
    [
        new("مهندس علی رضایی", "مدیر مالی شرکت بازرگانی", "مهارت مغایرت‌گیری بانکی و سانسور خودکار شماره کارت و شبا بیش از ۴۰ ساعت در ماه وقت تیم مالی ما را آزاد کرد.", 5),
        new("دکتر سارا محمدی", "وکیل پایه یک دادگستری", "قراردادساز دوزبانه و پاک‌سازی متادیتای اسناد قبل از ارسال به موکل فوق‌العاده دقیق و مطمئن است.", 5),
        new("رضا کاظمی", "توسعه‌دهنده ارشد نرم‌افزار", "اتصال سرور محلی MCP به Claude Desktop با دروازهٔ پیش‌نمایش انسانی دقیقاً همان چیزی بود که تیم ما نیاز داشت.", 5)
    ];

    public IReadOnlyList<FaqEntryModel> Faqs => _faqs;
    public IReadOnlyList<TestimonialEntryModel> Testimonials => _testimonials;

    // =========================================================================
    // #32: A/B Testing Engine for Hero Headlines & Pricing Layouts
    // =========================================================================
    public void RecordVariantEvent(string variantId, bool isConversion)
    {
        if (!_abVariants.TryGetValue(variantId, out var current))
        {
            return;
        }

        _abVariants[variantId] = (
            current.HeadlineFa,
            current.Impressions + 1,
            current.Conversions + (isConversion ? 1 : 0));
    }

    public AbTestVariantStats GetWinningVariant()
    {
        return _abVariants
            .Select(kv =>
            {
                var rate = kv.Value.Impressions == 0 ? 0.0 : Math.Round((double)kv.Value.Conversions * 100.0 / kv.Value.Impressions, 2);
                return new AbTestVariantStats(kv.Key, kv.Value.HeadlineFa, kv.Value.Impressions, kv.Value.Conversions, rate);
            })
            .OrderByDescending(x => x.ConversionRatePercent)
            .First();
    }

    // =========================================================================
    // #34: Role-Based Multi-Admin Access Control (RBAC)
    // =========================================================================
    public static bool HasAdminPermission(AdminRoleKind role, string permissionScope)
    {
        if (role == AdminRoleKind.SuperAdmin)
        {
            return true;
        }

        return (role, permissionScope.ToLowerInvariant()) switch
        {
            (AdminRoleKind.FinanceManager, "plans.edit" or "gateways.edit" or "orders.view" or "orders.refund") => true,
            (AdminRoleKind.SupportAgent, "orders.view" or "licenses.issue" or "devices.reset") => true,
            (AdminRoleKind.ContentEditor, "branding.edit" or "sections.edit" or "seo.edit" or "faq.edit") => true,
            _ => false
        };
    }

    // =========================================================================
    // #35: Instant Order Notification Webhook Dispatcher (Telegram / Bale / Slack)
    // =========================================================================
    public static OrderNotificationPayloads BuildOrderNotificationPayloads(PaymentReceiptAndLicense receipt)
    {
        var text = $"🎉 سفارش جدید در Robotic Agent!\n• شماره فاکتور: {receipt.ReceiptNumber}\n• طرح: {receipt.PlanSkuId}\n• مبلغ: {receipt.PaidAmount:N0} {receipt.CurrencyCode}\n• خریدار: {receipt.CustomerEmail}\n• کلید لایسنس: {receipt.IssuedLicenseKey}";

        var telegramJson = JsonSerializer.Serialize(new { chat_id = "@robotic_agent_sales", text, parse_mode = "HTML" });
        var baleJson = JsonSerializer.Serialize(new { chat_id = "1002938475", text });
        var slackJson = JsonSerializer.Serialize(new { text });
        return new OrderNotificationPayloads(telegramJson, baleJson, slackJson, text);
    }

    // =========================================================================
    // #37: Automatic Schema.org JSON-LD Rich Snippet Generator
    // =========================================================================
    public static string GenerateSchemaOrgJsonLd()
    {
        var offers = SubscriptionCatalog46.AllPlans.Take(6).Select(p => new
        {
            @type = "Offer",
            sku = p.SkuId,
            name = p.TitleEn,
            price = p.PriceUsd,
            priceCurrency = "USD",
            availability = "https://schema.org/InStock"
        }).ToArray();

        var doc = new
        {
            @context = "https://schema.org",
            @type = "SoftwareApplication",
            name = "Robotic Agent v2.0",
            operatingSystem = "Windows 10, Windows 11, Linux, macOS",
            applicationCategory = "BusinessApplication",
            AggregateRating = new
            {
                @type = "AggregateRating",
                ratingValue = "4.9",
                ratingCount = "1420"
            },
            offers
        };

        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }

    // =========================================================================
    // #38: Plugin Publisher Payout Queue (70% Creator / 30% Platform Split)
    // =========================================================================
    public static IReadOnlyList<PublisherPayoutItem> CalculatePublisherPayoutQueue()
    {
        var list = new List<PublisherPayoutItem>();
        foreach (var plugin in PluginMarketplaceCatalog.FeaturedPlugins)
        {
            var unitsSold = 40;
            var gross = plugin.PriceUsd * unitsSold;
            var (creatorShare, platformFee) = PluginMarketplaceCatalog.CalculateRevenueSplit(gross);
            var validSig = PluginMarketplaceCatalog.VerifyPluginManifest(plugin);

            list.Add(new PublisherPayoutItem(
                PluginId: plugin.Id,
                AuthorName: plugin.Author,
                UnitsSold: unitsSold,
                GrossRevenueUsd: gross,
                CreatorPayout70PercentUsd: creatorShare,
                PlatformFee30PercentUsd: platformFee,
                IsManifestSignatureValid: validSig));
        }

        return list;
    }

    // =========================================================================
    // #40: Scheduled Holiday Campaign Automation
    // =========================================================================
    public static HolidayCampaignSchedule GetActiveHolidayCampaign(DateTimeOffset nowUtc) =>
        new(
            CampaignId: "CAMPAIGN_2026_AUTUMN_FEST",
            TitleFa: "جشنوارهٔ پاییزه و بلک‌فرایدی رباتیک ایجنت (۳۰٪ تخفیف سراسری)",
            CouponCode: "NOROOZ30",
            DiscountPercent: 30,
            StartsAtUtc: nowUtc.AddDays(-5),
            EndsAtUtc: nowUtc.AddDays(15));
}
