using System.Text.Json;

namespace Agent.Core.AI;

public enum AiExecutionMode
{
    LocalOnly,
    Hybrid
}

public enum AiProviderKind
{
    Local,
    External
}

public enum AiTaskComplexity
{
    Lightweight,
    Heavy,
    VeryHeavy
}

public enum AiRouteAction
{
    RunLocal,
    RunExternal,
    AskExternalConsent,
    CannotRun
}

public enum LocalBudgetFailure
{
    None,
    LocalModelUnavailable,
    InvalidResourceSnapshot,
    InvalidBudget,
    InvalidWorkloadEstimate,
    ModelMemoryCapExceeded,
    InsufficientFreeMemory,
    CpuThreadLimitExceeded,
    CpuUsageLimitExceeded,
    SystemCpuBusy,
    DurationLimitExceeded
}

/// <summary>
/// A deliberately approximate range. Providers should base this on measured
/// performance where possible and report lower confidence for cold starts.
/// </summary>
public sealed record TaskDurationEstimate(
    TimeSpan BestCase,
    TimeSpan Typical,
    TimeSpan WorstCase,
    double Confidence,
    string Basis)
{
    public bool IsValid =>
        BestCase >= TimeSpan.Zero &&
        Typical >= BestCase &&
        WorstCase >= Typical &&
        double.IsFinite(Confidence) && Confidence is >= 0 and <= 1 &&
        !string.IsNullOrWhiteSpace(Basis);
}

/// <summary>
/// A snapshot supplied by a platform-specific monitor immediately before local
/// inference. It is not a guarantee that the operating system will not change.
/// </summary>
public sealed record LocalSystemResources(
    long TotalPhysicalMemoryBytes,
    long AvailablePhysicalMemoryBytes,
    int LogicalProcessorCount,
    double CurrentCpuPercent)
{
    public bool IsValid =>
        TotalPhysicalMemoryBytes > 0 &&
        AvailablePhysicalMemoryBytes >= 0 &&
        AvailablePhysicalMemoryBytes <= TotalPhysicalMemoryBytes &&
        LogicalProcessorCount > 0 &&
        double.IsFinite(CurrentCpuPercent) && CurrentCpuPercent is >= 0 and <= 100;
}

/// <summary>
/// Configurable preflight limits for local model execution. Runtime adapters
/// must also enforce limits while a model is running.
/// </summary>
public sealed record LocalAiResourceBudget(
    long MaxModelMemoryBytes,
    long MinimumFreeMemoryReserveBytes,
    int MaxCpuThreads,
    int MaxInferenceCpuPercent,
    int MaximumSystemCpuPercentToStart,
    TimeSpan MaxTaskDuration)
{
    public bool IsValid =>
        MaxModelMemoryBytes > 0 &&
        MinimumFreeMemoryReserveBytes >= 0 &&
        MaxCpuThreads > 0 &&
        MaxInferenceCpuPercent is >= 1 and <= 100 &&
        MaximumSystemCpuPercentToStart is >= 1 and <= 100 &&
        MaxTaskDuration > TimeSpan.Zero;
}

/// <summary>
/// Estimates supplied by the selected local model/runtime before it is loaded.
/// Memory includes weights and expected inference workspace.
/// </summary>
public sealed record LocalAiWorkloadEstimate(
    long PeakMemoryBytes,
    int CpuThreads,
    int EstimatedCpuPercent,
    TaskDurationEstimate Duration)
{
    public bool IsValid =>
        PeakMemoryBytes > 0 &&
        CpuThreads > 0 &&
        EstimatedCpuPercent is >= 1 and <= 100 &&
        Duration.IsValid;
}

public sealed record AiRoutingRequest(
    AiExecutionMode Mode,
    AiTaskComplexity Complexity,
    LocalSystemResources? SystemResources,
    LocalAiResourceBudget? LocalBudget,
    LocalAiWorkloadEstimate? LocalEstimate,
    string? ExternalProviderId,
    bool ExternalProviderConfigured,
    TaskDurationEstimate? ExternalEstimate,
    bool ExternalApprovedForThisTask);

public sealed record AiRoutingDecision(
    AiRouteAction Action,
    AiProviderKind? ProviderKind,
    string? ProviderId,
    TaskDurationEstimate? DurationEstimate,
    bool RequiresExternalConsent,
    bool LocalFallbackAvailable,
    TaskDurationEstimate? LocalFallbackEstimate,
    LocalBudgetFailure LocalBudgetFailure);

public sealed record AiProviderDescriptor(
    string ProviderId,
    string DisplayName,
    AiProviderKind Kind,
    bool RequiresNetwork,
    bool RequiresCredential);

public sealed record AiProviderWorkloadEstimate(
    TaskDurationEstimate Duration,
    LocalAiWorkloadEstimate? LocalResources)
{
    public bool IsValid => Duration.IsValid;
}

/// <summary>
/// Only a proposed, typed plan may cross the provider boundary. Provider output
/// is advisory and must be validated against the local skill catalog before use.
/// </summary>
public sealed record AiAllowedSkill(
    string SkillId,
    string Description,
    string ArgumentsJsonSchema);

public sealed record AiPlanningRequest(
    string UserRequest,
    IReadOnlyList<AiAllowedSkill> AllowedSkills);

public sealed record AiProposedStep(
    string SkillId,
    JsonElement Arguments);

public sealed record AiPlanProposal(
    string Summary,
    IReadOnlyList<AiProposedStep> Steps,
    TaskDurationEstimate? DurationEstimate);

public interface IAiSystemResourceMonitor
{
    Task<LocalSystemResources> CaptureAsync(CancellationToken cancellationToken = default);
}

public interface IAiPlanningProvider
{
    AiProviderDescriptor Descriptor { get; }

    /// <summary>
    /// Estimates duration and local resource needs without contacting a remote provider.
    /// </summary>
    AiProviderWorkloadEstimate Estimate(AiPlanningRequest request);

    Task<AiPlanProposal> ProposePlanAsync(
        AiPlanningRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiProviderRegistry
{
    IReadOnlyList<AiProviderDescriptor> GetProviders();

    bool TryGetProvider(string providerId, out IAiPlanningProvider? provider);
}

/// <summary>
/// Implementations should use the OS credential vault (for example, Windows
/// Credential Manager or DPAPI-backed storage); secrets do not belong in settings,
/// logs, prompts, or source control.
/// </summary>
public interface IAiCredentialStore
{
    Task SaveAsync(string providerId, string secret, CancellationToken cancellationToken = default);

    Task<string?> ReadAsync(string providerId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string providerId, CancellationToken cancellationToken = default);
}
