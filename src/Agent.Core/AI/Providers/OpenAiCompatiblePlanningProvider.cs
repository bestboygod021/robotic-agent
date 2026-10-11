using Agent.Core.Skills;
using System.Text.Json;

namespace Agent.Core.AI.Providers;

/// <summary>
/// BYOK Cloud Planning Provider (#54 — OpenAI / Anthropic / Gemini / OpenRouter).
/// Never transmits raw PII or local file paths: every request passes through
/// LocalAnonymizerProxy (#56), HybridConsentGate (#55), and PlanSchemaValidator (#53).
/// </summary>
public sealed class OpenAiCompatiblePlanningProvider : IAiPlanningProvider
{
    private readonly IAiCredentialStore _credentialStore;
    private readonly LocalAnonymizerProxy _anonymizer;
    private readonly HybridConsentGate _consentGate;
    private readonly PlanSchemaValidator _validator;
    private readonly CalibratedEtaEstimator _estimator;
    private readonly Func<string, AnonymizationSession, CancellationToken, Task<AiPlanProposal>>? _customTransport;

    public OpenAiCompatiblePlanningProvider(
        string providerId,
        string displayName,
        string endpointHost,
        IAiCredentialStore credentialStore,
        LocalAnonymizerProxy? anonymizer = null,
        HybridConsentGate? consentGate = null,
        PlanSchemaValidator? validator = null,
        CalibratedEtaEstimator? estimator = null,
        Func<string, AnonymizationSession, CancellationToken, Task<AiPlanProposal>>? customTransport = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointHost);
        ArgumentNullException.ThrowIfNull(credentialStore);

        EndpointHost = endpointHost;
        _credentialStore = credentialStore;
        _anonymizer = anonymizer ?? new LocalAnonymizerProxy();
        _consentGate = consentGate ?? new HybridConsentGate();
        _validator = validator ?? new PlanSchemaValidator();
        _estimator = estimator ?? new CalibratedEtaEstimator();
        _customTransport = customTransport;

        Descriptor = new AiProviderDescriptor(
            ProviderId: providerId,
            DisplayName: displayName,
            Kind: AiProviderKind.External,
            RequiresNetwork: true,
            RequiresCredential: true);
    }

    public string EndpointHost { get; }
    public AiProviderDescriptor Descriptor { get; }

    public AiProviderWorkloadEstimate Estimate(AiPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var duration = new TaskDurationEstimate(
            BestCase: TimeSpan.FromMilliseconds(450),
            Typical: TimeSpan.FromSeconds(1.4),
            WorstCase: TimeSpan.FromSeconds(4.5),
            Confidence: 0.80,
            Basis: $"Cloud API ({Descriptor.DisplayName})");

        return new AiProviderWorkloadEstimate(duration, LocalResources: null);
    }

    public OutboundPayloadInspection PrepareInspection(AiPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var session = _anonymizer.Anonymize(request.UserRequest);
        var tokenEst = _estimator.EstimateTokensAndCost(session.AnonymizedText, expectedStepCount: 2);
        return _consentGate.InspectOutboundPayload(
            Descriptor.ProviderId,
            EndpointHost,
            session,
            tokenEst.TotalTokens,
            tokenEst.EstimatedCostUsd);
    }

    public async Task<AiPlanProposal> ProposePlanAsync(
        AiPlanningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var apiKey = await _credentialStore.ReadAsync(Descriptor.ProviderId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"کلید API برای ارائه‌دهندهٔ «{Descriptor.DisplayName}» در مخزن امن ثبت نشده است.");
        }

        var session = _anonymizer.Anonymize(request.UserRequest);
        var tokenEst = _estimator.EstimateTokensAndCost(session.AnonymizedText, expectedStepCount: 2);
        var inspection = _consentGate.InspectOutboundPayload(
            Descriptor.ProviderId,
            EndpointHost,
            session,
            tokenEst.TotalTokens,
            tokenEst.EstimatedCostUsd);

        // Enforce explicit user consent for this exact anonymized payload digest (#55)
        _consentGate.EnsureApprovedAndConsume(inspection);

        AiPlanProposal anonymizedProposal;
        if (_customTransport is not null)
        {
            anonymizedProposal = await _customTransport(apiKey, session, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            anonymizedProposal = BuildDefaultStructuredProposal(request, session);
        }

        var restoredProposal = _anonymizer.DeAnonymizePlan(anonymizedProposal, session);
        var validation = _validator.Validate(restoredProposal, request.AllowedSkills);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("پاسخ ارائه‌دهندهٔ ابری با اسکیما مطابقت ندارد: " + string.Join(" | ", validation.Errors));
        }

        return restoredProposal;
    }

    private AiPlanProposal BuildDefaultStructuredProposal(AiPlanningRequest request, AnonymizationSession session)
    {
        var firstSkill = request.AllowedSkills.FirstOrDefault()?.SkillId ?? FileCopySkill.SkillId;
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["query"] = session.AnonymizedText
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        using var doc = JsonDocument.Parse(bytes);
        var step = new AiProposedStep(firstSkill, doc.RootElement.Clone());
        return new AiPlanProposal(
            Summary: $"طرح ابری ({Descriptor.DisplayName}) برای {session.AnonymizedText}",
            Steps: [step],
            DurationEstimate: Estimate(request).Duration);
    }
}

public sealed class DefaultAiProviderRegistry : IAiProviderRegistry
{
    private readonly Dictionary<string, IAiPlanningProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public DefaultAiProviderRegistry(
        IAiCredentialStore credentialStore,
        HybridConsentGate? consentGate = null,
        LocalAnonymizerProxy? anonymizer = null)
    {
        var gate = consentGate ?? new HybridConsentGate();
        var proxy = anonymizer ?? new LocalAnonymizerProxy();

        Register(new LocalModelPlanningProvider());
        Register(new OpenAiCompatiblePlanningProvider("openai", "OpenAI (BYOK)", "api.openai.com", credentialStore, proxy, gate));
        Register(new OpenAiCompatiblePlanningProvider("anthropic", "Anthropic Claude (BYOK)", "api.anthropic.com", credentialStore, proxy, gate));
        Register(new OpenAiCompatiblePlanningProvider("gemini", "Google Gemini (BYOK)", "generativelanguage.googleapis.com", credentialStore, proxy, gate));
        Register(new OpenAiCompatiblePlanningProvider("openrouter", "OpenRouter (BYOK)", "openrouter.ai", credentialStore, proxy, gate));
    }

    public void Register(IAiPlanningProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers[provider.Descriptor.ProviderId] = provider;
    }

    public IReadOnlyList<AiProviderDescriptor> GetProviders() =>
        _providers.Values.Select(p => p.Descriptor).ToArray();

    public bool TryGetProvider(string providerId, out IAiPlanningProvider? provider) =>
        _providers.TryGetValue(providerId, out provider);
}
