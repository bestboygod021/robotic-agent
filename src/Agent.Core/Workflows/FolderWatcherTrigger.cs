using Agent.Core.Security;
using System.Collections.Concurrent;

namespace Agent.Core.Workflows;

public sealed record FolderWatcherEvent(
    string WorkflowId,
    string DirectoryPath,
    IReadOnlyList<string> ReadyFilePaths,
    WorkflowPreviewPlan PreviewPlan,
    DateTimeOffset TriggeredAtUtc);

/// <summary>
/// Safe Folder Watcher Trigger (#48) that monitors a user-authorized directory,
/// applies a debounce window and file-lock readiness check, and prepares an
/// End-to-End Dry-Run preview plan for Human-in-the-Loop confirmation.
/// </summary>
public sealed class FolderWatcherTrigger : IDisposable
{
    private readonly WorkflowEngine _engine;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pendingFiles = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;

    public FolderWatcherTrigger(
        WorkflowDefinition workflow,
        WorkflowRuntimeInputs runtimeInputs,
        TimeSpan? debounceWindow = null,
        WorkflowEngine? engine = null)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(runtimeInputs);

        Workflow = workflow;
        RuntimeInputs = runtimeInputs;
        WatchedDirectory = PathGuard.ValidateSelectedFolder(runtimeInputs.SourceDirectory, "پوشهٔ نگهبان هوشمند");
        DebounceWindow = debounceWindow ?? TimeSpan.FromMilliseconds(500);
        _engine = engine ?? new WorkflowEngine();
    }

    public WorkflowDefinition Workflow { get; }
    public WorkflowRuntimeInputs RuntimeInputs { get; }
    public string WatchedDirectory { get; }
    public TimeSpan DebounceWindow { get; }

    public event EventHandler<FolderWatcherEvent>? PreviewReady;

    public void StartWatching(string filter = "*.*")
    {
        StopWatching();
        _watcher = new FileSystemWatcher(WatchedDirectory, filter)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Created += (_, e) => NotifyFileDetected(e.FullPath);
        _watcher.Changed += (_, e) => NotifyFileDetected(e.FullPath);
        _watcher.EnableRaisingEvents = true;
    }

    public void StopWatching()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public void NotifyFileDetected(string filePath, DateTimeOffset? detectedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || PathGuard.IsSuspiciousDoubleExtension(filePath, out _))
        {
            return;
        }

        _pendingFiles[Path.GetFullPath(filePath)] = detectedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public FolderWatcherEvent? FlushReadyFiles(DateTimeOffset? nowUtc = null, string languageCode = "fa")
    {
        var currentTime = nowUtc ?? DateTimeOffset.UtcNow;
        var readyPaths = new List<string>();

        foreach (var (filePath, seenAt) in _pendingFiles.ToArray())
        {
            if (currentTime - seenAt < DebounceWindow)
            {
                continue;
            }

            if (IsFileReadyForReading(filePath))
            {
                if (_pendingFiles.TryRemove(filePath, out _))
                {
                    readyPaths.Add(filePath);
                }
            }
        }

        if (readyPaths.Count == 0)
        {
            return null;
        }

        var plan = _engine.PreviewWorkflow(Workflow, RuntimeInputs, languageCode);
        var evt = new FolderWatcherEvent(Workflow.Id, WatchedDirectory, readyPaths, plan, currentTime);
        PreviewReady?.Invoke(this, evt);
        return evt;
    }

    public static bool IsFileReadyForReading(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose() => StopWatching();
}
