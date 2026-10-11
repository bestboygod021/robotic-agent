namespace Agent.Core.Skills.Rollback;

public enum OperationRecordKind
{
    FileCreatedOrCopied,
    FileMovedOrRenamed,
    DirectoryCreated
}

public sealed record OperationRecord(
    OperationRecordKind Kind,
    string? OriginalPath,
    string TargetPath,
    long SizeBytes,
    string? ContentSha256Hex = null);

public sealed record OperationManifest(
    string OperationId,
    string SkillId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<OperationRecord> Records)
{
    public bool CanRollback => Records.Count > 0;
}

public sealed record RollbackExecutionResult(
    string OperationId,
    int RevertedCount,
    int SkippedCount,
    IReadOnlyList<string> Warnings)
{
    public bool Succeeded => Warnings.Count == 0 && RevertedCount > 0;
}
