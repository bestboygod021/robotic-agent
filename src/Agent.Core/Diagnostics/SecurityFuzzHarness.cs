using Agent.Core.AI;
using Agent.Core.Security;
using Agent.Core.Skills;
using Agent.Core.Skills.Documents;
using Agent.Core.Workflows;
using System.Text.Json;

namespace Agent.Core.Diagnostics;

public sealed record SecurityFuzzReport(
    bool AllVectorsBlockedOrSanitized,
    int TotalVectorsTested,
    int PassedVectorsCount,
    IReadOnlyList<string> Failures);

/// <summary>
/// Automated Security Fuzz &amp; Boundary Test Harness (#92): verifies that deceptive
/// double extensions, path traversal vectors, shell injections, and CSV formula
/// injections are deterministically blocked or neutralized.
/// </summary>
public static class SecurityFuzzHarness
{
    private static readonly string[] DoubleExtensionVectors =
    [
        "invoice.pdf.exe",
        "contract.docx.bat",
        "photo.jpg.scr",
        "salary.xlsx.cmd",
        "backup.zip.ps1",
        "notes.txt.vbs",
        "form.pdf.msi"
    ];

    private static readonly string[] WorkflowPathTraversalVectors =
    [
        "../../etc/passwd",
        "..\\..\\Windows\\System32\\cmd.exe",
        "C:\\Windows\\System32",
        "/etc/shadow",
        "\\\\evil-server\\share\\payload"
    ];

    private static readonly string[] CsvFormulaVectors =
    [
        "=CMD|' /C calc'!A0",
        "+SUM(1,2)",
        "-2+3+cmd",
        "@IMPORTXML(\"http://evil.example/\",\"//a\")"
    ];

    private static readonly string[] PlanInjectionVectors =
    [
        "cmd.exe /c calc",
        "powershell -enc AAA",
        "../../secret",
        "/bin/sh -c id"
    ];

    public static SecurityFuzzReport RunAllSuites(SkillRegistry? registry = null)
    {
        var skillRegistry = registry ?? SkillRegistry.CreateDefault();
        var validator = new PlanSchemaValidator(skillRegistry);
        var failures = new List<string>();
        var total = 0;

        foreach (var fileVec in DoubleExtensionVectors)
        {
            total++;
            if (!PathGuard.IsSuspiciousDoubleExtension(fileVec, out _))
            {
                failures.Add($"Double-extension vector not detected: {fileVec}");
            }
        }

        foreach (var pathVec in WorkflowPathTraversalVectors)
        {
            total++;
            var wf = new WorkflowDefinition(
                Id: "fuzz.wf",
                Version: "1.0",
                Category: "Fuzz",
                Titles: new Dictionary<string, string> { ["fa"] = "Fuzz" },
                Descriptions: new Dictionary<string, string> { ["fa"] = "Fuzz" },
                Steps:
                [
                    new WorkflowStepDefinition("s1", FileCopySkill.SkillId, new Dictionary<string, string> { ["sourceDir"] = pathVec })
                ]);

            try
            {
                WorkflowSerializer.ValidateWorkflowSecurity(wf, skillRegistry);
                failures.Add($"Workflow path traversal vector was not blocked: {pathVec}");
            }
            catch (InvalidOperationException)
            {
                // Expected block
            }
        }

        foreach (var csvVec in CsvFormulaVectors)
        {
            total++;
            var sanitized = InvoiceExtractSkill.SanitizeCsvCell(csvVec);
            if (!sanitized.StartsWith("'", StringComparison.Ordinal))
            {
                failures.Add($"CSV formula injection vector not neutralized: {csvVec}");
            }
        }

        foreach (var planVec in PlanInjectionVectors)
        {
            total++;
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { query = planVec }));
            var proposal = new AiPlanProposal("Fuzz", [new AiProposedStep(FileCopySkill.SkillId, doc.RootElement.Clone())], null);
            var res = validator.Validate(proposal);
            if (res.IsValid)
            {
                failures.Add($"PlanSchemaValidator did not block injection vector: {planVec}");
            }
        }

        return new SecurityFuzzReport(
            AllVectorsBlockedOrSanitized: failures.Count == 0,
            TotalVectorsTested: total,
            PassedVectorsCount: total - failures.Count,
            Failures: failures);
    }
}
