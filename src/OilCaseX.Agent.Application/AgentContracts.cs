using System.Text.Json;

namespace OilCaseX.Agent.Application;

public sealed record ChatTurnRequest(string Message);

public sealed record AgentRequestContext(string UserId, string? TeamId, string? AccessToken);

public sealed record AgentMessage(string Role, string Content, DateTimeOffset CreatedAtUtc);

public sealed record AgentToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema,
    bool ReadOnly,
    bool Destructive,
    bool Idempotent);

public sealed record AgentToolCall(string Name, JsonElement Arguments, string CallId);

public sealed record ModelCompletion(
    string? Text,
    IReadOnlyList<AgentToolCall> ToolCalls);

public sealed record ToolObservation(
    string ToolName,
    bool Success,
    JsonElement? Data,
    string? ErrorCode,
    string? ErrorMessage,
    string? TraceId);

public sealed record PendingConfirmation(
    string ConfirmationId,
    DateTimeOffset ExpiresAtUtc,
    JsonElement Preview);

public sealed record ConversationState(
    string ConversationId,
    string UserId,
    string? TeamId,
    IReadOnlyList<AgentMessage> Messages,
    PendingConfirmation? PendingConfirmation = null)
{
    public ConversationState AddMessage(AgentMessage message)
        => this with { Messages = Messages.Append(message).TakeLast(24).ToArray() };
}

public sealed record AgentTurnResult(
    string ConversationId,
    string Status,
    string? Message,
    PendingConfirmation? Confirmation,
    ToolObservation? Observation,
    IReadOnlyList<string> Warnings);

public interface IAgentModelClient
{
    Task<ModelCompletion> CompleteAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken);
}

public interface IAgentToolClient
{
    Task<IReadOnlyList<AgentToolDefinition>> ListToolsAsync(CancellationToken cancellationToken);

    Task<ToolObservation> CallToolAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken);
}

public interface IConversationStore
{
    Task<ConversationState> GetOrCreateAsync(
        string conversationId,
        AgentRequestContext context,
        CancellationToken cancellationToken);

    Task SaveAsync(ConversationState state, CancellationToken cancellationToken);
}
