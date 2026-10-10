namespace Agent.Core.AI;

/// <summary>
/// Chooses a planning backend without invoking it. Hybrid routing prefers local
/// inference for lightweight tasks and proposes external inference for heavy
/// tasks, but never sends a request until the user approves that specific task.
/// </summary>
public sealed class AiRoutingPolicy
{
    public AiRoutingDecision Decide(AiRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var localFailure = CheckLocalBudget(request);
        var localAvailable = localFailure == LocalBudgetFailure.None;
        var externalAvailable = request.ExternalProviderConfigured &&
            !string.IsNullOrWhiteSpace(request.ExternalProviderId);

        if (request.Mode == AiExecutionMode.LocalOnly)
        {
            return localAvailable
                ? UseLocal(request.LocalEstimate!.Duration)
                : CannotRun(localFailure);
        }

        if (request.Mode != AiExecutionMode.Hybrid)
        {
            return CannotRun(LocalBudgetFailure.InvalidBudget);
        }

        var shouldPreferExternal = request.Complexity is AiTaskComplexity.Heavy or AiTaskComplexity.VeryHeavy;
        if (shouldPreferExternal && externalAvailable)
        {
            return SelectExternal(request, localAvailable, localFailure);
        }

        if (localAvailable)
        {
            return UseLocal(request.LocalEstimate!.Duration);
        }

        if (externalAvailable)
        {
            return SelectExternal(request, localAvailable: false, localFailure: localFailure);
        }

        return CannotRun(localFailure);
    }

    private static LocalBudgetFailure CheckLocalBudget(AiRoutingRequest request)
    {
        if (request.LocalEstimate is null)
        {
            return LocalBudgetFailure.LocalModelUnavailable;
        }

        if (request.SystemResources is null || !request.SystemResources.IsValid)
        {
            return LocalBudgetFailure.InvalidResourceSnapshot;
        }

        if (request.LocalBudget is null || !request.LocalBudget.IsValid)
        {
            return LocalBudgetFailure.InvalidBudget;
        }

        if (!request.LocalEstimate.IsValid)
        {
            return LocalBudgetFailure.InvalidWorkloadEstimate;
        }

        if (request.LocalEstimate.PeakMemoryBytes > request.LocalBudget.MaxModelMemoryBytes)
        {
            return LocalBudgetFailure.ModelMemoryCapExceeded;
        }

        var usableMemory = request.SystemResources.AvailablePhysicalMemoryBytes -
            request.LocalBudget.MinimumFreeMemoryReserveBytes;
        if (usableMemory < 0 || request.LocalEstimate.PeakMemoryBytes > usableMemory)
        {
            return LocalBudgetFailure.InsufficientFreeMemory;
        }

        if (request.SystemResources.CurrentCpuPercent > request.LocalBudget.MaximumSystemCpuPercentToStart)
        {
            return LocalBudgetFailure.SystemCpuBusy;
        }

        if (request.LocalEstimate.CpuThreads > request.LocalBudget.MaxCpuThreads ||
            request.LocalEstimate.CpuThreads > request.SystemResources.LogicalProcessorCount)
        {
            return LocalBudgetFailure.CpuThreadLimitExceeded;
        }

        if (request.LocalEstimate.EstimatedCpuPercent > request.LocalBudget.MaxInferenceCpuPercent)
        {
            return LocalBudgetFailure.CpuUsageLimitExceeded;
        }

        if (request.LocalEstimate.Duration.WorstCase > request.LocalBudget.MaxTaskDuration)
        {
            return LocalBudgetFailure.DurationLimitExceeded;
        }

        return LocalBudgetFailure.None;
    }

    private static AiRoutingDecision SelectExternal(
        AiRoutingRequest request,
        bool localAvailable,
        LocalBudgetFailure localFailure)
    {
        if (request.ExternalApprovedForThisTask)
        {
            return new AiRoutingDecision(
                Action: AiRouteAction.RunExternal,
                ProviderKind: AiProviderKind.External,
                ProviderId: request.ExternalProviderId,
                DurationEstimate: request.ExternalEstimate,
                RequiresExternalConsent: false,
                LocalFallbackAvailable: localAvailable,
                LocalFallbackEstimate: localAvailable ? request.LocalEstimate!.Duration : null,
                LocalBudgetFailure: localFailure);
        }

        return new AiRoutingDecision(
            Action: AiRouteAction.AskExternalConsent,
            ProviderKind: AiProviderKind.External,
            ProviderId: request.ExternalProviderId,
            DurationEstimate: request.ExternalEstimate,
            RequiresExternalConsent: true,
            LocalFallbackAvailable: localAvailable,
            LocalFallbackEstimate: localAvailable ? request.LocalEstimate!.Duration : null,
            LocalBudgetFailure: localFailure);
    }

    private static AiRoutingDecision UseLocal(TaskDurationEstimate estimate) => new(
        Action: AiRouteAction.RunLocal,
        ProviderKind: AiProviderKind.Local,
        ProviderId: null,
        DurationEstimate: estimate,
        RequiresExternalConsent: false,
        LocalFallbackAvailable: false,
        LocalFallbackEstimate: null,
        LocalBudgetFailure: LocalBudgetFailure.None);

    private static AiRoutingDecision CannotRun(LocalBudgetFailure failure) => new(
        AiRouteAction.CannotRun,
        ProviderKind: null,
        ProviderId: null,
        DurationEstimate: null,
        RequiresExternalConsent: false,
        LocalFallbackAvailable: false,
        LocalFallbackEstimate: null,
        LocalBudgetFailure: failure);
}
