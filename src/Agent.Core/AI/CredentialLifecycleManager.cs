namespace Agent.Core.AI;

public sealed record ProviderCredentialStatus(
    string ProviderId,
    string DisplayName,
    bool IsConfigured,
    string MaskedPreview);

/// <summary>
/// Manages API key lifecycle (validation, masked preview, rotation, deletion)
/// across BYOK cloud providers on top of IAiCredentialStore (#76).
/// </summary>
public sealed class CredentialLifecycleManager
{
    public const int MinApiKeyLength = 12;

    private static readonly (string Id, string Name)[] SupportedProviders =
    [
        ("openai", "OpenAI (BYOK)"),
        ("anthropic", "Anthropic Claude (BYOK)"),
        ("gemini", "Google Gemini (BYOK)"),
        ("openrouter", "OpenRouter (BYOK)")
    ];

    private readonly IAiCredentialStore _store;

    public CredentialLifecycleManager(IAiCredentialStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task SaveOrRotateKeyAsync(
        string providerId,
        string rawApiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        var trimmed = rawApiKey?.Trim() ?? string.Empty;
        if (trimmed.Length < MinApiKeyLength || trimmed.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"کلید API باید حداقل {MinApiKeyLength} کاراکتر و بدون فاصله باشد.", nameof(rawApiKey));
        }

        await _store.SaveAsync(providerId.Trim().ToLowerInvariant(), trimmed, cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeKeyAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        await _store.DeleteAsync(providerId.Trim().ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProviderCredentialStatus>> GetAllStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var list = new List<ProviderCredentialStatus>(SupportedProviders.Length);
        foreach (var (id, name) in SupportedProviders)
        {
            var secret = await _store.ReadAsync(id, cancellationToken).ConfigureAwait(false);
            var configured = !string.IsNullOrWhiteSpace(secret);
            var masked = configured ? MaskSecret(secret!) : "ثبت‌نشده";
            list.Add(new ProviderCredentialStatus(id, name, configured, masked));
        }

        return list;
    }

    public static string MaskSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return "ثبت‌نشده";
        }

        if (secret.Length <= 8)
        {
            return "****";
        }

        return $"{secret[..4]}...{secret[^4..]}";
    }
}

public sealed class InMemoryAiCredentialStore : IAiCredentialStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(string providerId, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        _secrets[providerId] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> ReadAsync(string providerId, CancellationToken cancellationToken = default)
    {
        _secrets.TryGetValue(providerId, out var val);
        return Task.FromResult(val);
    }

    public Task DeleteAsync(string providerId, CancellationToken cancellationToken = default)
    {
        _secrets.Remove(providerId);
        return Task.CompletedTask;
    }
}
