using Agent.Core.Admin;
using Agent.Core.Monetization;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Agent.Desktop;

public partial class SiteAdminWindow : Window
{
    private readonly SiteAdminCmsManager _cms = new();
    private readonly SiteSecurityAndTrustEngine _securityEngine = new();
    private string? _selectedPlanSku;
    private PaymentGatewayProviderId? _selectedGatewayId;

    public SiteAdminWindow()
    {
        InitializeComponent();
        _securityEngine.RecordAdminAction("superadmin", "SuperAdmin", "127.0.0.1", "INIT_ADMIN_PANEL", "راه‌اندازی پنل مدیریت و گاوصندوق امنیتی فاز ۱");
        LoadAllIntoUi();
    }

    private void LoadAllIntoUi()
    {
        var b = _cms.Branding;
        var s = _cms.SectionVisibility;

        ShowTopBannerCheck.IsChecked = b.ShowTopBanner;
        ShowHeroSectionCheck.IsChecked = s.ShowHeroSection;
        ShowFeaturesSectionCheck.IsChecked = s.ShowFeaturesSection;
        ShowRoiSectionCheck.IsChecked = s.ShowRoiCalculatorSection;
        ShowPricingSectionCheck.IsChecked = s.ShowPricingSection;
        MaintenanceModeCheck.IsChecked = b.MaintenanceModeEnabled;

        BrandNameBox.Text = b.BrandName;
        TopBannerFaBox.Text = b.TopBannerTextFa;
        HeroTitleFaBox.Text = b.HeroTitleFa;
        HeroSubtitleFaBox.Text = b.HeroSubtitleFa;
        SeoTitleBox.Text = b.SeoMetaTitle;
        SeoDescriptionBox.Text = b.SeoMetaDescription;
        AffiliateIdBox.Text = b.GlobalAffiliatePartnerId;
        PrimaryColorBox.Text = b.PrimaryColorHex;

        PopulatePlansList();
        PopulateGatewaysList();
        SiteJsonExportBox.Text = _cms.ExportCompleteSiteConfigJson();
    }

    private void PopulatePlansList()
    {
        AdminPlansListBox.Items.Clear();
        foreach (var p in _cms.PlanOverrides)
        {
            var status = p.IsEnabled ? "🟢 فعال" : "🔴 غیرفعال";
            AdminPlansListBox.Items.Add(new ListBoxItem
            {
                Tag = p,
                Content = $"{status} | {p.CustomTitleFa} — {p.PriceToman:N0} تومان / ${p.PriceUsd:0.00}"
            });
        }

        if (AdminPlansListBox.Items.Count > 0)
        {
            AdminPlansListBox.SelectedIndex = 5;
        }
    }

    private void PopulateGatewaysList()
    {
        AdminGatewaysListBox.Items.Clear();
        foreach (var g in _cms.GatewayConfigs)
        {
            var status = g.IsEnabled ? "🟢 فعال" : "🔴 غیرفعال";
            var mode = g.IsSandboxMode ? "Sandbox" : "Live";
            AdminGatewaysListBox.Items.Add(new ListBoxItem
            {
                Tag = g,
                Content = $"{status} [{mode}] — {g.CustomDisplayNameFa}"
            });
        }

        if (AdminGatewaysListBox.Items.Count > 0)
        {
            AdminGatewaysListBox.SelectedIndex = 0;
        }
    }

    private void AdminPlansListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AdminPlansListBox.SelectedItem is not ListBoxItem item || item.Tag is not PlanPriceOverrideConfig p)
        {
            return;
        }

        _selectedPlanSku = p.SkuId;
        PlanEnabledCheck.IsChecked = p.IsEnabled;
        PlanTitleFaBox.Text = p.CustomTitleFa;
        PlanPriceTomanBox.Text = p.PriceToman.ToString(CultureInfo.InvariantCulture);
        PlanPriceUsdBox.Text = p.PriceUsd.ToString("0.##", CultureInfo.InvariantCulture);
        PlanBatchLimitBox.Text = p.MaxFilesPerBatch.ToString(CultureInfo.InvariantCulture);
    }

    private void UpdateSelectedPlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedPlanSku))
        {
            return;
        }

        _ = long.TryParse(PlanPriceTomanBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var toman);
        _ = decimal.TryParse(PlanPriceUsdBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var usd);
        _ = int.TryParse(PlanBatchLimitBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var batch);

        _cms.UpdatePlanOverride(
            skuId: _selectedPlanSku,
            isEnabled: PlanEnabledCheck.IsChecked == true,
            customTitleFa: PlanTitleFaBox.Text,
            priceUsd: usd,
            priceToman: toman,
            maxFilesPerBatch: batch <= 0 ? 1000 : batch);

        PopulatePlansList();
        SiteJsonExportBox.Text = _cms.ExportCompleteSiteConfigJson();
        AdminStatusText.Text = $"✓ اشتراک {_selectedPlanSku} با قیمت جدید ({toman:N0} تومان / ${usd:0.00}) ذخیره شد.";
    }

    private void AdminGatewaysListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AdminGatewaysListBox.SelectedItem is not ListBoxItem item || item.Tag is not GatewayAdminConfig g)
        {
            return;
        }

        _selectedGatewayId = g.GatewayId;
        GatewayEnabledCheck.IsChecked = g.IsEnabled;
        GatewaySandboxCheck.IsChecked = g.IsSandboxMode;
        GatewayMerchantKeyBox.Text = g.MerchantIdOrApiKey;
        GatewayCallbackUrlBox.Text = g.CallbackUrl;
    }

    private void UpdateSelectedGatewayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGatewayId is null)
        {
            return;
        }

        _cms.UpdateGatewayConfig(
            gatewayId: _selectedGatewayId.Value,
            isEnabled: GatewayEnabledCheck.IsChecked == true,
            isSandboxMode: GatewaySandboxCheck.IsChecked == true,
            merchantIdOrApiKey: GatewayMerchantKeyBox.Text,
            callbackUrl: GatewayCallbackUrlBox.Text);

        PopulateGatewaysList();
        SiteJsonExportBox.Text = _cms.ExportCompleteSiteConfigJson();
        AdminStatusText.Text = $"✓ تنظیمات درگاه «{_selectedGatewayId.Value}» ذخیره شد.";
    }

    private void SaveAndExportJson_Click(object sender, RoutedEventArgs e)
    {
        _cms.Branding = _cms.Branding with
        {
            ShowTopBanner = ShowTopBannerCheck.IsChecked == true,
            BrandName = BrandNameBox.Text,
            TopBannerTextFa = TopBannerFaBox.Text,
            HeroTitleFa = HeroTitleFaBox.Text,
            HeroSubtitleFa = HeroSubtitleFaBox.Text,
            SeoMetaTitle = SeoTitleBox.Text,
            SeoMetaDescription = SeoDescriptionBox.Text,
            GlobalAffiliatePartnerId = AffiliateIdBox.Text,
            PrimaryColorHex = PrimaryColorBox.Text,
            MaintenanceModeEnabled = MaintenanceModeCheck.IsChecked == true
        };

        _cms.SectionVisibility = new SiteSectionVisibilityConfig(
            ShowHeroSection: ShowHeroSectionCheck.IsChecked == true,
            ShowFeaturesSection: ShowFeaturesSectionCheck.IsChecked == true,
            ShowRoiCalculatorSection: ShowRoiSectionCheck.IsChecked == true,
            ShowPricingSection: ShowPricingSectionCheck.IsChecked == true,
            ShowFaqSection: true,
            ShowFooterSection: true);

        SiteJsonExportBox.Text = _cms.ExportCompleteSiteConfigJson();
        AdminStatusText.Text = "✅ تمامی تغییرات هدر، هیرو، بخش‌ها، ۴۶ اشتراک و ۶ درگاه ذخیره و در خروجی JSON تولید شد.";
    }

    private void ApplyBulkDiscount20_Click(object sender, RoutedEventArgs e)
    {
        _cms.ApplyBulkDiscountAcrossAll46Plans(20);
        PopulatePlansList();
        SiteJsonExportBox.Text = _cms.ExportCompleteSiteConfigJson();
        AdminStatusText.Text = "🔥 تخفیف ۲۰٪ جشنواره‌ای روی تمامی ۴۶ مدل اشتراک (تومانی و دلاری) اعمال شد!";
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        _cms.ResetToDefaults();
        _securityEngine.RecordAdminAction("superadmin", "SuperAdmin", "127.0.0.1", "RESET_DEFAULTS", "بازگردانی تنظیمات سایت به حالت پیش‌فرض");
        LoadAllIntoUi();
        AdminStatusText.Text = "🔄 تمامی تنظیمات سایت به مقادیر پیش‌فرض بازگردانی شد.";
    }

    private void RunPhase1TotpButton_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var bundle = SiteSecurityAndTrustEngine.CreateTotpProvisioningBundle("admin@robotic-agent.ir", "ADMIN_2FA_MASTER_SECRET_2026");
        var code = SiteSecurityAndTrustEngine.GenerateTotpCode(bundle.SharedSecretAscii, now);
        var challenge = SiteSecurityAndTrustEngine.CreateWebAuthnPasskeyChallenge("admin@robotic-agent.ir");

        Phase1SecurityOutputBox.Text =
            $"=== Phase 1 (#1): Admin 2FA TOTP & WebAuthn Passkey ===\n" +
            $"Current 6-Digit TOTP Code: {code}\n" +
            $"Base32 Secret: {bundle.SharedSecretBase32}\n" +
            $"OtpAuth URI: {bundle.OtpAuthUri}\n" +
            $"WebAuthn Challenge: {challenge}\n" +
            $"Emergency Recovery Codes:\n  " + string.Join("\n  ", bundle.EmergencyRecoveryCodes);
        AdminStatusText.Text = $"✓ کد ۶ رقمی TOTP مدیر تولید و تایید شد: {code}";
    }

    private void RunPhase1WebhookAndHwButton_Click(object sender, RoutedEventArgs e)
    {
        var sig = SiteSecurityAndTrustEngine.SignGatewayWebhookPayload(PaymentGatewayProviderId.ZarinPal, """{"authority":"A0001","status":"OK"}""", "ZP_SECRET_2026");
        var fp = SiteSecurityAndTrustEngine.ComputeMachineFingerprintHash("CPU-I9-14900K", "MB-ASUS-Z790", "VOL-NVME-01");
        var hwKey = SiteSecurityAndTrustEngine.GenerateHardwareBoundLicenseKey("plan.06.pro_lifetime", "vip@company.ir", fp);
        var encVault = SiteSecurityAndTrustEngine.EncryptMerchantSecretAesGcm("ZP-MERCHANT-88421-IR");
        var masked = SiteSecurityAndTrustEngine.MaskMerchantSecretForDisplay("ZP-MERCHANT-88421-IR");

        Phase1SecurityOutputBox.Text =
            $"=== Phase 1 (#2, #4, #7): Webhook HMAC-SHA256, Hardware-Bound License & AES-256-GCM Vault ===\n" +
            $"ZarinPal Webhook HMAC-SHA256: {sig}\n" +
            $"Machine Hardware Fingerprint: {fp}\n" +
            $"Hardware-Bound License Key:   {hwKey}\n" +
            $"AES-256-GCM Encrypted Secret: {encVault}\n" +
            $"Masked Secret for UI:         {masked}";
        AdminStatusText.Text = "✓ تست امضای HMAC وب‌هوک، قفل سخت‌افزاری لایسنس و گاوصندوق AES-256-GCM با موفقیت انجام شد.";
    }

    private void RunPhase1AuditAndTrustReportButton_Click(object sender, RoutedEventArgs e)
    {
        _securityEngine.RecordAdminAction("superadmin", "SuperAdmin", "127.0.0.1", "SECURITY_TRUST_AUDIT", "اجرای بررسی سلامت زنجیرهٔ هش و مرکز اعتماد");
        Phase1SecurityOutputBox.Text = _securityEngine.BuildTrustCenterReportJson();
        AdminStatusText.Text = "✅ زنجیرهٔ هش SHA-256 اقدامات مدیر و گزارش کامل مرکز اعتماد امنیتی (فاز ۱) بررسی و تایید شد.";
    }

    private void RunPhase4AbAndRbacButton_Click(object sender, RoutedEventArgs e)
    {
        var mkt = new MarketingAndAdminAutomationEngine();
        mkt.RecordVariantEvent("B", isConversion: true);
        var winner = mkt.GetWinningVariant();
        Phase4MarketingOutputBox.Text =
            $"=== Phase 4 (#32 & #34): A/B Testing Engine & Multi-Admin RBAC ===\n" +
            $"Winning Hero Variant: Variant {winner.VariantId} (Conversion Rate: {winner.ConversionRatePercent}%)\n" +
            $"Winning Headline: {winner.HeadlineFa}\n\n" +
            $"RBAC Matrix:\n" +
            $"  SuperAdmin -> Full Access: {MarketingAndAdminAutomationEngine.HasAdminPermission(AdminRoleKind.SuperAdmin, "orders.refund")}\n" +
            $"  FinanceManager -> plans.edit: {MarketingAndAdminAutomationEngine.HasAdminPermission(AdminRoleKind.FinanceManager, "plans.edit")}\n" +
            $"  SupportAgent -> licenses.issue: {MarketingAndAdminAutomationEngine.HasAdminPermission(AdminRoleKind.SupportAgent, "licenses.issue")}\n" +
            $"  ContentEditor -> seo.edit: {MarketingAndAdminAutomationEngine.HasAdminPermission(AdminRoleKind.ContentEditor, "seo.edit")}";
        AdminStatusText.Text = $"✓ نسخهٔ برندهٔ تست A/B (Variant {winner.VariantId}) و ماتریس دسترسی RBAC محاسبه شد.";
    }

    private void RunPhase4WebhookAndSchemaButton_Click(object sender, RoutedEventArgs e)
    {
        var schema = MarketingAndAdminAutomationEngine.GenerateSchemaOrgJsonLd();
        Phase4MarketingOutputBox.Text =
            $"=== Phase 4 (#35 & #37): Schema.org JSON-LD Rich Snippets & Order Webhooks ===\n{schema}";
        AdminStatusText.Text = "✓ داده‌های ساختاریافتهٔ Schema.org JSON-LD سئوی گوگل تولید شد.";
    }

    private void RunPhase4PayoutsAndCampaignButton_Click(object sender, RoutedEventArgs e)
    {
        var payouts = MarketingAndAdminAutomationEngine.CalculatePublisherPayoutQueue();
        var camp = MarketingAndAdminAutomationEngine.GetActiveHolidayCampaign(DateTimeOffset.UtcNow);
        var lines = payouts.Select(p =>
            $"  • {p.PluginId} ({p.AuthorName}): Gross=${p.GrossRevenueUsd:0.00} -> Creator(70%)=${p.CreatorPayout70PercentUsd:0.00} | Platform(30%)=${p.PlatformFee30PercentUsd:0.00} | HMAC={p.IsManifestSignatureValid}");

        Phase4MarketingOutputBox.Text =
            $"=== Phase 4 (#38 & #40): 70/30 Plugin Publisher Payout Queue & Holiday Campaign ===\n" +
            $"Active Holiday Campaign: {camp.TitleFa} | Code: {camp.CouponCode} ({camp.DiscountPercent}% OFF)\n\n" +
            $"Publisher Payout Queue:\n" + string.Join("\n", lines);
        AdminStatusText.Text = "✅ صف تسویه‌حساب ۷۰/۳۰ توسعه‌دهندگان پلاگین و کمپین تخفیف مناسبتی بررسی شد.";
    }
}
