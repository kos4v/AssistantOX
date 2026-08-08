using System.Net.Http.Headers;

namespace OilCaseX.McpServer.ApiClient;

/// <summary>
/// Copies only the delegated Bearer token from the current MCP HTTP request.
/// The token never enters the LLM/tool arguments or application logs.
/// </summary>
public sealed class DelegatedJwtHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(authorization, out var value)
            && string.Equals(value.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(value.Parameter))
        {
            request.Headers.Authorization = value;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
