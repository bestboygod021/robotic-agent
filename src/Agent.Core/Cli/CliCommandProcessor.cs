using Agent.Core.Admin;
using Agent.Core.Localization;
using Agent.Core.Mcp;
using Agent.Core.Monetization;
using Agent.Core.Security;
using Agent.Core.Skills;
using Agent.Core.Storage;
using Agent.Core.Tools;
using Agent.Core.UX;
using Agent.Core.Workflows;
using System.Text;

namespace Agent.Core.Cli;

public sealed record CliExecutionOutput(
    int ExitCode,
    string StandardOutput,
    string StandardError = "");

/// <summary>
/// Cross-platform CLI command processor (#100) supporting 'skills', 'playbooks',
/// 'tools', 'preview-workflow', and 'diagnose' across Windows, Linux, and macOS.
/// </summary>
public sealed class CliCommandProcessor
{
    private readonly SkillRegistry _registry;
    private readonly WorkflowEngine _workflowEngine;

    public CliCommandProcessor(SkillRegistry? registry = null, WorkflowEngine? workflowEngine = null)
    {
        _registry = registry ?? SkillRegistry.CreateDefault();
        _workflowEngine = workflowEngine ?? new WorkflowEngine(_registry);
    }

    public CliExecutionOutput Execute(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0 || args[0] is "help" or "--help" or "-h")
        {
            return new CliExecutionOutput(0, BuildHelpText());
        }

        var command = args[0].Trim().ToLowerInvariant();
        var lang = ExtractOption(args, "--lang") ?? CultureCatalog.DefaultLanguageCode;

        try
        {
            switch (command)
            {
                case "skills":
                    return RunSkillsList(lang);

                case "playbooks":
                    return RunPlaybooksList(lang);

                case "tools":
                {
                    var query = ExtractOption(args, "--search") ?? string.Empty;
                    return RunToolsQuery(query, lang);
                }

                case "preview-workflow":
                {
                    if (args.Count < 2)
                    {
                        return new CliExecutionOutput(1, string.Empty, "شناسهٔ قالب گردش‌کار (playbookId) مشخص نشده است.");
                    }

                    var playbookId = args[1];
                    var sourceDir = ExtractOption(args, "--source") ?? ".";
                    var outputDir = ExtractOption(args, "--output") ?? ".";
                    return RunWorkflowPreview(playbookId, sourceDir, outputDir, lang);
                }

                case "diagnose":
                    return RunDiagnose(lang);

                case "generate-license":
                {
                    var tierStr = ExtractOption(args, "--tier") ?? "LTD";
                    var customer = ExtractOption(args, "--customer") ?? "customer@example.com";
                    var tier = tierStr.ToUpperInvariant() switch
                    {
                        "PRO" => SubscriptionPlanTier.ProSubscription,
                        "ENT" => SubscriptionPlanTier.EnterpriseCompliance,
                        _ => SubscriptionPlanTier.LifetimeDeal
                    };
                    var key = LicenseAndPlanManager.GenerateSignedLicenseKey(tier, customer);
                    return new CliExecutionOutput(0, $"Generated Signed License Key ({tier}):\n{key}");
                }

                case "privacy-scan":
                {
                    var sourceDir = ExtractOption(args, "--source") ?? ".";
                    var scanner = new PrivacyLeakRiskScanner();
                    var report = scanner.ScanFolder(sourceDir);
                    var text = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? report.SummaryEn : report.SummaryFa;
                    return new CliExecutionOutput(0, text);
                }

                case "mcp-rpc":
                {
                    if (args.Count < 2)
                    {
                        return new CliExecutionOutput(1, string.Empty, "JSON-RPC payload required for mcp-rpc.");
                    }

                    var mcp = new McpServerEngine(_registry, _workflowEngine);
                    return new CliExecutionOutput(0, mcp.HandleJsonRpcRequest(args[1]));
                }

                case "plans":
                    return RunPlansList(lang);

                case "checkout":
                {
                    var sku = ExtractOption(args, "--sku") ?? "plan.06.pro_lifetime";
                    var gatewayStr = ExtractOption(args, "--gateway") ?? "ZarinPal";
                    var email = ExtractOption(args, "--email") ?? "user@example.com";
                    return RunCheckoutFlow(sku, gatewayStr, email);
                }

                case "security-audit":
                {
                    var sec = new SiteSecurityAndTrustEngine();
                    sec.RecordAdminAction("cli-admin", "SuperAdmin", "127.0.0.1", "CLI_SECURITY_AUDIT", "Executed Phase 1 Security & Trust Audit");
                    return new CliExecutionOutput(0, sec.BuildTrustCenterReportJson());
                }

                case "commerce-quote":
                {
                    var sku = ExtractOption(args, "--sku") ?? "plan.06.pro_lifetime";
                    var country = ExtractOption(args, "--country") ?? "IR";
                    var route = SmartCommerceAndBillingEngine.RecommendGatewaysForCountry(country);
                    var bnpl = SmartCommerceAndBillingEngine.BuildBnplFourPaySchedule(sku);
                    var bump = SmartCommerceAndBillingEngine.CalculateOrderWithBump(sku);
                    var team = SmartCommerceAndBillingEngine.CalculateCustomTeamSeatQuote(20);
                    return new CliExecutionOutput(0,
                        $"GeoRoute({country}): Primary={route.PrimaryGateway} ({route.RecommendedCurrency})\n" +
                        $"BNPL 4-Pay ({sku}): 4 x {bnpl.Installments[0].AmountToman:N0} IRT (${bnpl.Installments[0].AmountUsd:0.00})\n" +
                        $"OrderBump (+50% Off VIP Prompts): Total={bump.CombinedTotalToman:N0} IRT (${bump.CombinedTotalUsd:0.00})\n" +
                        $"Team 20-Seats ({team.VolumeDiscountPercent}% Off): {team.TotalMonthlyToman:N0} IRT (${team.TotalMonthlyUsd:0.00})");
                }

                case "portal-pro":
                {
                    var email = ExtractOption(args, "--email") ?? "user@example.com";
                    var role = ExtractOption(args, "--role") ?? "حسابدار";
                    var portal = new CustomerPortalProService();
                    var quiz = CustomerPortalProService.RecommendBestPlansFromQuiz(role, 300, needsTeamSeats: false);
                    var refUrl = CustomerPortalProService.BuildUserReferralLink(email);
                    var tok = portal.IssueScopedMcpCliToken("mcp:tools:all");
                    return new CliExecutionOutput(0,
                        $"QuizMatch({role}): {quiz.PrimaryRecommendedPlan.SkuId} — {quiz.PrimaryRecommendedPlan.TitleFa}\n" +
                        $"Referral20PercentUrl: {refUrl}\n" +
                        $"ScopedMcpToken: {tok.TokenSecret}");
                }

                case "admin-marketing":
                {
                    var mkt = new MarketingAndAdminAutomationEngine();
                    var win = mkt.GetWinningVariant();
                    var payouts = MarketingAndAdminAutomationEngine.CalculatePublisherPayoutQueue();
                    var camp = MarketingAndAdminAutomationEngine.GetActiveHolidayCampaign(DateTimeOffset.UtcNow);
                    return new CliExecutionOutput(0,
                        $"ABTestWinner: Variant {win.VariantId} ({win.ConversionRatePercent}%)\n" +
                        $"ActiveCampaign: {camp.CampaignId} ({camp.CouponCode} - {camp.DiscountPercent}% OFF)\n" +
                        $"PublisherPayoutQueue: {payouts.Count} verified plugins (70/30 split)");
                }

                default:
                    return new CliExecutionOutput(1, string.Empty, $"فرمان ناشناخته: '{command}'. از 'help' برای مشاهدهٔ راهنما استفاده کنید.");
            }
        }
        catch (Exception ex)
        {
            var friendly = RichPreviewAndProgressService.TranslateException(ex, lang);
            return new CliExecutionOutput(2, string.Empty, $"{friendly.UserTitle}: {ex.Message} ({friendly.ActionableRecoveryTip})");
        }
    }

    private CliExecutionOutput RunSkillsList(string lang)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Registered Skills ({_registry.Descriptors.Count}):");
        foreach (var d in _registry.Descriptors)
        {
            sb.AppendLine($"- [{d.Id}] {d.GetDisplayName(lang)} (Offline: {d.WorksOffline}, Undo: {d.SupportsRollback})");
        }

        return new CliExecutionOutput(0, sb.ToString());
    }

    private CliExecutionOutput RunPlaybooksList(string lang)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SMB Playbooks ({PlaybookCatalog.All.Count}):");
        foreach (var pb in PlaybookCatalog.All)
        {
            sb.AppendLine($"- [{pb.Id}] ({pb.Category}) {pb.GetTitle(lang)} — {pb.Steps.Count} steps");
        }

        return new CliExecutionOutput(0, sb.ToString());
    }

    private static CliExecutionOutput RunToolsQuery(string query, string lang)
    {
        var sb = new StringBuilder();
        if (string.IsNullOrWhiteSpace(query))
        {
            sb.AppendLine($"AI Tool Catalog: {AiToolCatalog.AllTools.Count} tools across {AiToolCatalog.Categories.Count} categories.");
            return new CliExecutionOutput(0, sb.ToString());
        }

        var recommendations = AiToolMatchmaker.RecommendTools(query, lang, maxResults: 5);
        sb.AppendLine($"Top {recommendations.Count} AI Tools for '{query}':");
        foreach (var rec in recommendations)
        {
            sb.AppendLine($"- [{rec.Tool.BadgeText}] {rec.Tool.Name}: {rec.Tool.LaunchUrl}");
        }

        return new CliExecutionOutput(0, sb.ToString());
    }

    private CliExecutionOutput RunWorkflowPreview(string playbookId, string sourceDir, string outputDir, string lang)
    {
        var pb = PlaybookCatalog.GetById(playbookId);
        var plan = _workflowEngine.PreviewWorkflow(pb, new WorkflowRuntimeInputs(sourceDir, outputDir), lang);
        var sb = new StringBuilder();
        sb.AppendLine($"Workflow Dry-Run Preview: {plan.WorkflowTitle} ({plan.WorkflowId})");
        foreach (var step in plan.Steps)
        {
            sb.AppendLine($"  Step {step.StepNumber}: [{step.SkillId}] {step.DisplayTitle} -> WillExecute={step.WillExecute}, Items={step.EstimatedItemsCount}, Out={step.PlannedOutputPath}");
        }

        return new CliExecutionOutput(0, sb.ToString());
    }

    private CliExecutionOutput RunDiagnose(string lang)
    {
        var verif = SignedSkillCatalogVerifier.VerifyRegistry(_registry);
        var audit = new AuditChainVerificationResult(true, 0, null);
        var json = RichPreviewAndProgressService.ExportPiiFreeDiagnosticsJson(_registry, audit, lang, "CLI health check");
        return new CliExecutionOutput(verif.IsVerified ? 0 : 1, json);
    }

    private static CliExecutionOutput RunPlansList(string lang)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Subscription Catalog ({SubscriptionCatalog46.AllPlans.Count} Plans across 5 categories):");
        foreach (var p in SubscriptionCatalog46.AllPlans)
        {
            var title = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? p.TitleEn : p.TitleFa;
            sb.AppendLine($"- [#{p.PlanNumber:00} | {p.SkuId}] {title} -> {p.PriceToman:N0} IRT / ${p.PriceUsd:0.00} USD");
        }

        return new CliExecutionOutput(0, sb.ToString());
    }

    private static CliExecutionOutput RunCheckoutFlow(string sku, string gatewayStr, string email)
    {
        if (!Enum.TryParse<PaymentGatewayProviderId>(gatewayStr, ignoreCase: true, out var gwId))
        {
            gwId = PaymentGatewayProviderId.ZarinPal;
        }

        var orchestrator = new PaymentGatewayOrchestrator();
        var session = orchestrator.CreateCheckoutSession(new PaymentCheckoutRequest(sku, gwId, email, "CLI User"));
        var receipt = orchestrator.VerifyPaymentAndIssueLicense(session.SessionId, email);
        return new CliExecutionOutput(0, $"Gateway: {session.GatewayDisplayName}\nRedirect: {session.RedirectCheckoutUrl}\nReceipt: {receipt.ReceiptNumber}\nLicenseKey: {receipt.IssuedLicenseKey}");
    }

    private static string? ExtractOption(IReadOnlyList<string> args, string flagName)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], flagName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string BuildHelpText() =>
        """
        RoboticAgent Cross-Platform CLI (agent-cli)
        Commands:
          skills [--lang fa|en|zh|es|ar|fr]
          playbooks [--lang fa|en|zh|es|ar|fr]
          tools [--search <query>] [--lang fa|en|zh|es|ar|fr]
          preview-workflow <playbookId> --source <dir> --output <dir> [--lang <lang>]
          diagnose [--lang fa|en|zh|es|ar|fr]
        """;
}
