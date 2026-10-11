using Agent.Core.Security;
using Agent.Core.Skills;
using System.Text.Json;

namespace Agent.Core.Automation;

public sealed record WindowPlacementSnapshot(
    AppId AppId,
    int Left,
    int Top,
    int Width,
    int Height);

public sealed record WorkspaceSnapshotProfile(
    string ProfileId,
    string DisplayName,
    IReadOnlyList<string> ProjectDirectories,
    IReadOnlyList<WindowPlacementSnapshot> Windows,
    DateTimeOffset SavedAtUtc);

/// <summary>
/// Workspace Snapshot Service (#35): captures and restores named workspace layouts
/// (authorized project folders + allowed AppCatalog window placements).
/// </summary>
public sealed class WorkspaceSnapshotService
{
    private readonly Dictionary<string, WorkspaceSnapshotProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<WorkspaceSnapshotProfile> Profiles => _profiles.Values;

    public WorkspaceSnapshotProfile SaveSnapshot(
        string profileId,
        string displayName,
        IReadOnlyList<string> projectDirectories,
        IReadOnlyList<WindowPlacementSnapshot> windows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(projectDirectories);
        ArgumentNullException.ThrowIfNull(windows);

        var validatedDirs = new List<string>(projectDirectories.Count);
        foreach (var dir in projectDirectories)
        {
            validatedDirs.Add(PathGuard.ValidateSelectedFolder(dir, "پوشهٔ پروفایل کاری"));
        }

        var snapshot = new WorkspaceSnapshotProfile(
            ProfileId: profileId.Trim(),
            DisplayName: displayName.Trim(),
            ProjectDirectories: validatedDirs,
            Windows: windows.ToArray(),
            SavedAtUtc: DateTimeOffset.UtcNow);

        _profiles[snapshot.ProfileId] = snapshot;
        return snapshot;
    }

    public WorkspaceSnapshotProfile RestoreSnapshot(string profileId)
    {
        if (!_profiles.TryGetValue(profileId, out var profile))
        {
            throw new KeyNotFoundException($"پروفایل فضای کاری '{profileId}' پیدا نشد.");
        }

        foreach (var dir in profile.ProjectDirectories)
        {
            PathGuard.ValidateSelectedFolder(dir, "بازیابی پوشهٔ پروفایل کاری");
        }

        return profile;
    }

    public string ExportProfileToJson(string profileId)
    {
        var profile = RestoreSnapshot(profileId);
        return JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
    }
}
