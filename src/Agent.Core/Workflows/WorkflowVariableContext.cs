using Agent.Core.Security;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Agent.Core.Workflows;

/// <summary>
/// Step Variable Passing engine (#45) that resolves workflow variables across steps
/// such as ${sourceDir}, ${outputDir}, ${workDir}, ${date}, ${prev.OutputPath},
/// ${prev.Count}, ${step.&lt;StepId&gt;.OutputPath}, and ${step.&lt;StepId&gt;.Count}.
/// </summary>
public sealed class WorkflowVariableContext
{
    private static readonly Regex PlaceholderPattern = new(
        @"\$\{(?<key>[a-zA-Z0-9_\.\-]+)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);

    public WorkflowVariableContext(WorkflowRuntimeInputs inputs, DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var sourceDir = PathGuard.ValidateSelectedFolder(inputs.SourceDirectory, "پوشهٔ مبدأ گردش‌کار");
        var outputDir = PathGuard.ValidateSelectedFolder(inputs.OutputDirectory, "پوشهٔ مقصد گردش‌کار");
        var workDir = string.IsNullOrWhiteSpace(inputs.WorkingDirectory)
            ? outputDir
            : PathGuard.ValidateSelectedFolder(inputs.WorkingDirectory, "پوشهٔ کاری گردش‌کار");

        var timestamp = (nowUtc ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        _variables["sourceDir"] = sourceDir;
        _variables["outputDir"] = outputDir;
        _variables["workDir"] = workDir;
        _variables["date"] = timestamp;
        _variables["prev.OutputPath"] = sourceDir;
        _variables["prev.OutputDir"] = sourceDir;
        _variables["prev.Count"] = CountTopLevelFilesSafe(sourceDir).ToString(CultureInfo.InvariantCulture);

        if (inputs.CustomVariables is not null)
        {
            foreach (var (key, val) in inputs.CustomVariables)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _variables[key.Trim()] = val ?? string.Empty;
                }
            }
        }
    }

    public string SourceDirectory => _variables["sourceDir"];
    public string OutputDirectory => _variables["outputDir"];
    public string WorkingDirectory => _variables["workDir"];

    public int PreviousCount =>
        int.TryParse(_variables.GetValueOrDefault("prev.Count", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : 0;

    public string PreviousOutputPath => _variables.GetValueOrDefault("prev.OutputPath", SourceDirectory);

    public void RecordStepOutcome(string stepId, string outputPath, int count)
    {
        var cleanStepId = stepId.Trim();
        var outputDir = Directory.Exists(outputPath)
            ? outputPath
            : Path.GetDirectoryName(outputPath) ?? OutputDirectory;

        var countText = count.ToString(CultureInfo.InvariantCulture);
        _variables["prev.OutputPath"] = outputPath;
        _variables["prev.OutputDir"] = outputDir;
        _variables["prev.Count"] = countText;

        _variables[$"step.{cleanStepId}.OutputPath"] = outputPath;
        _variables[$"step.{cleanStepId}.OutputDir"] = outputDir;
        _variables[$"step.{cleanStepId}.Count"] = countText;
    }

    public string ResolveString(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return _variables.TryGetValue(key, out var resolved) ? resolved : match.Value;
        });
    }

    public IReadOnlyDictionary<string, string> ResolveParameters(IReadOnlyDictionary<string, string> rawParameters)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in rawParameters)
        {
            resolved[key] = ResolveString(value);
        }

        return resolved;
    }

    public static int CountTopLevelFilesSafe(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly).Count();
    }
}
