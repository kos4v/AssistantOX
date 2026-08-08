using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using OilCaseX.McpServer.Configuration;

namespace OilCaseX.McpServer.Middleware;

public sealed class McpSecurityMiddleware : IDisposable
{
    private readonly RequestDelegate next;
    private readonly IOptions<McpServerOptions> options;
    private readonly ConcurrencyLimiter concurrencyLimiter;
    private readonly FixedWindowRateLimiter rateLimiter;

    public McpSecurityMiddleware(RequestDelegate next, IOptions<McpServerOptions> options)
    {
        this.next = next;
        this.options = options;
        concurrencyLimiter = new(new ConcurrencyLimiterOptions
        {
            PermitLimit = options.Value.MaxConcurrentRequests,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
        rateLimiter = new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.Value.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.Equals(options.Value.McpPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        using var rateLease = await rateLimiter.AcquireAsync(1, context.RequestAborted);
        if (!rateLease.IsAcquired)
        {
            await WriteLimitResponseAsync(context, "MCP request rate limit exceeded.");
            return;
        }

        using var concurrencyLease = await concurrencyLimiter.AcquireAsync(1, context.RequestAborted);
        if (!concurrencyLease.IsAcquired)
        {
            await WriteLimitResponseAsync(context, "MCP concurrency limit exceeded.");
            return;
        }

        await next(context);
    }

    public void Dispose()
    {
        concurrencyLimiter.Dispose();
        rateLimiter.Dispose();
    }

    private static async Task WriteLimitResponseAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = "60";
        await context.Response.WriteAsJsonAsync(new { error = "rate_limited", message });
    }
}
