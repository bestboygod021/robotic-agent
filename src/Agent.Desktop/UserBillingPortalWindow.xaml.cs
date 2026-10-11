using Agent.Core.Monetization;
using Agent.Desktop.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Agent.Desktop;

public partial class UserBillingPortalWindow : Window
{
    private readonly UserBillingPortalViewModel _viewModel = new();

    public UserBillingPortalWindow()
    {
        InitializeComponent();
        PopulateGateways();
        PopulatePlansList();
    }

    private void PopulateGateways()
    {
        GatewayComboBox.Items.Clear();
        foreach (var gw in _viewModel.AllGateways)
        {
            var regionBadge = gw.Region == PaymentGatewayRegion.IranianToman ? "🇮🇷 درگاه ایرانی" : "🌍 درگاه بین‌المللی";
            GatewayComboBox.Items.Add(new ComboBoxItem
            {
                Tag = gw,
                Content = $"{regionBadge} — {gw.DisplayNameFa} ({gw.SupportedCurrencyCode})"
            });
        }

        GatewayComboBox.SelectedIndex = 0;
    }

    private void PopulatePlansList()
    {
        PlansListBox.Items.Clear();
        foreach (var p in _viewModel.VisiblePlans)
        {
            var priceText = p.PriceUsd == 0
                ? "رایگان (Free)"
                : $"{p.PriceToman:N0} تومان  |  ${p.PriceUsd:0.00} USD";

            PlansListBox.Items.Add(new ListBoxItem
            {
                Tag = p,
                Content = $"{p.TitleFa}   —   [{priceText}]"
            });
        }

        if (PlansListBox.Items.Count > 0)
        {
            PlansListBox.SelectedIndex = Math.Min(5, PlansListBox.Items.Count - 1);
        }
    }

    private void PlansListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlansListBox.SelectedItem is not ListBoxItem item || item.Tag is not SubscriptionPlanModel plan)
        {
            return;
        }

        _viewModel.SelectedPlan = plan;
        SelectedPlanTitleText.Text = $"{plan.TitleFa}\n{plan.TitleEn}";
        SelectedPlanDescText.Text = $"{plan.DescriptionFa}\n({plan.DescriptionEn})";
        SelectedPlanPriceText.Text = plan.PriceUsd == 0
            ? "مبلغ قابل پرداخت: رایگان ($0 / 0 تومان)"
            : $"مبلغ قابل پرداخت: {plan.PriceToman:N0} تومان (درگاه‌های ایرانی)  |  ${plan.PriceUsd:0.00} USD (درگاه‌های خارجی)  |  سقف: {plan.MaxFilesPerBatch:N0} فایل";
    }

    private void GatewayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GatewayComboBox.SelectedItem is ComboBoxItem item && item.Tag is PaymentGatewayDescriptor gw)
        {
            _viewModel.SelectedGateway = gw;
        }
    }

    private void ConnectGatewayButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.UserFullName = CustomerNameBox.Text?.Trim() ?? string.Empty;
            _viewModel.UserEmail = CustomerEmailBox.Text?.Trim() ?? string.Empty;
            var session = _viewModel.StartGatewayCheckout();

            GatewaySessionOutputBox.Text =
                $"✓ درگاه انتخابی: {session.GatewayDisplayName}\n" +
                $"✓ شناسهٔ نشست (Session ID): {session.SessionId}\n" +
                $"✓ کد رهگیری درگاه (Authority / TrackId): {session.AuthorityOrTrackId}\n" +
                $"✓ مبلغ: {session.PayableAmount:N0} {session.CurrencyCode}\n" +
                $"✓ آدرس هدایت به درگاه (Redirect URL):\n{session.RedirectCheckoutUrl}\n\n" +
                $"--- JSON Request Payload ---\n{session.OutboundRequestJsonPayload}";

            PortalStatusText.Text = _viewModel.StatusMessage;
        }
        catch (Exception ex)
        {
            PortalStatusText.Text = $"خطا در اتصال به درگاه: {ex.Message}";
        }
    }

    private void VerifyAndIssueKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.UserFullName = CustomerNameBox.Text?.Trim() ?? string.Empty;
            _viewModel.UserEmail = CustomerEmailBox.Text?.Trim() ?? string.Empty;
            var receipt = _viewModel.CompleteAndVerifyPayment();

            var ent = _viewModel.CurrentEntitlements;
            ActivePlanBannerText.Text =
                $"✓ اشتراک فعال فعلی: {ent.TierDisplayNameFa} | سقف هر نوبت: {ent.MaxFilesPerBatch:N0} فایل | شماره فاکتور: {receipt.ReceiptNumber}";

            GatewaySessionOutputBox.Text =
                $"🎉 پرداخت با موفقیت تأیید و لایسنس صادر شد!\n" +
                $"• شماره فاکتور رسمی: {receipt.ReceiptNumber}\n" +
                $"• طرح خریداری‌شده: {receipt.PlanSkuId}\n" +
                $"• مبلغ پرداخت‌شده: {receipt.PaidAmount:N0} {receipt.CurrencyCode}\n" +
                $"• کلید لایسنس امضاشده (فعال‌شده روی برنامه):\n{receipt.IssuedLicenseKey}";

            PortalStatusText.Text = _viewModel.StatusMessage;
        }
        catch (Exception ex)
        {
            PortalStatusText.Text = $"خطا در تأیید پرداخت: {ex.Message}";
        }
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e) => ApplyCategoryFilter(null);
    private void FilterPersonal_Click(object sender, RoutedEventArgs e) => ApplyCategoryFilter(SubscriptionCategory.PersonalAndGeneral);
    private void FilterSkills_Click(object sender, RoutedEventArgs e) => ApplyCategoryFilter(SubscriptionCategory.SkillFocusedPacks);
    private void FilterPlaybooks_Click(object sender, RoutedEventArgs e) => ApplyCategoryFilter(SubscriptionCategory.SmbJobPlaybooks);
    private void FilterCredits_Click(object sender, RoutedEventArgs e) => ApplyCategoryFilter(SubscriptionCategory.HybridAiAndPromptCredits);
    private void FilterEnterprise_Click(object sender, RoutedEventArgs e) => ApplyCategoryFilter(SubscriptionCategory.TeamAndEnterprise);

    private void SearchPlansBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchQuery = SearchPlansBox.Text ?? string.Empty;
        PopulatePlansList();
    }

    private void ApplyCategoryFilter(SubscriptionCategory? cat)
    {
        _viewModel.SelectedCategory = cat;
        PopulatePlansList();
    }

    private void ShowQrInvoiceButton_Click(object sender, RoutedEventArgs e)
    {
        var receipt = _viewModel.PurchaseHistory.FirstOrDefault() ?? _viewModel.CompleteAndVerifyPayment();
        var inv = SmartCommerceAndBillingEngine.GenerateOfficialInvoiceWithQr(receipt);
        GatewaySessionOutputBox.Text =
            $"=== فاکتور رسمی دوزبانه با مهر دیجیتال و لینک QR (#13) ===\n" +
            $"• شماره فاکتور: {inv.InvoiceNumber}\n" +
            $"• امضای دیجیتال اصالت: {inv.DigitalSignatureHex}\n" +
            $"• آدرس استعلام QR: {inv.VerificationQrPayload}";
        PortalStatusText.Text = $"✓ فاکتور رسمی {inv.InvoiceNumber} با امضای دیجیتال صادر شد.";
    }

    private void ShowBnplScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        var sku = _viewModel.SelectedPlan?.SkuId ?? "plan.06.pro_lifetime";
        var bnpl = SmartCommerceAndBillingEngine.BuildBnplFourPaySchedule(sku);
        var lines = bnpl.Installments.Select(i =>
            $"  قسط #{i.InstallmentNumber} ({i.DueDateUtc:yyyy-MM-dd}): {i.AmountToman:N0} تومان | ${i.AmountUsd:0.00} USD [{i.Status}]");
        GatewaySessionOutputBox.Text =
            $"=== جدول پرداخت اقساطی ۴ مرحله‌ای بدون بهره (#14 BNPL 4-Pay) ===\n" +
            $"طرح: {bnpl.PlanSkuId} | مجموع: {bnpl.TotalToman:N0} تومان (${bnpl.TotalUsd:0.00})\n" +
            string.Join("\n", lines);
        PortalStatusText.Text = "✓ برنامهٔ پرداخت اقساطی ۴ مرحله‌ای بدون بهره محاسبه شد.";
    }

    private void ShowProratedAndTeamQuoteButton_Click(object sender, RoutedEventArgs e)
    {
        var targetSku = _viewModel.SelectedPlan?.SkuId ?? "plan.04.pro_monthly";
        var proQuote = SmartCommerceAndBillingEngine.CalculateProratedUpgradeQuote("plan.02.plus_monthly", targetSku, 15, 30);
        var bumpQuote = SmartCommerceAndBillingEngine.CalculateOrderWithBump(targetSku);
        var teamQuote = SmartCommerceAndBillingEngine.CalculateCustomTeamSeatQuote(20);

        GatewaySessionOutputBox.Text =
            $"=== محاسبهٔ ارتقای پلکانی (#15)، پیشنهاد مکمل ۵۰٪ (#18) و تخفیف صندلی تیمی (#19) ===\n" +
            $"• اعتبار ۱۵ روز باقی‌مانده از پلن قبلی: {proQuote.UnusedCreditToman:N0} تومان (${proQuote.UnusedCreditUsd:0.00})\n" +
            $"• مابه‌التفاوت خالص ارتقا به {proQuote.TargetPlanSku}: {proQuote.PayableDifferenceToman:N0} تومان (${proQuote.PayableDifferenceUsd:0.00})\n" +
            $"• خرید به همراه بستهٔ مکمل ۱۰۰۰ پرامپت (۵۰٪ تخفیف): {bumpQuote.CombinedTotalToman:N0} تومان (${bumpQuote.CombinedTotalUsd:0.00})\n" +
            $"• پیش‌فاکتور تیمی ۲۰ کاربره ({teamQuote.VolumeDiscountPercent}٪ تخفیف گروهی): {teamQuote.TotalMonthlyToman:N0} تومان (${teamQuote.TotalMonthlyUsd:0.00})";
        PortalStatusText.Text = "✓ محاسبهٔ ارتقای پلکانی، پیشنهاد مکمل و تخفیف حجمی تیمی انجام شد.";
    }

    private void ShowPortalProFeaturesButton_Click(object sender, RoutedEventArgs e)
    {
        var portalPro = new CustomerPortalProService();
        var email = CustomerEmailBox.Text?.Trim() ?? "user@example.com";
        var sku = _viewModel.SelectedPlan?.SkuId ?? "plan.06.pro_lifetime";
        var licKey = _viewModel.PurchaseHistory.FirstOrDefault()?.IssuedLicenseKey ?? "RA-LTD-DEMO-2026";

        var deepLink = CustomerPortalProService.BuildDeepLinkActivationUri(licKey, sku, email);
        portalPro.RegisterDeviceSession(licKey, "DEV-WIN11-01", "Workstation-PC", "Windows 11");
        var refLink = CustomerPortalProService.BuildUserReferralLink(email);
        var subKey = CustomerPortalProService.IssueTeamEmployeeSubLicense(licKey, "emp1@company.ir", 1);
        var mcpTok = portalPro.IssueScopedMcpCliToken("mcp:tools:preview_and_execute");
        var encBackup = CustomerPortalProService.EncryptWorkflowBackupZeroKnowledge("""{"workflows":20,"skills":24}""", "UserSecret!2026");

        GatewaySessionOutputBox.Text =
            $"=== امکانات پیشرفتهٔ پنل کاربری مشتریان — فاز ۳ (#21 تا #30) ===\n" +
            $"• #21 لینک فعال‌سازی تک‌کلیکی دسکتاپ:\n  {deepLink}\n" +
            $"• #22 دستگاه‌های فعال روی لایسنس: {portalPro.GetActiveDevices(licKey).Count} از ۳ دستگاه مجاز\n" +
            $"• #24 لینک دعوت دوستان با ۲۰٪ پاداش نقدی:\n  {refLink}\n" +
            $"• #25 کلید لایسنس فرعی کارمند شرکت: {subKey}\n" +
            $"• #28 توکن اختصاصی سرور محلی MCP/CLI: {mcpTok.TokenSecret}\n" +
            $"• #30 بکاپ رمزنگاری‌شدهٔ سرتاسری (Zero-Knowledge E2E):\n  {encBackup}";
        PortalStatusText.Text = "✅ تمامی امکانات فاز ۳ پنل کاربری (Deep-Link، دستگاه‌ها، دعوت ۲۰٪، توکن MCP و بکاپ Zero-Knowledge) آماده است.";
    }
}
