using OilCaseX.McpServer.ApiClient.Generated;
using OilCaseX.McpServer.Mcp;
using OilCaseX.McpServer.Mcp.Dtos;

namespace OilCaseX.McpServer.ApiClient;

public static class OilCaseXErrorMapper
{
    public static ToolError Map(Exception exception)
    {
        if (exception is ApiException apiException)
        {
            return apiException.StatusCode switch
            {
                401 => new ToolError("unauthorized", "OilCaseX rejected the delegated identity.", false),
                403 => new ToolError("forbidden", "The current user cannot access this OilCaseX resource.", false),
                404 => new ToolError("not_found", "The requested OilCaseX resource was not found.", false),
                409 => new ToolError("conflict", "OilCaseX reported a resource conflict.", false),
                429 => new ToolError("rate_limited", "OilCaseX rate limit was reached.", true),
                >= 500 => new ToolError("upstream_unavailable", "OilCaseX is temporarily unavailable.", true),
                _ => new ToolError("upstream_error", "OilCaseX rejected the request.", false)
            };
        }

        return exception switch
        {
            UpstreamResponseTooLargeException => new ToolError(
                "upstream_response_too_large",
                "OilCaseX returned a response larger than the MCP limit.",
                false),
            UpstreamCircuitOpenException => new ToolError(
                "upstream_unavailable",
                "OilCaseX is temporarily unavailable.",
                true),
            HttpRequestException or TimeoutException or TaskCanceledException => new ToolError(
                "upstream_unavailable",
                "OilCaseX is temporarily unavailable.",
                true),
            _ => new ToolError(
                "internal_error",
                "The MCP server could not complete the request.",
                false)
        };
    }
}
