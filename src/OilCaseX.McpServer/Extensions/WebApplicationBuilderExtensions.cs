using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using OilCaseX.McpServer.ApiClient;
using OilCaseX.McpServer.ApiClient.Generated;
using OilCaseX.McpServer.Configuration;
using OilCaseX.McpServer.Diagnostics;
using OilCaseX.McpServer.Health;
using OilCaseX.McpServer.Mcp;
using OilCaseX.McpServer.Middleware;
using McpOptions = OilCaseX.McpServer.Configuration.McpServerOptions;

namespace OilCaseX.McpServer.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddOilCaseXHost(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "O";
            options.UseUtcTimestamp = true;
        });

        var maxRequestBodyBytes = builder.Configuration.GetValue<long?>(
            $"{McpOptions.SectionName}:MaxRequestBodyBytes")
            ?? McpOptions.DefaultMaxRequestBodyBytes;
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
        });

        builder.Services
            .AddOptions<McpOptions>()
            .BindConfiguration(McpOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<McpOptions>, McpServerOptionsValidator>();

        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IConfirmationStore, InMemoryConfirmationStore>();
        builder.Services.AddSingleton<IAuditSink, LoggingAuditSink>();
        builder.Services.AddScoped<DelegatedRequestContext>();

        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXApiClient(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient(OilCaseXApiHealthCheck.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(3);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OilCaseX.McpServer/0.1");
        });

        builder.Services.AddTransient<DelegatedJwtHandler>();
        builder.Services.AddTransient<CorrelationPropagationHandler>();
        builder.Services.AddTransient<ResponseSizeGuardHandler>();
        builder.Services.AddTransient<SafeGetResilienceHandler>();

        builder.Services.AddHttpClient<OilCaseXApiClientGenerated>(ConfigureOilCaseXHttpClient)
            .AddHttpMessageHandler<DelegatedJwtHandler>()
            .AddHttpMessageHandler<CorrelationPropagationHandler>()
            .AddHttpMessageHandler<ResponseSizeGuardHandler>()
            .AddHttpMessageHandler<SafeGetResilienceHandler>();
        builder.Services.AddScoped<IOilCaseXApiClientGenerated>(
            services => services.GetRequiredService<OilCaseXApiClientGenerated>());

        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXObservability(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck("mcp", () => HealthCheckResult.Healthy(), tags: ["ready"])
            .AddCheck<OilCaseXApiHealthCheck>(
                "oilcasex_api",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddSource(McpDiagnostics.ActivitySourceName);
                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
                }
            });

        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXMcp(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "OilCaseX.McpServer",
                    Version = "0.1.0"
                };
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<DiagnosticTools>()
            .WithTools(new OilCaseXGenericTools())
            ;

        return builder;
    }

    public static WebApplication UseOilCaseXMcpPipeline(this WebApplication app)
    {
        // Forces options validation during startup rather than on the first request.
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

        return app;
    }

    private static void ConfigureOilCaseXHttpClient(IServiceProvider services, HttpClient client)
    {
        var options = services.GetRequiredService<IOptions<McpOptions>>().Value;
        client.BaseAddress = options.GetOilCaseXBaseUri();
        client.Timeout = TimeSpan.FromSeconds(options.OilCaseXTimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OilCaseX.McpServer/0.1");
    }

    private static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
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
}
