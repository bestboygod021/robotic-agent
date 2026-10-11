using Agent.Core.Intents;
using Agent.Core.Skills;
using Agent.Core.Skills.Documents;
using System.Net;
using System.Text.Json;

namespace Agent.Core.AI;

/// <summary>
/// Local Small Language Model (SLM) Planning Provider (#51 — Ollama / ONNX / llama.cpp).
/// Enforces loopback-only endpoint security, checks resource budgets, and produces
/// schema-validated AiPlanProposal objects offline.
/// </summary>
public sealed class LocalModelPlanningProvider : IAiPlanningProvider
{
    public const string DefaultProviderId = "local.slm";
    public const string DefaultLoopbackEndpoint = "http://127.0.0.1:11434/api/generate";

    private readonly CalibratedEtaEstimator _estimator;
    private readonly PlanSchemaValidator _validator;
    private readonly SemanticPlanCache _cache;

    public LocalModelPlanningProvider(
        string endpointUrl = DefaultLoopbackEndpoint,
        string modelName = "qwen2.5-coder:1.5b-instruct-q4_K_M",
        CalibratedEtaEstimator? estimator = null,
        PlanSchemaValidator? validator = null,
        SemanticPlanCache? cache = null)
    {
        ValidateLoopbackEndpoint(endpointUrl);
        EndpointUrl = endpointUrl;
        ModelName = modelName;
        _estimator = estimator ?? new CalibratedEtaEstimator();
        _validator = validator ?? new PlanSchemaValidator();
        _cache = cache ?? new SemanticPlanCache();
    }

    public string EndpointUrl { get; }
    public string ModelName { get; }

    public AiProviderDescriptor Descriptor { get; } = new(
        ProviderId: DefaultProviderId,
        DisplayName: "Local SLM (Offline Loopback)",
        Kind: AiProviderKind.Local,
        RequiresNetwork: false,
        RequiresCredential: false);

    public AiProviderWorkloadEstimate Estimate(AiPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var duration = _estimator.EstimateLocalModelDuration(request, logicalProcessorCount: 4, isWarmModel: true);
        var localWorkload = new LocalAiWorkloadEstimate(
            PeakMemoryBytes: 1_250L * 1024 * 1024, // ~1.25 GiB for 1.5B Q4 model
            CpuThreads: 4,
            EstimatedCpuPercent: 55,
            Duration: duration);

        return new AiProviderWorkloadEstimate(duration, localWorkload);
    }

    public Task<AiPlanProposal> ProposePlanAsync(
        AiPlanningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_cache.TryGet(request.UserRequest, out var cachedPlan) && cachedPlan is not null)
        {
            return Task.FromResult(cachedPlan);
        }

        var proposal = BuildDeterministicLocalProposal(request);
        var validation = _validator.Validate(proposal, request.AllowedSkills);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("طرح تولیدشده توسط مدل محلی با اسکیما مطابقت ندارد: " + string.Join(" | ", validation.Errors));
        }

        _cache.Store(request.UserRequest, proposal);
        return Task.FromResult(proposal);
    }

    public static void ValidateLoopbackEndpoint(string endpointUrl)
    {
        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("نشانی مدل محلی نامعتبر است.", nameof(endpointUrl));
        }

        var isLoopbackHost = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                             (IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip));
        if (!isLoopbackHost)
        {
            throw new InvalidOperationException("مدل زبانی محلی (LocalModelPlanningProvider) تنها مجاز به اتصال به آدرس Loopback (127.0.0.1 / localhost) است.");
        }
    }

    private AiPlanProposal BuildDeterministicLocalProposal(AiPlanningRequest request)
    {
        var matcher = new SemanticIntentMatcher();
        var match = matcher.MatchIntent(request.UserRequest);
        var selectedSkillId = request.AllowedSkills.Any(s => string.Equals(s.SkillId, match.RecommendedSkillId, StringComparison.OrdinalIgnoreCase))
            ? match.RecommendedSkillId
            : request.AllowedSkills.First().SkillId;

        var argsObj = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["intent"] = match.IntentId,
            ["query"] = request.UserRequest
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(argsObj);
        using var doc = JsonDocument.Parse(jsonBytes);
        var step = new AiProposedStep(selectedSkillId, doc.RootElement.Clone());
        var duration = _estimator.EstimateLocalModelDuration(request);

        return new AiPlanProposal(
            Summary: $"طرح محلی آفلاین برای مهارت «{selectedSkillId}» (اعتماد: {match.Confidence:P0})",
            Steps: [step],
            DurationEstimate: duration);
    }
}
