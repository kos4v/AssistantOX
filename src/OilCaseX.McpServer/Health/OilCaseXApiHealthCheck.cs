using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OilCaseX.McpServer.Configuration;

namespace OilCaseX.McpServer.Health;

public sealed class OilCaseXApiHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<McpServerOptions> options,
    ILogger<OilCaseXApiHealthCheck> logger) : IHealthCheck
{
    public const string ClientName = "OilCaseX.Health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configuredOptions = options.Value;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                configuredOptions.GetOilCaseXHealthUri());

            using var response = await httpClientFactory
                .CreateClient(ClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode < 500)
            {
                return HealthCheckResult.Healthy(data: new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode
                });
            }

            logger.LogWarning(
                "OilCaseX API readiness check returned status code {StatusCode}",
                (int)response.StatusCode);

            return HealthCheckResult.Unhealthy(data: new Dictionary<string, object>
            {
                ["statusCode"] = (int)response.StatusCode
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("OilCaseX API readiness check was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "OilCaseX API readiness check failed with {ExceptionType}",
                exception.GetType().Name);

            return HealthCheckResult.Unhealthy("OilCaseX API is unavailable.");
        }
    }
}
