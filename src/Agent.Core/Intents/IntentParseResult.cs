namespace Agent.Core.Intents;

public enum IntentStatus
{
    Ready,
    NeedsClarification,
    Unsupported
}

public sealed record IntentParseResult(
    IntentStatus Status,
    string? Extension,
    string Message,
    double Confidence = 0,
    bool Inferred = false,
    FileFilterCriteria? FilterCriteria = null);
