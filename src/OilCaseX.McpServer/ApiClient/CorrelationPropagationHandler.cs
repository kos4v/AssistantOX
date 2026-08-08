using System.Diagnostics;

namespace OilCaseX.McpServer.ApiClient;

public sealed class CorrelationPropagationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var correlationId = httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        }

        request.Headers.Remove("X-Correlation-ID");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        var traceParent = Activity.Current?.Id
            ?? httpContext?.Request.Headers.TraceParent.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            request.Headers.Remove("traceparent");
            request.Headers.TryAddWithoutValidation("traceparent", traceParent);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
