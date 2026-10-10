using System.Text.Json.Serialization;

namespace Agent.Core.Storage;

public sealed record HistoryEntry(
    DateTimeOffset AtUtc,
    string Operation,
    int Copied,
    int Skipped,
    int Failed,
    bool Cancelled,
    string? Detail = null)
{
    [JsonIgnore]
    public string LocalTime => AtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");

    [JsonIgnore]
    public string Summary => Detail ??
        $"کپی‌شده: {Copied}  |  ردشده: {Skipped}  |  خطا: {Failed}" +
        (Cancelled ? "  |  لغوشده" : string.Empty);
}
