using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using OilCaseX.Agent.Application;

namespace OilCaseX.Agent.Ui.Services;

public sealed class AgentHubConnection(IOptions<AgentUiOptions> options) : IAsyncDisposable
{
    private readonly AgentUiOptions settings = options.Value;
    private HubConnection? connection;

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        _ = await connection!.InvokeAsync<string>("Ping", cancellationToken);
    }

    public async Task<AgentTurnResult> SendMessageAsync(
        string conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        return await connection!.InvokeAsync<AgentTurnResult>(
            "SendMessage", conversationId, message, cancellationToken);
    }

    public async Task<AgentTurnResult> ConfirmAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        return await connection!.InvokeAsync<AgentTurnResult>(
            "Confirm", conversationId, cancellationToken);
    }

    public async Task<AgentTurnResult> RejectAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        return await connection!.InvokeAsync<AgentTurnResult>(
            "Reject", conversationId, cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (connection is not null)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                return;
            }

            await connection.StartAsync(cancellationToken);
            return;
        }

        connection = new HubConnectionBuilder()
            .WithUrl(settings.HubUrl, transportOptions =>
            {
                if (!string.IsNullOrWhiteSpace(settings.AccessToken))
                {
                    transportOptions.AccessTokenProvider = () =>
                        Task.FromResult<string?>(settings.AccessToken);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        await connection.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }
}
