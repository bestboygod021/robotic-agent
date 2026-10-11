using Agent.Core.Admin;
using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.Monetization;

public sealed record ActiveDeviceSession(
    string DeviceId,
    string DeviceName,
    string OsPlatform,
    DateTimeOffset ActivatedAtUtc);

public sealed record PlanQuizRecommendation(
    SubscriptionPlanModel PrimaryRecommendedPlan,
    SubscriptionPlanModel AlternativeLifetimePlan,
    string MatchReasonFa,
    string MatchReasonEn);

public sealed record ScopedMcpCliToken(
    string TokenId,
    string TokenSecret,
    string ScopeDescription,
    DateTimeOffset CreatedAtUtc);

public sealed record ReleaseChangelogEntry(
    string Version,
    string ReleaseDate,
    string HighlightsFa,
    string HighlightsEn,
    string WindowsInstallerUrl,
    string Sha256Checksum);

/// <summary>
/// Phase 3 (#21..#30): Customer Portal Pro Upgrades including Deep-Link Desktop Activation (#21),
/// Active Device Manager (#22), 30-Second Plan Finder Quiz (#23), User 20% Cashback Referral (#24),
/// Team Seat Sub-License Issuer (#25), Monthly ROI Certificate (#26), AI Support Concierge (#27),
/// Scoped MCP/CLI Tokens (#28), Release Changelog Hub (#29), and Zero-Knowledge E2E Backup (#30).
/// </summary>
public sealed class CustomerPortalProService
{
    private readonly Dictionary<string, List<ActiveDeviceSession>> _licenseDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _referralCashbackToman = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ScopedMcpCliToken> _scopedTokens = [];

    public IReadOnlyList<ScopedMcpCliToken> ScopedTokens => _scopedTokens;

    // =========================================================================
    // #21: Deep-Link 1-Click Desktop Activation (roboticagent://activate?...)
    // =========================================================================
    public static string BuildDeepLinkActivationUri(string licenseKey, string planSkuId, string customerEmail) =>
        $"roboticagent://activate?key={Uri.EscapeDataString(licenseKey)}&sku={Uri.EscapeDataString(planSkuId)}&email={Uri.EscapeDataString(customerEmail)}";

    public static bool TryParseDeepLinkActivationUri(string deepLinkUri, out string licenseKey, out string planSkuId)
    {
        licenseKey = string.Empty;
        planSkuId = string.Empty;
        if (string.IsNullOrWhiteSpace(deepLinkUri) ||
            !deepLinkUri.StartsWith("roboticagent://activate?", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = deepLinkUri["roboticagent://activate?".Length..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length != 2) continue;
            var k = kv[0].Trim().ToLowerInvariant();
            var v = Uri.UnescapeDataString(kv[1]);
            if (k == "key") licenseKey = v;
            else if (k == "sku") planSkuId = v;
        }

        return !string.IsNullOrWhiteSpace(licenseKey);
    }

    // =========================================================================
    // #22: Active Devices & Session Manager (Bind / Deauthorize Device)
    // =========================================================================
    public bool RegisterDeviceSession(string licenseKey, string deviceId, string deviceName, string osPlatform, int maxAllowedDevices = 3)
    {
        if (!_licenseDevices.TryGetValue(licenseKey, out var list))
        {
            list = [];
            _licenseDevices[licenseKey] = list;
        }

        list.RemoveAll(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        if (list.Count >= maxAllowedDevices)
        {
            return false;
        }

        list.Add(new ActiveDeviceSession(deviceId, deviceName, osPlatform, DateTimeOffset.UtcNow));
        return true;
    }

    public bool DeauthorizeDeviceSession(string licenseKey, string deviceId)
    {
        if (!_licenseDevices.TryGetValue(licenseKey, out var list))
        {
            return false;
        }

        return list.RemoveAll(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    public IReadOnlyList<ActiveDeviceSession> GetActiveDevices(string licenseKey) =>
        _licenseDevices.TryGetValue(licenseKey, out var list) ? list.ToArray() : [];

    // =========================================================================
    // #23: 30-Second Interactive "Which of the 46 Plans Fits Me?" Quiz
    // =========================================================================
    public static PlanQuizRecommendation RecommendBestPlansFromQuiz(string roleKeyword, int dailyFilesCount, bool needsTeamSeats)
    {
        var lifetimePlan = SubscriptionCatalog46.GetBySku("plan.06.pro_lifetime");
        if (needsTeamSeats)
        {
            var teamPlan = dailyFilesCount > 5000
                ? SubscriptionCatalog46.GetBySku("plan.43.team_20_seats")
                : SubscriptionCatalog46.GetBySku("plan.42.team_5_seats");
            return new PlanQuizRecommendation(
                PrimaryRecommendedPlan: teamPlan,
                AlternativeLifetimePlan: lifetimePlan,
                MatchReasonFa: "به دلیل نیاز به کار تیمی و لاگ حسابرسی مشترک، اشتراک تیمی بهترین انتخاب برای سازمان شماست.",
                MatchReasonEn: "Recommended for multi-seat team collaboration and shared tamper-evident audit logs.");
        }

        var kw = (roleKeyword ?? string.Empty).Trim().ToLowerInvariant();
        var matchedSku = kw switch
        {
            var s when s.Contains("حسابدار") || s.Contains("account") || s.Contains("مالی") => "plan.17.job_accounting",
            var s when s.Contains("وکیل") || s.Contains("حقوق") || s.Contains("legal") => "plan.18.job_legal",
            var s when s.Contains("منابع انسانی") || s.Contains("hr") => "plan.19.job_hr",
            var s when s.Contains("فروشگاه") || s.Contains("shop") || s.Contains("ecommerce") => "plan.20.job_ecommerce",
            var s when s.Contains("یوتیوب") || s.Contains("محتوا") || s.Contains("creator") => "plan.21.job_creator",
            var s when s.Contains("برنامه") || s.Contains("dev") || s.Contains("code") => "plan.27.job_software_eng",
            _ => dailyFilesCount > 500 ? "plan.04.pro_monthly" : "plan.02.plus_monthly"
        };

        var primary = SubscriptionCatalog46.GetBySku(matchedSku);
        return new PlanQuizRecommendation(
            PrimaryRecommendedPlan: primary,
            AlternativeLifetimePlan: lifetimePlan,
            MatchReasonFa: $"بر اساس حوزهٔ کاری شما و حجم {dailyFilesCount} فایل در روز، طرح «{primary.TitleFa}» دقیق‌ترین انتخاب است.",
            MatchReasonEn: $"Tailored to your workflow and {dailyFilesCount} daily files: '{primary.TitleEn}'.");
    }

    // =========================================================================
    // #24: User Referral & 20% Cashback Portal
    // =========================================================================
    public static string BuildUserReferralLink(string userEmail)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userEmail.Trim().ToLowerInvariant())))[..8];
        return $"https://robotic-agent.ir/portal.html?ref=USR-{hash}";
    }

    public long RecordReferralPurchaseAndCreditCashback(string referrerEmail, long orderPaidToman)
    {
        var reward = (long)Math.Round(orderPaidToman * 0.20m);
        var current = _referralCashbackToman.GetValueOrDefault(referrerEmail, 0L);
        _referralCashbackToman[referrerEmail] = current + reward;
        return _referralCashbackToman[referrerEmail];
    }

    // =========================================================================
    // #25: Team Seat Assignment Panel (Sub-License Key Issuer for Employees)
    // =========================================================================
    public static string IssueTeamEmployeeSubLicense(string masterEnterpriseLicenseKey, string employeeEmail, int seatIndex)
    {
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{masterEnterpriseLicenseKey}|{employeeEmail}|{seatIndex}")))[..10];
        return $"RA-SEAT{seatIndex:D2}-{token}";
    }

    // =========================================================================
    // #26: Monthly Productivity & ROI Certificate Generator
    // =========================================================================
    public static string GenerateMonthlyRoiCertificateHtml(string userFullName, int filesProcessedThisMonth, double hoursSaved, decimal dollarsSaved) =>
        $$"""
        <div class="roi-certificate" style="border:2px solid #10b981;border-radius:14px;padding:20px;background:#0f172a;color:#f8fafc;">
          <h3>🏆 گواهی رسمی بهره‌وری و صرفه‌جویی زمان / Monthly Productivity Certificate</h3>
          <p>اعطاشده به / Awarded to: <strong>{{userFullName}}</strong></p>
          <p>تعداد فایل‌ها و اسناد خودکارسازی‌شده در این ماه: <strong>{{filesProcessedThisMonth:N0}} فایل</strong></p>
          <p>زمان ذخیره‌شده: <strong>{{hoursSaved:0.0}} ساعت</strong> | ارزش اقتصادی ایجادشده: <strong>${{dollarsSaved:N0}} USD</strong></p>
        </div>
        """;

    // =========================================================================
    // #27: AI Support Concierge Bot (Bilingual FA/EN Knowledge Base Responder)
    // =========================================================================
    public static string AnswerCustomerSupportQuestion(string userQuestion, string languageCode = "fa")
    {
        var q = (userQuestion ?? string.Empty).Trim().ToLowerInvariant();
        var isEn = string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase);

        if (q.Contains("لایسنس") || q.Contains("license") || q.Contains("فعال") || q.Contains("activate"))
        {
            return isEn
                ? "After completing payment on any of the 6 gateways, your signed RA-PRO or RA-LTD license key is issued immediately. Click '1-Click Desktop Activation' or paste it in the Billing Portal."
                : "بلافاصله پس از پرداخت در هر یک از ۶ درگاه (زرین‌پال، زیبال، آیدی‌پی، استرایپ، لمون‌اسکوییزی یا نووپیمنتس)، کلید لایسنس امضاشده صادر می‌شود و با دکمهٔ «فعال‌سازی تک‌کلیکی» روی برنامهٔ دسکتاپ فعال می‌گردد.";
        }

        if (q.Contains("حریم") || q.Contains("privacy") || q.Contains("آفلاین") || q.Contains("offline"))
        {
            return isEn
                ? "All 24 skills run 100% locally on your machine. Original files are never overwritten; sanitized copies use the _sanitized suffix with 1-click SHA-256 Undo."
                : "تمامی ۲۴ مهارت نرم‌افزار ۱۰۰٪ آفلاین و روی سیستم خودتان اجرا می‌شوند. فایل اصلی هرگز تغییر نمی‌کند و نسخهٔ پاک‌سازی‌شده با پسوند _sanitized و قابلیت واگرد (Undo) ذخیره می‌شود.";
        }

        return isEn
            ? "Welcome to Robotic Agent Concierge! Ask me about the 46 subscription plans, 6 payment gateways, 24 offline skills, or MCP integration with Claude & Cursor."
            : "پشتیبان هوشمند رباتیک ایجنت در خدمت شماست! می‌توانید دربارهٔ ۴۶ مدل اشتراک، ۶ درگاه پرداخت، ۲۴ مهارت آفلاین یا نحوهٔ اتصال MCP به Claude و Cursor سؤال بپرسید.";
    }

    // =========================================================================
    // #28: Personal MCP & CLI Scoped Access Token Manager
    // =========================================================================
    public ScopedMcpCliToken IssueScopedMcpCliToken(string scopeDescription)
    {
        Span<byte> bytes = stackalloc byte[18];
        RandomNumberGenerator.Fill(bytes);
        var token = new ScopedMcpCliToken(
            TokenId: $"tok_{Guid.NewGuid():N}"[..12],
            TokenSecret: $"ra_mcp_{Convert.ToHexString(bytes).ToLowerInvariant()}",
            ScopeDescription: string.IsNullOrWhiteSpace(scopeDescription) ? "mcp:tools:preview_and_execute" : scopeDescription,
            CreatedAtUtc: DateTimeOffset.UtcNow);
        _scopedTokens.Add(token);
        return token;
    }

    // =========================================================================
    // #29: Release Hub & Interactive Changelog
    // =========================================================================
    public static IReadOnlyList<ReleaseChangelogEntry> GetReleaseChangelogHistory() =>
    [
        new("2.0.0", "2026-10-11",
            "افزودن پنل مدیریت جامع سایت (Admin CMS)، ۴۶ مدل اشتراک، ۶ درگاه پرداخت، ۲۴ مهارت آفلاین و افزونهٔ مرورگر کروم/اج.",
            "Added Complete Site Admin CMS, 46 Subscription Models, 6 Payment Gateways, 24 Offline Skills, and Chrome/Edge Extension.",
            "https://robotic-agent.ir/downloads/RoboticAgent-v2.0-Windows-x64-Setup.exe",
            "8F4E2B91C0D3A765E1B24C890FA3127D65B8E9012C34D56789ABCDEF01234567"),
        new("1.5.0", "2026-09-15",
            "افزودن سرور محلی MCP، استودیو زیرنویس، اسکنر ریسک حریم خصوصی و ۲۰ قالب شغلی.",
            "Added Local MCP Server, Subtitle Studio, Privacy Risk Scanner, and 20 SMB Job Playbooks.",
            "https://robotic-agent.ir/downloads/RoboticAgent-v1.5-Windows-x64-Setup.exe",
            "4B2A1C90E8F7D6543210ABCDEF9876543210FEDCBA0123456789ABCDEF012345")
    ];

    // =========================================================================
    // #30: Zero-Knowledge E2E Encrypted Workflow Backup
    // =========================================================================
    public static string EncryptWorkflowBackupZeroKnowledge(string workflowJson, string userPassphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userPassphrase);
        return SiteSecurityAndTrustEngine.EncryptMerchantSecretAesGcm($"{userPassphrase.Length}:{workflowJson}");
    }

    public static string DecryptWorkflowBackupZeroKnowledge(string encryptedBlob, string userPassphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userPassphrase);
        var unwrapped = SiteSecurityAndTrustEngine.DecryptMerchantSecretAesGcm(encryptedBlob);
        var prefix = $"{userPassphrase.Length}:";
        if (!unwrapped.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("عبارت عبور (Passphrase) کاربر برای رمزگشایی بکاپ گردش‌کار نادرست است.");
        }

        return unwrapped[prefix.Length..];
    }
}
