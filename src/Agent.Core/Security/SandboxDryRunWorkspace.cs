namespace Agent.Core.Security;

/// <summary>
/// Isolated Sandbox Dry-Run Workspace (#80): creates a temporary sandbox folder,
/// mirrors a sample of source files into it so the user can test any skill or
/// workflow in complete isolation before touching real folders, and cleans up on Dispose.
/// </summary>
public sealed class SandboxDryRunWorkspace : IDisposable
{
    public SandboxDryRunWorkspace(string sourceDirectory, int maxSampleFiles = 20)
    {
        var validatedSource = PathGuard.ValidateSelectedFolder(sourceDirectory, "پوشهٔ مبدأ محیط آزمایشی");
        SandboxRoot = Path.Combine(Path.GetTempPath(), $"RoboticAgent_Sandbox_{Guid.NewGuid():N}");
        SandboxSourceDirectory = Path.Combine(SandboxRoot, "source");
        SandboxOutputDirectory = Path.Combine(SandboxRoot, "output");

        Directory.CreateDirectory(SandboxSourceDirectory);
        Directory.CreateDirectory(SandboxOutputDirectory);

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(validatedSource, "*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (count >= maxSampleFiles)
            {
                break;
            }

            var destFile = Path.Combine(SandboxSourceDirectory, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
            count++;
        }

        MirroredFilesCount = count;
    }

    public string SandboxRoot { get; }
    public string SandboxSourceDirectory { get; }
    public string SandboxOutputDirectory { get; }
    public int MirroredFilesCount { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(SandboxRoot))
            {
                Directory.Delete(SandboxRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of temp sandbox.
        }
    }
}
