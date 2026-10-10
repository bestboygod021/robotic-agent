using System.Text.Json;

namespace Agent.Core.Resources;

/// <summary>
/// Stores non-secret user resource preferences in the user's local app-data folder.
/// </summary>
public sealed class ResourcePreferenceStore
{
    private const int MaximumSettingsFileBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ResourcePreferenceStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboticAgent",
            "resource-preferences.json");
    }

    public async Task<ResourcePreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length > MaximumSettingsFileBytes)
            {
                return ResourcePreferences.Balanced;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
                var preferences = JsonSerializer.Deserialize<ResourcePreferences>(json, JsonOptions);
                return preferences is { IsValid: true } ? preferences : ResourcePreferences.Balanced;
            }
            catch (JsonException)
            {
                return ResourcePreferences.Balanced;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ResourcePreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!preferences.IsValid)
        {
            throw new ArgumentException("Resource preferences are outside the supported settings range.", nameof(preferences));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("مسیر تنظیمات منابع معتبر نیست.");
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
        finally
        {
            _gate.Release();
        }
    }
}
