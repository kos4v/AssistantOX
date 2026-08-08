using System.Net;

namespace OilCaseX.McpServer.ApiClient;

/// <summary>
/// Retries only idempotent GET requests and opens a small circuit after repeated
/// transport/5xx failures. POST/write requests are never retried by this handler.
/// </summary>
public sealed class SafeGetResilienceHandler(ILogger<SafeGetResilienceHandler> logger) : DelegatingHandler
{
    private const int MaxAttempts = 3;
    private const int FailureThreshold = 5;
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(30);
    private readonly object sync = new();
    private int consecutiveFailures;
    private DateTimeOffset circuitOpenUntil;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        ThrowIfCircuitOpen();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt == MaxAttempts)
                {
                    if (!IsTransient(response.StatusCode))
                    {
                        ResetCircuit();
                    }
                    else
                    {
                        RecordFailure();
                    }

                    return response;
                }

                response.Dispose();
                RecordFailure();
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                RecordFailure();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
        }

        throw new InvalidOperationException("GET resilience handler reached an unexpected state.");
    }

    private void ThrowIfCircuitOpen()
    {
        lock (sync)
        {
            if (circuitOpenUntil > DateTimeOffset.UtcNow)
            {
                throw new UpstreamCircuitOpenException();
            }
        }
    }

    private void RecordFailure()
    {
        lock (sync)
        {
            consecutiveFailures++;
            if (consecutiveFailures >= FailureThreshold)
            {
                circuitOpenUntil = DateTimeOffset.UtcNow.Add(CircuitBreakDuration);
                logger.LogWarning("OilCaseX GET circuit opened for {DurationSeconds} seconds", CircuitBreakDuration.TotalSeconds);
            }
        }
    }

    private void ResetCircuit()
    {
        lock (sync)
        {
            consecutiveFailures = 0;
            circuitOpenUntil = default;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || (int)statusCode >= 500;
    }
}
