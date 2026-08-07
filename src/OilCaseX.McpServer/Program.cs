using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using OpenTelemetry.Trace;
using OilCaseX.McpServer.Configuration;
using OilCaseX.McpServer.Diagnostics;
using OilCaseX.McpServer.Health;
using OilCaseX.McpServer.Mcp;
using OilCaseX.McpServer.Middleware;
using McpOptions = OilCaseX.McpServer.Configuration.McpServerOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});

var configuredMaxRequestBodyBytes = builder.Configuration.GetValue<long?>(
    $"{McpOptions.SectionName}:MaxRequestBodyBytes")
    ?? McpOptions.DefaultMaxRequestBodyBytes;
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = configuredMaxRequestBodyBytes;
});

builder.Services
    .AddOptions<McpOptions>()
    .BindConfiguration(McpOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<McpOptions>, McpServerOptionsValidator>();

builder.Services.AddHttpClient(OilCaseXApiHealthCheck.ClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("OilCaseX.McpServer/0.1");
});

builder.Services
    .AddHealthChecks()
    .AddCheck("mcp", () => HealthCheckResult.Healthy(), tags: ["ready"])
    .AddCheck<OilCaseXApiHealthCheck>(
        "oilcasex_api",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(McpDiagnostics.ActivitySourceName);

        // Export is opt-in. The default scaffold never writes trace payloads to stdout.
        if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
        {
            tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
        }
    });

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "OilCaseX.McpServer",
            Version = "0.1.0"
        };
    })
    .WithHttpTransport(options =>
    {
        // Stateless mode is sufficient for the stage-2 scaffold and is easy to scale.
        options.Stateless = true;
    })
    .WithTools<DiagnosticTools>();

var app = builder.Build();

// The options are resolved explicitly so validation also happens when the app is started
// by a container without an HTTP request.
_ = app.Services.GetRequiredService<IOptions<McpOptions>>().Value;

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ResponseSizeLimitMiddleware>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});

var serverOptions = app.Services.GetRequiredService<IOptions<McpOptions>>().Value;
app.MapMcp(serverOptions.McpPath);

app.Run();

static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var entries = report.Entries.ToDictionary(
        pair => pair.Key,
        pair => new
        {
            status = pair.Value.Status.ToString(),
            durationMs = pair.Value.Duration.TotalMilliseconds,
            description = pair.Value.Description
        });

    await context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        entries
    });
}

public partial class Program;
