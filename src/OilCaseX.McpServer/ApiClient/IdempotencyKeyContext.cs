namespace OilCaseX.McpServer.ApiClient;

/// <summary>Holds the idempotency key for the current scoped upstream operation.</summary>
public sealed class IdempotencyKeyContext
{
    public string? CurrentKey { get; set; }
}

public sealed class IdempotencyKeyHandler(IdempotencyKeyContext context) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.CurrentKey))
        {
            request.Headers.Remove("Idempotency-Key");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", context.CurrentKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
