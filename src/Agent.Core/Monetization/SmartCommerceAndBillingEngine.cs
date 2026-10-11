using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Monetization;

public sealed record GeoGatewayRecommendation(
    string CountryIso2,
    string RecommendedCurrency,
    PaymentGatewayProviderId PrimaryGateway,
    IReadOnlyList<PaymentGatewayDescriptor> OrderedGateways,
    string RoutingReasonFa);

public sealed record OfficialTaxInvoiceDocument(
    string InvoiceNumber,
    string VerificationQrPayload,
    string DigitalSignatureHex,
    string StandaloneBilingualHtml);

public sealed record BnplInstallmentItem(
    int InstallmentNumber,
    DateTimeOffset DueDateUtc,
    decimal AmountUsd,
    long AmountToman,
    string Status);

public sealed record BnplSchedulePlan(
    string PlanSkuId,
    decimal TotalUsd,
    long TotalToman,
    IReadOnlyList<BnplInstallmentItem> Installments);

public sealed record ProratedUpgradeQuote(
    string CurrentPlanSku,
    string TargetPlanSku,
    int RemainingDaysOnCurrentPlan,
    decimal UnusedCreditUsd,
    long UnusedCreditToman,
    decimal PayableDifferenceUsd,
    long PayableDifferenceToman);

public sealed record AbandonedCartRecoveryOffer(
    string SessionId,
    string CustomerEmail,
    string PlanSkuId,
    string RecoveryCouponCode,
    int DiscountPercent,
    decimal DiscountedPriceUsd,
    long DiscountedPriceToman,
    string RecoveryMessageFa);

public sealed record OrderBumpQuote(
    string MainPlanSku,
    string BumpAddOnPlanSku,
    decimal MainPriceUsd,
    decimal BumpDiscountedUsd,
    decimal CombinedTotalUsd,
    long MainPriceToman,
    long BumpDiscountedToman,
    long CombinedTotalToman);

public sealed record CustomTeamSeatsQuote(
    int SeatCount,
    int VolumeDiscountPercent,
    decimal PerSeatMonthlyUsd,
    decimal TotalMonthlyUsd,
    long TotalMonthlyToman);

/// <summary>
/// Phase 2 (#11..#20): Smart Payment Routing, Dynamic Exchange Rate Sync, Official Tax Invoice
/// with QR, 4-Pay BNPL Installments, Prorated Upgrades, User Credit Wallet, Abandoned Cart
/// Recovery, 1-Click Order Bump, Team Seat Volume Slider, and Automated Refund + License CRL.
/// </summary>
public sealed class SmartCommerceAndBillingEngine
{
    private readonly Dictionary<string, (long BalanceToman, decimal BalanceUsd)> _wallets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _revokedLicenseKeys = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> RevokedLicenseKeys => _revokedLicenseKeys;

    // =========================================================================
    // #11: Smart Geo-IP Currency & Gateway Router
    // =========================================================================
    public static GeoGatewayRecommendation RecommendGatewaysForCountry(string countryIso2)
    {
        var isIran = string.Equals(countryIso2?.Trim(), "IR", StringComparison.OrdinalIgnoreCase);
        if (isIran)
        {
            var ordered = PaymentGatewayOrchestrator.IranianGateways
                .Concat(PaymentGatewayOrchestrator.InternationalGateways)
                .ToArray();
            return new GeoGatewayRecommendation(
                CountryIso2: "IR",
                RecommendedCurrency: "IRT",
                PrimaryGateway: PaymentGatewayProviderId.ZarinPal,
                OrderedGateways: ordered,
                RoutingReasonFa: "تشخیص موقعیت ایران (IR): اولویت با درگاه‌های شتاب تومانی (زرین‌پال، زیبال، آیدی‌پی)");
        }

        var globalOrdered = PaymentGatewayOrchestrator.InternationalGateways
            .Concat(PaymentGatewayOrchestrator.IranianGateways)
            .ToArray();
        return new GeoGatewayRecommendation(
            CountryIso2: string.IsNullOrWhiteSpace(countryIso2) ? "US" : countryIso2.Trim().ToUpperInvariant(),
            RecommendedCurrency: "USD",
            PrimaryGateway: PaymentGatewayProviderId.Stripe,
            OrderedGateways: globalOrdered,
            RoutingReasonFa: "تشخیص موقعیت بین‌المللی: اولویت با درگاه‌های ارزی و رمزارزی (Stripe, LemonSqueezy, NOWPayments)");
    }

    // =========================================================================
    // #12: Dynamic USD-to-Toman Exchange Rate Synchronizer for All 46 Plans
    // =========================================================================
    public static IReadOnlyDictionary<string, long> CalculateDynamicTomanPricesForAll46Plans(long usdToTomanRate)
    {
        if (usdToTomanRate < 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(usdToTomanRate), "نرخ تبدیل دلار به تومان معتبر نیست.");
        }

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in SubscriptionCatalog46.AllPlans)
        {
            if (plan.PriceUsd <= 0m)
            {
                result[plan.SkuId] = 0;
                continue;
            }

            var raw = plan.PriceUsd * usdToTomanRate;
            var roundedToFiveThousand = (long)(Math.Round(raw / 5_000m) * 5_000m);
            result[plan.SkuId] = Math.Max(5_000, roundedToFiveThousand);
        }

        return result;
    }

    // =========================================================================
    // #13: Bilingual Official Tax Invoice with Digital Signature & QR Payload
    // =========================================================================
    public static OfficialTaxInvoiceDocument GenerateOfficialInvoiceWithQr(PaymentReceiptAndLicense receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var plan = SubscriptionCatalog46.GetBySku(receipt.PlanSkuId);
        var canonical = $"{receipt.ReceiptNumber}|{receipt.CustomerEmail}|{receipt.PlanSkuId}|{receipt.PaidAmount}|{receipt.CurrencyCode}|{receipt.IssuedLicenseKey}";
        var sigBytes = SHA256.HashData(Encoding.UTF8.GetBytes("RA_INVOICE_SIGN_2026:" + canonical));
        var sigHex = Convert.ToHexString(sigBytes)[..24];
        var qrPayload = $"https://robotic-agent.ir/verify-invoice?inv={Uri.EscapeDataString(receipt.ReceiptNumber)}&sig={sigHex}";

        var html = $$"""
        <!DOCTYPE html>
        <html lang="fa" dir="rtl">
        <head><meta charset="utf-8"><title>فاکتور رسمی {{receipt.ReceiptNumber}}</title></head>
        <body style="font-family:Tahoma,sans-serif;padding:24px;">
          <h2>🧾 صورت‌حساب رسمی فروش نرم‌افزار و لایسنس دیجیتال / Official Tax Invoice</h2>
          <p><strong>شماره فاکتور / Invoice No:</strong> {{receipt.ReceiptNumber}}</p>
          <p><strong>خریدار / Customer:</strong> {{receipt.CustomerEmail}}</p>
          <p><strong>طرح اشتراک / Plan:</strong> {{plan.TitleFa}} ({{plan.TitleEn}})</p>
          <p><strong>مبلغ پرداخت‌شده / Amount Paid:</strong> {{receipt.PaidAmount:N0}} {{receipt.CurrencyCode}}</p>
          <p><strong>کلید لایسنس / License Key:</strong> <code>{{receipt.IssuedLicenseKey}}</code></p>
          <p><strong>امضای دیجیتال اصالت فاکتور / Digital Seal:</strong> <code>{{sigHex}}</code></p>
          <p><strong>لینک استعلام QR:</strong> {{qrPayload}}</p>
        </body>
        </html>
        """;

        return new OfficialTaxInvoiceDocument(receipt.ReceiptNumber, qrPayload, sigHex, html);
    }

    // =========================================================================
    // #14: 4-Pay BNPL Installment Schedule Engine (Buy Now, Pay in 4)
    // =========================================================================
    public static BnplSchedulePlan BuildBnplFourPaySchedule(string planSkuId, DateTimeOffset? startDateUtc = null)
    {
        var plan = SubscriptionCatalog46.GetBySku(planSkuId);
        var start = startDateUtc ?? DateTimeOffset.UtcNow;
        var quarterUsd = Math.Round(plan.PriceUsd / 4m, 2);
        var quarterToman = (long)(Math.Round(plan.PriceToman / 4000m) * 1000m);

        var items = new List<BnplInstallmentItem>(4);
        for (var i = 0; i < 4; i++)
        {
            items.Add(new BnplInstallmentItem(
                InstallmentNumber: i + 1,
                DueDateUtc: start.AddDays(i * 30),
                AmountUsd: quarterUsd,
                AmountToman: quarterToman,
                Status: i == 0 ? "PAID_INITIAL_25_PERCENT" : "SCHEDULED_MONTHLY"));
        }

        return new BnplSchedulePlan(plan.SkuId, plan.PriceUsd, plan.PriceToman, items);
    }

    // =========================================================================
    // #15: Prorated Plan Upgrade Calculator
    // =========================================================================
    public static ProratedUpgradeQuote CalculateProratedUpgradeQuote(
        string currentPlanSku,
        string targetPlanSku,
        int remainingDaysOnCurrentPlan,
        int totalCycleDays = 30)
    {
        var current = SubscriptionCatalog46.GetBySku(currentPlanSku);
        var target = SubscriptionCatalog46.GetBySku(targetPlanSku);

        var ratio = Math.Clamp((decimal)remainingDaysOnCurrentPlan / Math.Max(1, totalCycleDays), 0m, 1m);
        var creditUsd = Math.Round(current.PriceUsd * ratio, 2);
        var creditToman = (long)(Math.Round((current.PriceToman * ratio) / 1000m) * 1000m);

        var diffUsd = Math.Max(0m, target.PriceUsd - creditUsd);
        var diffToman = Math.Max(0L, target.PriceToman - creditToman);

        return new ProratedUpgradeQuote(
            CurrentPlanSku: current.SkuId,
            TargetPlanSku: target.SkuId,
            RemainingDaysOnCurrentPlan: remainingDaysOnCurrentPlan,
            UnusedCreditUsd: creditUsd,
            UnusedCreditToman: creditToman,
            PayableDifferenceUsd: diffUsd,
            PayableDifferenceToman: diffToman);
    }

    // =========================================================================
    // #16: In-Site User Credit Wallet (Toman & USD)
    // =========================================================================
    public (long BalanceToman, decimal BalanceUsd) TopUpWallet(string userEmail, long addToman, decimal addUsd)
    {
        var current = _wallets.GetValueOrDefault(userEmail, (0L, 0m));
        var updated = (current.BalanceToman + Math.Max(0, addToman), current.BalanceUsd + Math.Max(0m, addUsd));
        _wallets[userEmail] = updated;
        return updated;
    }

    public bool TryPayPlanFromWallet(string userEmail, string planSkuId, bool payInToman, out string issuedLicenseKey)
    {
        var plan = SubscriptionCatalog46.GetBySku(planSkuId);
        var current = _wallets.GetValueOrDefault(userEmail, (0L, 0m));

        if (payInToman)
        {
            if (current.BalanceToman < plan.PriceToman)
            {
                issuedLicenseKey = string.Empty;
                return false;
            }

            _wallets[userEmail] = (current.BalanceToman - plan.PriceToman, current.BalanceUsd);
        }
        else
        {
            if (current.BalanceUsd < plan.PriceUsd)
            {
                issuedLicenseKey = string.Empty;
                return false;
            }

            _wallets[userEmail] = (current.BalanceToman, current.BalanceUsd - plan.PriceUsd);
        }

        issuedLicenseKey = LicenseAndPlanManager.GenerateSignedLicenseKey(SubscriptionPlanTier.ProSubscription, userEmail);
        return true;
    }

    // =========================================================================
    // #17: Abandoned Checkout Recovery & 10% Incentive Coupon Generator
    // =========================================================================
    public static AbandonedCartRecoveryOffer CreateAbandonedCartRecoveryOffer(PaymentCheckoutSession abandonedSession, string customerEmail)
    {
        var plan = SubscriptionCatalog46.GetBySku(abandonedSession.PlanSkuId);
        var code = $"COMEBACK10-{abandonedSession.SessionId[^4..].ToUpperInvariant()}";
        var discUsd = Math.Round(plan.PriceUsd * 0.90m, 2);
        var discToman = (long)(Math.Round((plan.PriceToman * 0.90m) / 1000m) * 1000m);

        return new AbandonedCartRecoveryOffer(
            SessionId: abandonedSession.SessionId,
            CustomerEmail: customerEmail,
            PlanSkuId: plan.SkuId,
            RecoveryCouponCode: code,
            DiscountPercent: 10,
            DiscountedPriceUsd: discUsd,
            DiscountedPriceToman: discToman,
            RecoveryMessageFa: $"خرید شما برای «{plan.TitleFa}» ناتمام ماند! با کد {code} همین الان با ۱۰٪ تخفیف ویژه تکمیل کنید.");
    }

    // =========================================================================
    // #18: One-Click Order Bump & Cross-Sell Engine (50% Off Add-on at Checkout)
    // =========================================================================
    public static OrderBumpQuote CalculateOrderWithBump(string mainPlanSku, string bumpAddOnPlanSku = "plan.41.prompt_pack_1000")
    {
        var main = SubscriptionCatalog46.GetBySku(mainPlanSku);
        var bump = SubscriptionCatalog46.GetBySku(bumpAddOnPlanSku);

        var bumpHalfUsd = Math.Round(bump.PriceUsd * 0.50m, 2);
        var bumpHalfToman = (long)(Math.Round((bump.PriceToman * 0.50m) / 1000m) * 1000m);

        return new OrderBumpQuote(
            MainPlanSku: main.SkuId,
            BumpAddOnPlanSku: bump.SkuId,
            MainPriceUsd: main.PriceUsd,
            BumpDiscountedUsd: bumpHalfUsd,
            CombinedTotalUsd: main.PriceUsd + bumpHalfUsd,
            MainPriceToman: main.PriceToman,
            BumpDiscountedToman: bumpHalfToman,
            CombinedTotalToman: main.PriceToman + bumpHalfToman);
    }

    // =========================================================================
    // #19: Interactive Team Seat Volume Pricing Calculator
    // =========================================================================
    public static CustomTeamSeatsQuote CalculateCustomTeamSeatQuote(int seatCount)
    {
        var clampedSeats = Math.Clamp(seatCount, 2, 500);
        var discountPercent = clampedSeats switch
        {
            >= 50 => 35,
            >= 20 => 25,
            >= 10 => 15,
            >= 5 => 10,
            _ => 0
        };

        var basePerSeatUsd = 6.00m;
        var netPerSeatUsd = Math.Round(basePerSeatUsd * (100m - discountPercent) / 100m, 2);
        var totalUsd = netPerSeatUsd * clampedSeats;
        var totalToman = (long)(totalUsd * 60_000m);

        return new CustomTeamSeatsQuote(
            SeatCount: clampedSeats,
            VolumeDiscountPercent: discountPercent,
            PerSeatMonthlyUsd: netPerSeatUsd,
            TotalMonthlyUsd: totalUsd,
            TotalMonthlyToman: totalToman);
    }

    // =========================================================================
    // #20: Automated 7-Day Refund & Instant License Key Revocation List (CRL)
    // =========================================================================
    public bool ProcessRefundAndRevokeLicense(PaymentReceiptAndLicense receipt, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if ((now - receipt.PaidAtUtc).TotalDays > 7.0)
        {
            return false;
        }

        _revokedLicenseKeys.Add(receipt.IssuedLicenseKey);
        return true;
    }

    public bool IsLicenseKeyRevoked(string licenseKey) =>
        !string.IsNullOrWhiteSpace(licenseKey) && _revokedLicenseKeys.Contains(licenseKey.Trim());
}
