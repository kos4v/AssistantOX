using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OilCaseX.McpServer.ApiClient.Generated;
using OilCaseX.McpServer.Mcp.Dtos;

namespace OilCaseX.McpServer.Mcp;

[McpServerToolType]
public sealed class ExecuteCreateBoreholeTools
{
    [McpServerTool(
        Name = "execute_create_borehole",
        Title = "Execute OilCaseX borehole creation",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    public static async Task<ToolResponse<ExecuteCreateBoreholeResult>> ExecuteAsync(
        string confirmationId,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(confirmationId))
        {
            return ToolResponse<ExecuteCreateBoreholeResult>.Failure(
                new ToolError("invalid_input", "confirmationId is required.", false));
        }

        using var scope = context.Server.Services!.CreateScope();
        var decorator = new ConfirmationToolDecorator(
            scope.ServiceProvider.GetRequiredService<IConfirmationStore>(),
            scope.ServiceProvider.GetRequiredService<DelegatedRequestContext>(),
            scope.ServiceProvider.GetRequiredService<IAuditSink>(),
            scope.ServiceProvider.GetRequiredService<ApiClient.IdempotencyKeyContext>());
        var client = scope.ServiceProvider.GetRequiredService<OilCaseXApiClientGenerated>();

        return await decorator.ExecuteAsync(
            confirmationId,
            BoreholePayloadCanonicalizer.ToolName,
            async (confirmation, token) =>
            {
                var request = JsonSerializer.Deserialize<PrepareCreateBoreholeRequest>(confirmation.CanonicalPayload)
                    ?? throw new InvalidOperationException("Confirmation payload is invalid.");
                var args = new PurchasedBoreholeCreateArgs { WellpadId = request.WellpadId, OrderId = request.OrderId };
                var validation = await client.ValidatePurchasedBoreholeAsync(args, token);
                if (!validation.IsValid || validation.Preview is null)
                {
                    var issue = validation.Issues?.FirstOrDefault();
                    throw new OilCaseXPreflightException(issue?.Code ?? "validation_failed", issue?.Message ?? "OilCaseX rejected the current request.");
                }
            },
            async (confirmation, _, token) =>
            {
                var request = JsonSerializer.Deserialize<PrepareCreateBoreholeRequest>(confirmation.CanonicalPayload)
                    ?? throw new InvalidOperationException("Confirmation payload is invalid.");
                var args = new PurchasedBoreholeCreateArgs { WellpadId = request.WellpadId, OrderId = request.OrderId };
                var boreholeId = await client.CreatePurchasedBoreholeAsync(args, token);
                var borehole = await client.GetBoreholeAsync(boreholeId, token);
                return new ExecuteCreateBoreholeResult(boreholeId, borehole);
            },
            cancellationToken);
    }
}

public sealed record ExecuteCreateBoreholeResult(int BoreholeId, BoreholeResult2? Borehole);

public sealed class OilCaseXPreflightException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
