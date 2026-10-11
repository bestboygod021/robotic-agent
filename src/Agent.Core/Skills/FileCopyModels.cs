using Agent.Core.Skills.Rollback;

namespace Agent.Core.Skills;

public enum FileConflictPolicy
{
    SkipExisting,
    AutoRenameNumbered,
    CopyOnlyIfNewerWithSuffix
}

public sealed record FileCopyRequest(
    string SourceDirectory,
    string DestinationDirectory,
    string Extension,
    bool IncludeSubdirectories = false,
    int MaxDepth = 5,
    FileConflictPolicy ConflictPolicy = FileConflictPolicy.SkipExisting);

public sealed record FileCopyCandidate(
    string SourcePath,
    string Name,
    long SizeBytes,
    string DestinationPath,
    bool AlreadyExists,
    string RelativePath = "",
    bool WasAutoRenamed = false);

public sealed record FileCopyPreview(
    string SourceDirectory,
    string DestinationDirectory,
    string Extension,
    IReadOnlyList<FileCopyCandidate> Candidates,
    long DestinationAvailableBytes,
    bool HasDiskHeadroom,
    bool IncludeSubdirectories = false,
    int MaxDepth = 0,
    FileConflictPolicy ConflictPolicy = FileConflictPolicy.SkipExisting)
{
    public int ReadyCount => HasDiskHeadroom ? Candidates.Count(candidate => !candidate.AlreadyExists) : 0;
    public int ExistingCount => Candidates.Count(candidate => candidate.AlreadyExists);
    public long TotalBytes => Candidates.Where(candidate => !candidate.AlreadyExists).Sum(candidate => candidate.SizeBytes);
}

public sealed record FileCopyProgress(int Completed, int Total, string FileName);

public sealed record FileCopyExecutionResult(
    int Copied,
    int Skipped,
    IReadOnlyList<string> FailedFileNames,
    bool Cancelled,
    OperationManifest? RollbackManifest = null)
{
    public int Failed => FailedFileNames.Count;
}
