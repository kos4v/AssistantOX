using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OilCaseX.Agent.Application;

namespace OilCaseX.Agent.Api.Hubs;

[Authorize]
public sealed class AgentChatHub(AgentOrchestrator orchestrator) : Hub
{
    public Task<string> Ping() => Task.FromResult("ok");

    public Task<AgentTurnResult> SendMessage(
        string conversationId,
        string message,
        CancellationToken cancellationToken)
        => orchestrator.ProcessAsync(
            conversationId,
            message,
            CreateContext(),
            cancellationToken);

    public Task<AgentTurnResult> Confirm(
        string conversationId,
        CancellationToken cancellationToken)
        => orchestrator.ConfirmAsync(
            conversationId,
            CreateContext(),
            cancellationToken);

    public Task<AgentTurnResult> Reject(
        string conversationId,
        CancellationToken cancellationToken)
        => orchestrator.RejectAsync(
            conversationId,
            CreateContext(),
            cancellationToken);

    private AgentRequestContext CreateContext()
    {
        var httpContext = Context.GetHttpContext();
        var userId = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.Identity?.Name
            ?? "anonymous";
        var teamId = Context.User?.FindFirst("team_id")?.Value;
        var accessToken = httpContext?.Request.Headers.Authorization.ToString();
        return new AgentRequestContext(userId, teamId, accessToken);
    }
}
