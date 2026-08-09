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
        string message)
        => orchestrator.ProcessAsync(
            conversationId,
            message,
            CreateContext(),
            Context.ConnectionAborted);

    public Task<AgentTurnResult> Confirm(
        string conversationId)
        => orchestrator.ConfirmAsync(
            conversationId,
            CreateContext(),
            Context.ConnectionAborted);

    public Task<AgentTurnResult> Reject(
        string conversationId)
        => orchestrator.RejectAsync(
            conversationId,
            CreateContext(),
            Context.ConnectionAborted);

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
