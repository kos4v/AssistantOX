using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OilCaseX.Agent.Api.Configuration;
using OilCaseX.Agent.Api.Runtime;
using OilCaseX.Agent.Application;
using OilCaseX.Agent.Infrastructure;
using OilCaseX.Agent.Api.Hubs;
using OilCaseX.Agent.Api.Authentication;

namespace OilCaseX.Agent.Api.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddOilCaseXAgentHost(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "O";
            options.UseUtcTimestamp = true;
        });

        var maxRequestBodyBytes = builder.Configuration.GetValue<long?>(
            $"{AgentServiceOptions.SectionName}:MaxRequestBodyBytes") ?? 1_048_576;
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
        });

        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXAgentConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AgentServiceOptions>()
            .BindConfiguration(AgentServiceOptions.SectionName)
            .Configure(options =>
            {
                options.VllmBaseUrl = Environment.GetEnvironmentVariable("LOCAL_LLM_BASE_URL")
                    ?? Environment.GetEnvironmentVariable("VLLM_BASE_URL")
                    ?? options.VllmBaseUrl;
                options.Model = Environment.GetEnvironmentVariable("LOCAL_LLM_MODEL")
                    ?? Environment.GetEnvironmentVariable("VLLM_MODEL")
                    ?? options.Model;
                options.VllmApiKey = Environment.GetEnvironmentVariable("LOCAL_LLM_API_KEY")
                    ?? Environment.GetEnvironmentVariable("VLLM_API_KEY")
                    ?? options.VllmApiKey;
                options.McpEndpoint = Environment.GetEnvironmentVariable("OILCASE_MCP_URL")
                    ?? options.McpEndpoint;
            })
            .ValidateDataAnnotations()
            .Validate(options => Uri.TryCreate(options.McpEndpoint, UriKind.Absolute, out _),
                "AgentService:McpEndpoint must be an absolute URL.")
            .Validate(options => Uri.TryCreate(options.VllmBaseUrl, UriKind.Absolute, out _),
                "AgentService:VllmBaseUrl must be an absolute URL.")
            .ValidateOnStart();

        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXAgentAuthentication(this WebApplicationBuilder builder)
    {
        var localDevelopmentAuthentication = builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue("Authentication:AllowDevelopmentAnonymous", false);
        var authentication = builder.Services.AddAuthentication(
            localDevelopmentAuthentication ? "Development" : JwtBearerDefaults.AuthenticationScheme);
        if (localDevelopmentAuthentication)
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                "Development", _ => { });
        }
        else
        {
            authentication
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = builder.Configuration["Authentication:Authority"];
                options.Audience = builder.Configuration["Authentication:Audience"];
                options.RequireHttpsMetadata = builder.Configuration.GetValue(
                    "Authentication:RequireHttpsMetadata", true);
            });
        }
        builder.Services.AddAuthorization();

        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXAgentApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AgentRuntimeReadiness>();
        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<AgentRuntimeOptions>(services =>
        {
            var source = services.GetRequiredService<IOptions<AgentServiceOptions>>().Value;
            return new AgentRuntimeOptions
            {
                McpEndpoint = source.McpEndpoint,
                VllmBaseUrl = source.VllmBaseUrl,
                VllmApiKey = source.VllmApiKey,
                Model = source.Model,
                RequestTimeoutSeconds = source.RequestTimeoutSeconds,
                MaxAgentSteps = source.MaxAgentSteps,
                MaxMcpCallsPerTurn = source.MaxMcpCallsPerTurn
            };
        });
        builder.Services.AddSingleton<IOptions<AgentRuntimeOptions>>(services =>
            Options.Create(services.GetRequiredService<AgentRuntimeOptions>()));
        builder.Services.AddSingleton<McpSdkToolClient>();
        builder.Services.AddSingleton<IAgentToolClient>(services => services.GetRequiredService<McpSdkToolClient>());
        builder.Services.AddSingleton<IAgentModelClient, OpenAiVllmModelClient>();
        builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<AgentOrchestrator>();
        return builder;
    }

    public static WebApplicationBuilder AddOilCaseXAgentObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("agent_host", () => HealthCheckResult.Healthy(), tags: ["ready"]);

        return builder;
    }

    public static WebApplication UseOilCaseXAgentPipeline(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<IOptions<AgentServiceOptions>>().Value;

        app.UseAuthentication();
        app.UseAuthorization();

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

        app.MapGet("/api/v1/agent/status", (
                IOptions<AgentServiceOptions> options,
                AgentRuntimeReadiness readiness) =>
            readiness.IsReady
                ? Results.Ok(new
                {
                    service = "OilCaseX.AgentService",
                    model = options.Value.Model,
                    status = "ready"
                })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
            .RequireAuthorization();

        app.MapHub<AgentChatHub>("/hubs/agent");

        app.MapPost("/api/v1/conversations/{conversationId}/messages", async (
                string conversationId,
                ChatTurnRequest request,
                HttpContext httpContext,
                AgentOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "message_required" });
            }

            var result = await orchestrator.ProcessAsync(
                conversationId,
                request.Message,
                CreateContext(httpContext),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization();

        app.MapPost("/api/v1/conversations/{conversationId}/confirm", async (
                string conversationId,
                HttpContext httpContext,
                AgentOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
        {
            var result = await orchestrator.ConfirmAsync(
                conversationId,
                CreateContext(httpContext),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization();

        app.MapPost("/api/v1/conversations/{conversationId}/reject", async (
                string conversationId,
                HttpContext httpContext,
                AgentOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
        {
            var result = await orchestrator.RejectAsync(
                conversationId,
                CreateContext(httpContext),
                cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization();

        return app;
    }

    private static AgentRequestContext CreateContext(HttpContext context)
    {
        var userId = context.User.FindFirst("sub")?.Value
            ?? context.User.Identity?.Name
            ?? "anonymous";
        var teamId = context.User.FindFirst("team_id")?.Value;
        var accessToken = context.Request.Headers.Authorization.ToString();
        return new AgentRequestContext(userId, teamId, accessToken);
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
