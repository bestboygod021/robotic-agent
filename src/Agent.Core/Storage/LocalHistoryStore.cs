using System.Text.Json;

namespace Agent.Core.Storage;

public sealed record AuditChainVerificationResult(
    bool IsValid,
    int VerifiedCount,
    int? TamperedEntryIndex);

/// <summary>
/// Dependency-free local history with Tamper-Evident SHA-256 Hash Chain (#72).
/// Intentionally records counts and outcomes, not source paths or file contents.
/// </summary>
public sealed class LocalHistoryStore
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
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
            var prevHash = entries.Count > 0 && !string.IsNullOrWhiteSpace(entries[0].EntryHash)
                ? entries[0].EntryHash!
                : GenesisHash;
            var computedHash = HistoryEntry.ComputeHashHex(entry, prevHash);
            var chainedEntry = entry with
            {
                PreviousEntryHash = prevHash,
                EntryHash = computedHash
            };

            entries.Insert(0, chainedEntry);
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

    public async Task<AuditChainVerificationResult> VerifyHashChainAsync(CancellationToken cancellationToken = default)
    {
        var entries = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return new AuditChainVerificationResult(true, 0, null);
        }

        // Entries are stored newest-first (index 0 is newest, index Count-1 is oldest).
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var current = entries[i];
            if (string.IsNullOrWhiteSpace(current.EntryHash) || string.IsNullOrWhiteSpace(current.PreviousEntryHash))
            {
                return new AuditChainVerificationResult(false, entries.Count - 1 - i, i);
            }

            if (i < entries.Count - 1)
            {
                var older = entries[i + 1];
                if (!string.Equals(current.PreviousEntryHash, older.EntryHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new AuditChainVerificationResult(false, entries.Count - 1 - i, i);
                }
            }

            var expectedHash = HistoryEntry.ComputeHashHex(current, current.PreviousEntryHash);
            if (!string.Equals(current.EntryHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new AuditChainVerificationResult(false, entries.Count - 1 - i, i);
            }
        }

        return new AuditChainVerificationResult(true, entries.Count, null);
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
