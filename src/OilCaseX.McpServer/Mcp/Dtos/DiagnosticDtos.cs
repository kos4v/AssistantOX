namespace OilCaseX.McpServer.Mcp.Dtos;

public sealed record PingResult(string Status, string Message, string Server, DateTimeOffset Utc);
