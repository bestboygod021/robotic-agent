using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Agent.Core.Storage;

public sealed record HistoryEntry(
    DateTimeOffset AtUtc,
    string Operation,
    int Copied,
    int Skipped,
    int Failed,
    bool Cancelled,
    string? Detail = null,
    string? PreviousEntryHash = null,
    string? EntryHash = null)
{
    [JsonIgnore]
    public string LocalTime => AtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");

    [JsonIgnore]
    public string Summary => Detail ??
        $"کپی‌شده: {Copied}  |  ردشده: {Skipped}  |  خطا: {Failed}" +
        (Cancelled ? "  |  لغوشده" : string.Empty);

    public static string ComputeHashHex(HistoryEntry entry, string previousHash)
    {
        var canonical = $"{previousHash}|{entry.AtUtc.ToUnixTimeMilliseconds()}|{entry.Operation}|{entry.Copied}|{entry.Skipped}|{entry.Failed}|{entry.Cancelled}|{entry.Detail ?? string.Empty}";
        var bytes = Encoding.UTF8.GetBytes(canonical);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
