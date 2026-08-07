using ModelContextProtocol.Server;

namespace OilCaseX.McpServer.Mcp;

[McpServerToolType]
public sealed class DiagnosticTools
{
    [McpServerTool(
        Name = "mcp_server_ping",
        Title = "MCP server ping",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    public static PingResult Ping(string? message = null)
    {
        return new PingResult(
            Status: "ok",
            Message: string.IsNullOrWhiteSpace(message) ? "OilCaseX MCP is ready." : message.Trim(),
            Server: "OilCaseX.McpServer",
            Utc: DateTimeOffset.UtcNow);
    }
}

public sealed record PingResult(
    string Status,
    string Message,
    string Server,
    DateTimeOffset Utc);
