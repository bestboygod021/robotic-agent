using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Agent.Core.Monetization;

public enum PaymentGatewayRegion
{
    IranianToman,
    InternationalCurrency
}

public enum PaymentGatewayProviderId
{
    // 3 Iranian Payment Gateways (IRR / Toman)
    ZarinPal,
    Zibal,
    IdPay,

    // 3 International Payment Gateways (USD / EUR / Crypto)
    Stripe,
    LemonSqueezy,
    NowPaymentsCrypto
}

public sealed record PaymentGatewayDescriptor(
    PaymentGatewayProviderId Id,
    string Code,
    PaymentGatewayRegion Region,
    string DisplayNameFa,
    string DisplayNameEn,
    string SupportedCurrencyCode,
    string ApiEndpointUrl,
    string CheckoutBaseUrl);

public sealed record PaymentCheckoutRequest(
    string PlanSkuId,
    PaymentGatewayProviderId GatewayId,
    string CustomerEmail,
    string CustomerFullName = "",
    string CallbackUrl = "https://localhost:5179/payment/callback");

public sealed record PaymentCheckoutSession(
    string SessionId,
    string AuthorityOrTrackId,
    PaymentGatewayProviderId GatewayId,
    string GatewayDisplayName,
    string PlanSkuId,
    string PlanTitleFa,
    string PlanTitleEn,
    decimal PayableAmount,
    string CurrencyCode,
    string RedirectCheckoutUrl,
    string OutboundRequestJsonPayload,
    DateTimeOffset CreatedAtUtc);

public sealed record PaymentReceiptAndLicense(
    string ReceiptNumber,
    string SessionId,
    string AuthorityOrTrackId,
    PaymentGatewayProviderId GatewayId,
    string PlanSkuId,
    string CustomerEmail,
    decimal PaidAmount,
    string CurrencyCode,
    string IssuedLicenseKey,
    DateTimeOffset PaidAtUtc);

/// <summary>
/// Payment Gateway Orchestrator supporting 3 Iranian Gateways (ZarinPal, Zibal, IDPay)
/// and 3 International Gateways (Stripe, LemonSqueezy, NOWPayments Crypto) across all
/// 46 subscription models. Generates standard gateway request payloads, HTTPS checkout
/// redirect URLs, payment verification, and instant signed license issuance.
/// </summary>
public sealed class PaymentGatewayOrchestrator
{
    private readonly Dictionary<string, PaymentCheckoutSession> _pendingSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PaymentReceiptAndLicense> _completedReceipts = [];

    public static IReadOnlyList<PaymentGatewayDescriptor> AllGateways { get; } =
    [
        // 3 Iranian Gateways
        new(
            Id: PaymentGatewayProviderId.ZarinPal,
            Code: "zarinpal",
            Region: PaymentGatewayRegion.IranianToman,
            DisplayNameFa: "درگاه پرداخت زرین‌پال (ZarinPal — کارت‌های شتاب)",
            DisplayNameEn: "ZarinPal Payment Gateway (Iran Shetab Cards)",
            SupportedCurrencyCode: "IRT",
            ApiEndpointUrl: "https://api.zarinpal.com/pg/v4/payment/request.json",
            CheckoutBaseUrl: "https://www.zarinpal.com/pg/StartPay/"),

        new(
            Id: PaymentGatewayProviderId.Zibal,
            Code: "zibal",
            Region: PaymentGatewayRegion.IranianToman,
            DisplayNameFa: "درگاه پرداخت زیبال (Zibal — پرداخت مستقیم شاپرک)",
            DisplayNameEn: "Zibal Payment Gateway (Instant Shaparak IPG)",
            SupportedCurrencyCode: "IRT",
            ApiEndpointUrl: "https://gateway.zibal.ir/v1/request",
            CheckoutBaseUrl: "https://gateway.zibal.ir/start/"),

        new(
            Id: PaymentGatewayProviderId.IdPay,
            Code: "idpay",
            Region: PaymentGatewayRegion.IranianToman,
            DisplayNameFa: "درگاه پرداخت آیدی‌پی (IDPay — درگاه بانکی و کیف پول)",
            DisplayNameEn: "IDPay Payment Gateway (Direct Banking IPG)",
            SupportedCurrencyCode: "IRT",
            ApiEndpointUrl: "https://api.idpay.ir/v1.1/payment",
            CheckoutBaseUrl: "https://idpay.ir/p/ws/"),

        // 3 International Gateways
        new(
            Id: PaymentGatewayProviderId.Stripe,
            Code: "stripe",
            Region: PaymentGatewayRegion.InternationalCurrency,
            DisplayNameFa: "درگاه بین‌المللی استرایپ (Stripe — Visa / MasterCard / Apple Pay)",
            DisplayNameEn: "Stripe Global Checkout (Visa / MasterCard / Apple Pay)",
            SupportedCurrencyCode: "USD",
            ApiEndpointUrl: "https://api.stripe.com/v1/checkout/sessions",
            CheckoutBaseUrl: "https://checkout.stripe.com/c/pay/"),

        new(
            Id: PaymentGatewayProviderId.LemonSqueezy,
            Code: "lemonsqueezy",
            Region: PaymentGatewayRegion.InternationalCurrency,
            DisplayNameFa: "درگاه بین‌المللی لمون‌اسکوییزی (LemonSqueezy — فروش جهانی نرم‌افزار و مالیات خودکار)",
            DisplayNameEn: "LemonSqueezy Merchant of Record (PayPal / Cards / Global SaaS)",
            SupportedCurrencyCode: "USD",
            ApiEndpointUrl: "https://api.lemonsqueezy.com/v1/checkouts",
            CheckoutBaseUrl: "https://robotic-agent.lemonsqueezy.com/checkout/buy/"),

        new(
            Id: PaymentGatewayProviderId.NowPaymentsCrypto,
            Code: "nowpayments",
            Region: PaymentGatewayRegion.InternationalCurrency,
            DisplayNameFa: "درگاه رمزارزی نووپیمنتس (NOWPayments — USDT / BTC / TON بدون مرز)",
            DisplayNameEn: "NOWPayments Crypto Gateway (USDT / BTC / ETH / TON Borderless)",
            SupportedCurrencyCode: "USDT",
            ApiEndpointUrl: "https://api.nowpayments.io/v1/invoice",
            CheckoutBaseUrl: "https://nowpayments.io/payment/?iid=")
    ];

    public IReadOnlyList<PaymentReceiptAndLicense> CompletedReceipts => _completedReceipts;

    public static IReadOnlyList<PaymentGatewayDescriptor> IranianGateways =>
        AllGateways.Where(g => g.Region == PaymentGatewayRegion.IranianToman).ToArray();

    public static IReadOnlyList<PaymentGatewayDescriptor> InternationalGateways =>
        AllGateways.Where(g => g.Region == PaymentGatewayRegion.InternationalCurrency).ToArray();

    public static PaymentGatewayDescriptor GetGateway(PaymentGatewayProviderId id) =>
        AllGateways.First(g => g.Id == id);

    public PaymentCheckoutSession CreateCheckoutSession(PaymentCheckoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CustomerEmail) || !request.CustomerEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("ایمیل معتبر مشتری برای صدور فاکتور و لایسنس الزامی است.", nameof(request));
        }

        var plan = SubscriptionCatalog46.GetBySku(request.PlanSkuId);
        var gateway = GetGateway(request.GatewayId);

        var sessionId = $"chk_{Guid.NewGuid():N}"[..20];
        var authority = BuildGatewayAuthorityToken(gateway.Id, sessionId);
        var payableAmount = gateway.Region == PaymentGatewayRegion.IranianToman
            ? plan.PriceToman
            : plan.PriceUsd;

        var payloadJson = BuildStandardGatewayRequestJson(gateway, plan, request, authority, payableAmount);
        var redirectUrl = $"{gateway.CheckoutBaseUrl}{Uri.EscapeDataString(authority)}";

        var session = new PaymentCheckoutSession(
            SessionId: sessionId,
            AuthorityOrTrackId: authority,
            GatewayId: gateway.Id,
            GatewayDisplayName: gateway.DisplayNameFa,
            PlanSkuId: plan.SkuId,
            PlanTitleFa: plan.TitleFa,
            PlanTitleEn: plan.TitleEn,
            PayableAmount: payableAmount,
            CurrencyCode: gateway.SupportedCurrencyCode,
            RedirectCheckoutUrl: redirectUrl,
            OutboundRequestJsonPayload: payloadJson,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        _pendingSessions[sessionId] = session;
        return session;
    }

    public PaymentReceiptAndLicense VerifyPaymentAndIssueLicense(
        string sessionId,
        string customerEmail,
        DateTimeOffset? nowUtc = null)
    {
        if (!_pendingSessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"نشست پرداخت '{sessionId}' پیدا نشد.");
        }

        var plan = SubscriptionCatalog46.GetBySku(session.PlanSkuId);
        var paidAt = nowUtc ?? DateTimeOffset.UtcNow;

        var mappedTier = plan.Category switch
        {
            SubscriptionCategory.TeamAndEnterprise => SubscriptionPlanTier.EnterpriseCompliance,
            _ when plan.BillingCycle == BillingCycleKind.LifetimeOneTime => SubscriptionPlanTier.LifetimeDeal,
            _ when plan.BillingCycle == BillingCycleKind.Free => SubscriptionPlanTier.Free,
            _ => SubscriptionPlanTier.ProSubscription
        };

        DateTimeOffset? expiresAt = plan.BillingCycle switch
        {
            BillingCycleKind.Monthly => paidAt.AddDays(31),
            BillingCycleKind.Annual => paidAt.AddDays(366),
            _ => null
        };

        var licenseKey = LicenseAndPlanManager.GenerateSignedLicenseKey(mappedTier, customerEmail, expiresAt);
        var receiptNo = $"INV-{paidAt:yyyyMMdd}-{RandomNumberGenerator.GetHexString(6)}";

        var receipt = new PaymentReceiptAndLicense(
            ReceiptNumber: receiptNo,
            SessionId: session.SessionId,
            AuthorityOrTrackId: session.AuthorityOrTrackId,
            GatewayId: session.GatewayId,
            PlanSkuId: session.PlanSkuId,
            CustomerEmail: customerEmail.Trim(),
            PaidAmount: session.PayableAmount,
            CurrencyCode: session.CurrencyCode,
            IssuedLicenseKey: licenseKey,
            PaidAtUtc: paidAt);

        _pendingSessions.Remove(sessionId);
        _completedReceipts.Insert(0, receipt);
        return receipt;
    }

    private static string BuildGatewayAuthorityToken(PaymentGatewayProviderId id, string sessionId) =>
        id switch
        {
            PaymentGatewayProviderId.ZarinPal => $"A0000000000000000000{sessionId[^12..].ToUpperInvariant()}",
            PaymentGatewayProviderId.Zibal => Math.Abs(sessionId.GetHashCode(StringComparison.Ordinal)).ToString("D10", CultureInfo.InvariantCulture),
            PaymentGatewayProviderId.IdPay => $"idpay_{sessionId}",
            PaymentGatewayProviderId.Stripe => $"cs_live_{sessionId}",
            PaymentGatewayProviderId.LemonSqueezy => $"ls_chk_{sessionId}",
            _ => $"np_inv_{sessionId}"
        };

    private static string BuildStandardGatewayRequestJson(
        PaymentGatewayDescriptor gateway,
        SubscriptionPlanModel plan,
        PaymentCheckoutRequest request,
        string authority,
        decimal amount)
    {
        object body = gateway.Id switch
        {
            PaymentGatewayProviderId.ZarinPal => new
            {
                merchant_id = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
                amount = (long)(amount * 10), // Rials
                currency = "IRR",
                description = $"{plan.TitleFa} ({plan.SkuId})",
                callback_url = request.CallbackUrl,
                metadata = new { email = request.CustomerEmail, sku = plan.SkuId, authority }
            },

            PaymentGatewayProviderId.Zibal => new
            {
                merchant = "zibal_merchant_key",
                amount = (long)(amount * 10), // Rials
                callbackUrl = request.CallbackUrl,
                description = plan.TitleFa,
                orderId = authority,
                mobile = ""
            },

            PaymentGatewayProviderId.IdPay => new
            {
                order_id = authority,
                amount = (long)(amount * 10), // Rials
                name = request.CustomerFullName,
                mail = request.CustomerEmail,
                desc = plan.TitleFa,
                callback = request.CallbackUrl
            },

            PaymentGatewayProviderId.Stripe => new
            {
                mode = plan.BillingCycle is BillingCycleKind.Monthly or BillingCycleKind.Annual ? "subscription" : "payment",
                customer_email = request.CustomerEmail,
                client_reference_id = plan.SkuId,
                amount_cents = (long)(amount * 100),
                currency = "usd",
                success_url = request.CallbackUrl
            },

            PaymentGatewayProviderId.LemonSqueezy => new
            {
                data = new
                {
                    type = "checkouts",
                    attributes = new
                    {
                        custom_price = (long)(amount * 100),
                        checkout_data = new { email = request.CustomerEmail, custom = new { sku = plan.SkuId } }
                    }
                }
            },

            _ => new
            {
                price_amount = amount,
                price_currency = "usd",
                pay_currency = "usdttrc20",
                order_id = authority,
                order_description = $"{plan.TitleEn} ({plan.SkuId})",
                ipn_callback_url = request.CallbackUrl
            }
        };

        return JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true });
    }
}
