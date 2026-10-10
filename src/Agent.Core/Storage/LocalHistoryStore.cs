using System.Text.Json;

namespace Agent.Core.Storage;

/// <summary>
/// Dependency-free local history. It intentionally records counts and outcomes,
/// not source paths or file contents.
/// </summary>
public sealed class LocalHistoryStore
{
    private const int MaximumEntries = 100;
    private const long MaximumHistoryFileBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalHistoryStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboticAgent",
            "history.json");
    }

    public async Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = (await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false)).ToList();
            entries.Insert(0, entry);
            if (entries.Count > MaximumEntries)
            {
                entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);
            }

            await WriteAtomicallyAsync(entries, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<HistoryEntry>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<HistoryEntry>();
        }

        try
        {
            if (new FileInfo(_filePath).Length > MaximumHistoryFileBytes)
            {
                return Array.Empty<HistoryEntry>();
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOptions) ?? new List<HistoryEntry>();
        }
        catch (JsonException)
        {
            // A damaged history must not prevent the app from opening or running tasks.
            return Array.Empty<HistoryEntry>();
        }
    }

    private async Task WriteAtomicallyAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("مسیر تاریخچه معتبر نیست.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(entries, JsonOptions);
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
