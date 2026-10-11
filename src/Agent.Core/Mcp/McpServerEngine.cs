using Agent.Core.Skills;
using Agent.Core.Workflows;
using System.Text.Json;

namespace Agent.Core.Mcp;

/// <summary>
/// Model Context Protocol (MCP — JSON-RPC 2.0) Server Engine: exposes the 22
/// registered skills and 20 SMB playbooks as standard MCP tools and prompts so
/// external AI clients (Claude Desktop, Cursor, VS Code, Continue) can discover
/// and preview local skills safely.
/// </summary>
public sealed class McpServerEngine
{
    public const string ProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly SkillRegistry _registry;
    private readonly WorkflowEngine _workflowEngine;

    public McpServerEngine(SkillRegistry? registry = null, WorkflowEngine? workflowEngine = null)
    {
        _registry = registry ?? SkillRegistry.CreateDefault();
        _workflowEngine = workflowEngine ?? new WorkflowEngine(_registry);
    }

    public string HandleJsonRpcRequest(string rawJsonRequest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawJsonRequest);

        using var doc = JsonDocument.Parse(rawJsonRequest);
        var root = doc.RootElement;
        var idElement = root.TryGetProperty("id", out var idProp) ? idProp.Clone() : default;
        var method = root.TryGetProperty("method", out var mProp) ? mProp.GetString() ?? string.Empty : string.Empty;

        try
        {
            object resultPayload = method switch
            {
                "initialize" => new
                {
                    protocolVersion = ProtocolVersion,
                    serverInfo = new { name = "RoboticAgent.McpServer", version = "2.0.0" },
                    capabilities = new
                    {
                        tools = new { listChanged = false },
                        prompts = new { listChanged = false }
                    }
                },

                "tools/list" => new
                {
                    tools = _registry.Descriptors.Select(d => new
                    {
                        name = d.Id,
                        description = $"{d.GetDisplayName("en")} — {d.GetDescription("en")}",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                sourceDir = new { type = "string", description = "Authorized local source directory" },
                                outputDir = new { type = "string", description = "Authorized local output directory" }
                            },
                            required = new[] { "sourceDir", "outputDir" }
                        }
                    }).ToArray()
                },

                "prompts/list" => new
                {
                    prompts = PlaybookCatalog.All.Select(pb => new
                    {
                        name = pb.Id,
                        description = $"{pb.GetTitle("en")} ({pb.Category}): {pb.GetDescription("en")}"
                    }).ToArray()
                },

                "tools/call" => HandleToolCallDryRun(root),

                _ => throw new InvalidOperationException($"Unsupported MCP method: '{method}'")
            };

            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = idElement.ValueKind == JsonValueKind.Undefined ? (object?)null : idElement,
                result = resultPayload
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = idElement.ValueKind == JsonValueKind.Undefined ? (object?)null : idElement,
                error = new
                {
                    code = -32602,
                    message = ex.Message
                }
            }, JsonOptions);
        }
    }

    private object HandleToolCallDryRun(JsonElement root)
    {
        if (!root.TryGetProperty("params", out var paramsEl) ||
            !paramsEl.TryGetProperty("name", out var nameEl))
        {
            throw new InvalidOperationException("Missing tool name in tools/call params.");
        }

        var skillId = nameEl.GetString() ?? string.Empty;
        if (!_registry.TryGetDescriptor(skillId, out var descriptor) || descriptor is null)
        {
            throw new InvalidOperationException($"Skill '{skillId}' is not registered in SkillRegistry.");
        }

        return new
        {
            isError = false,
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"[Human-in-the-Loop Preview Gate] Skill '{descriptor.Id}' ({descriptor.GetDisplayName("en")}) validated. RequiresPreviewApproval={descriptor.RequiresPreviewApproval}, SupportsRollback={descriptor.SupportsRollback}."
                }
            }
        };
    }
}
