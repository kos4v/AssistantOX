namespace OilCaseX.McpServer.Mcp.Dtos;

public sealed record ToolError(string Code, string Message, bool Retryable);

public sealed record ToolResponse<T>(bool Ok, T? Data, ToolError? Error)
{
    public static ToolResponse<T> Success(T data) => new(true, data, null);

    public static ToolResponse<T> Failure(ToolError error) => new(false, default, error);
}
