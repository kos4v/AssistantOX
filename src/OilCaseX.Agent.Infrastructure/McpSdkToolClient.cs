using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Client;
using ModelContextProtocol;
using Microsoft.Extensions.AI;
using OilCaseX.Agent.Application;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace OilCaseX.Agent.Infrastructure;

/// <summary>Adapter over the official MCP C# SDK used by the agent runtime.</summary>
public sealed class McpSdkToolClient(
    IOptions<AgentRuntimeOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ILogger<McpSdkToolClient> logger) : IAgentToolClient
{
    private McpClient? modelClient;

    public async Task<IReadOnlyList<AITool>> ListModelToolsAsync(CancellationToken cancellationToken)
    {
        modelClient ??= await CreateClientAsync(cancellationToken);
        return (await modelClient.ListToolsAsync(cancellationToken: cancellationToken)).Cast<AITool>().ToArray();
    }

    public async Task<IReadOnlyList<AgentToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
    {
        await using var client = await CreateClientAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        return tools.Select(ToDefinition).ToArray();
    }

    public async Task<ToolObservation> CallToolAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var client = await CreateClientAsync(cancellationToken);
            var values = arguments.ValueKind == JsonValueKind.Object
                ? arguments.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value))
                : new Dictionary<string, object?>();
            var result = await client.CallToolAsync(toolName, values, cancellationToken: cancellationToken);
            var isError = result.IsError == true;
            var data = ExtractData(result);
            var errorMessage = isError ? ExtractText(result) : null;
            return new ToolObservation(
                toolName,
                !isError,
                data,
                isError ? "mcp_tool_error" : null,
                errorMessage,
                null);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or McpException)
        {
            logger.LogWarning(exception, "MCP tool {ToolName} is unavailable", toolName);
            return new ToolObservation(toolName, false, null, "mcp_unavailable", exception.Message, null);
        }
    }

    private async Task<McpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var endpoint = new Uri(options.Value.McpEndpoint);
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds)
        };
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Name = "OilCaseX Agent MCP",
                Endpoint = endpoint,
            },
            httpClient,
            LoggerFactory.Create(_ => { }),
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }

    private static AgentToolDefinition ToDefinition(McpClientTool tool)
    {
        var annotation = tool.ProtocolTool.Annotations;
        return new AgentToolDefinition(
            tool.Name,
            tool.Description ?? string.Empty,
            tool.JsonSchema,
            annotation?.ReadOnlyHint == true,
            annotation?.DestructiveHint == true,
            annotation?.IdempotentHint == true);
    }

    private static JsonElement? ExtractData(ModelContextProtocol.Protocol.CallToolResult result)
    {
        if (result.StructuredContent is JsonElement structured
            && structured.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            return structured.Clone();
        }

        var text = ExtractText(result);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { text });
        }
    }

    private static string? ExtractText(ModelContextProtocol.Protocol.CallToolResult result)
        => result.Content is null
            ? null
            : string.Join("\n", result.Content.Select(content =>
                content is ModelContextProtocol.Protocol.TextContentBlock text
                    ? text.Text
                    : content.ToString()));

    private static object? ToObject(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.Clone()
        };
}
