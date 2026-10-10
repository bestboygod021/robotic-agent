namespace Agent.Core.Skills;

public sealed record FileCopyRequest(
    string SourceDirectory,
    string DestinationDirectory,
    string Extension);

public sealed record FileCopyCandidate(
    string SourcePath,
    string Name,
    long SizeBytes,
    string DestinationPath,
    bool AlreadyExists);

public sealed record FileCopyPreview(
    string SourceDirectory,
    string DestinationDirectory,
    string Extension,
    IReadOnlyList<FileCopyCandidate> Candidates,
    long DestinationAvailableBytes,
    bool HasDiskHeadroom)
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
    bool Cancelled)
{
    public int Failed => FailedFileNames.Count;
}
