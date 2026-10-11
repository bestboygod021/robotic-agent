using Agent.Core.Localization;
using System.Text.Json;

namespace Agent.Core.Storage;

public sealed record ToolUserPreferences(
    string LanguageCode,
    IReadOnlyList<string> FavoriteToolIds,
    IReadOnlyList<string> RecentToolIds)
{
    public static ToolUserPreferences Default { get; } = new(
        CultureCatalog.DefaultLanguageCode,
        [],
        []);
}

/// <summary>
/// Persists user language preference, pinned favorite AI tools, and recently
/// opened AI tool IDs in %LOCALAPPDATA%\RoboticAgent\tool-preferences.json.
/// </summary>
public sealed class ToolPreferenceStore
{
    public const int MaximumRecentTools = 20;
    public const int MaximumFavoriteTools = 100;
    private const int MaximumFileBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ToolPreferenceStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboticAgent",
            "tool-preferences.json");
    }

    public async Task<ToolUserPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ToolUserPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sanitized = Sanitize(preferences);
            await SaveInternalAsync(sanitized, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ToolUserPreferences> ToggleFavoriteAsync(string toolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
            var favorites = current.FavoriteToolIds.ToList();
            var existingIndex = favorites.FindIndex(id => string.Equals(id, toolId, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                favorites.RemoveAt(existingIndex);
            }
            else
            {
                favorites.Insert(0, toolId.Trim());
            }

            var updated = Sanitize(current with { FavoriteToolIds = favorites });
            await SaveInternalAsync(updated, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ToolUserPreferences> RecordRecentAsync(string toolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
            var recents = current.RecentToolIds
                .Where(id => !string.Equals(id, toolId, StringComparison.OrdinalIgnoreCase))
                .Prepend(toolId.Trim())
                .Take(MaximumRecentTools)
                .ToArray();

            var updated = Sanitize(current with { RecentToolIds = recents });
            await SaveInternalAsync(updated, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ToolUserPreferences Sanitize(ToolUserPreferences preferences)
    {
        var language = CultureCatalog.NormalizeLanguageCode(preferences.LanguageCode);
        var favorites = (preferences.FavoriteToolIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumFavoriteTools)
            .ToArray();

        var recents = (preferences.RecentToolIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumRecentTools)
            .ToArray();

        return new ToolUserPreferences(language, favorites, recents);
    }

    private async Task<ToolUserPreferences> LoadInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath) || new FileInfo(_filePath).Length > MaximumFileBytes)
        {
            return ToolUserPreferences.Default;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<ToolUserPreferences>(json, JsonOptions);
            return loaded is null ? ToolUserPreferences.Default : Sanitize(loaded);
        }
        catch (JsonException)
        {
            return ToolUserPreferences.Default;
        }
    }

    private async Task SaveInternalAsync(ToolUserPreferences preferences, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("مسیر ذخیرهٔ ترجیحات ابزارها معتبر نیست.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(preferences, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
