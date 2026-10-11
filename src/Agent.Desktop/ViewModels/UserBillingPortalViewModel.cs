using Agent.Core.Monetization;
using System.Collections.ObjectModel;

namespace Agent.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Professional User Dashboard &amp; Billing Portal supporting
/// all 46 subscription models, 3 Iranian payment gateways (ZarinPal, Zibal, IDPay),
/// and 3 International payment gateways (Stripe, LemonSqueezy, NOWPayments Crypto).
/// </summary>
public sealed class UserBillingPortalViewModel : ObservableObject
{
    private readonly PaymentGatewayOrchestrator _gatewayOrchestrator = new();
    private readonly LicenseAndPlanManager _licenseManager = new();

    private string _userFullName = "کاربر حرفه‌ای رباتیک ایجنت";
    private string _userEmail = "user@example.com";
    private string _searchQuery = string.Empty;
    private SubscriptionCategory? _selectedCategory;
    private SubscriptionPlanModel? _selectedPlan;
    private PaymentGatewayDescriptor _selectedGateway = PaymentGatewayOrchestrator.AllGateways[0];
    private PaymentCheckoutSession? _activeSession;
    private string _statusMessage = "برای ارتقای حساب، یکی از ۴۶ مدل اشتراک و یکی از ۶ درگاه پرداخت (ایرانی یا بین‌المللی) را انتخاب کنید.";

    public UserBillingPortalViewModel()
    {
        RefreshPlans();
        _selectedPlan = VisiblePlans.FirstOrDefault(p => p.PlanNumber == 6) ?? VisiblePlans.FirstOrDefault();
    }

    public ObservableCollection<SubscriptionPlanModel> VisiblePlans { get; } = [];
    public ObservableCollection<PaymentReceiptAndLicense> PurchaseHistory { get; } = [];

    public IReadOnlyList<PaymentGatewayDescriptor> AllGateways => PaymentGatewayOrchestrator.AllGateways;
    public LicenseEntitlements CurrentEntitlements => _licenseManager.CurrentEntitlements;

    public string UserFullName
    {
        get => _userFullName;
        set => SetProperty(ref _userFullName, value);
    }

    public string UserEmail
    {
        get => _userEmail;
        set => SetProperty(ref _userEmail, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshPlans();
            }
        }
    }

    public SubscriptionCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                RefreshPlans();
            }
        }
    }

    public SubscriptionPlanModel? SelectedPlan
    {
        get => _selectedPlan;
        set => SetProperty(ref _selectedPlan, value);
    }

    public PaymentGatewayDescriptor SelectedGateway
    {
        get => _selectedGateway;
        set => SetProperty(ref _selectedGateway, value);
    }

    public PaymentCheckoutSession? ActiveSession
    {
        get => _activeSession;
        private set => SetProperty(ref _activeSession, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void RefreshPlans()
    {
        var list = SubscriptionCatalog46.FilterPlans(SelectedCategory, SearchQuery);
        VisiblePlans.Clear();
        foreach (var item in list)
        {
            VisiblePlans.Add(item);
        }

        if (SelectedPlan is null || !VisiblePlans.Contains(SelectedPlan))
        {
            SelectedPlan = VisiblePlans.FirstOrDefault();
        }
    }

    public PaymentCheckoutSession StartGatewayCheckout()
    {
        if (SelectedPlan is null)
        {
            throw new InvalidOperationException("ابتدا یکی از ۴۶ طرح اشتراک را انتخاب کنید.");
        }

        var req = new PaymentCheckoutRequest(
            PlanSkuId: SelectedPlan.SkuId,
            GatewayId: SelectedGateway.Id,
            CustomerEmail: UserEmail,
            CustomerFullName: UserFullName);

        ActiveSession = _gatewayOrchestrator.CreateCheckoutSession(req);
        StatusMessage = $"اتصال به «{SelectedGateway.DisplayNameFa}» برقرار شد | کد رهگیری: {ActiveSession.AuthorityOrTrackId} | لینک درگاه آماده است.";
        return ActiveSession;
    }

    public PaymentReceiptAndLicense CompleteAndVerifyPayment()
    {
        if (ActiveSession is null)
        {
            StartGatewayCheckout();
        }

        var receipt = _gatewayOrchestrator.VerifyPaymentAndIssueLicense(ActiveSession!.SessionId, UserEmail);
        _licenseManager.TryActivateLicenseKey(receipt.IssuedLicenseKey);

        PurchaseHistory.Insert(0, receipt);
        RaisePropertyChanged(nameof(CurrentEntitlements));
        StatusMessage = $"پرداخت تایید شد! فاکتور {receipt.ReceiptNumber} صادر گردید و کلید لایسنس ({receipt.IssuedLicenseKey}) روی حساب شما فعال شد.";
        return receipt;
    }
}
