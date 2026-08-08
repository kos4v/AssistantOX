using System.Collections.Concurrent;

namespace OilCaseX.Agent.Application;

public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, ConversationState> conversations = new(StringComparer.Ordinal);

    public Task<ConversationState> GetOrCreateAsync(
        string conversationId,
        AgentRequestContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = conversations.GetOrAdd(
            conversationId,
            _ => new ConversationState(conversationId, context.UserId, context.TeamId, []));

        if (!string.Equals(state.UserId, context.UserId, StringComparison.Ordinal)
            || !string.Equals(state.TeamId, context.TeamId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Conversation belongs to another user or team.");
        }

        return Task.FromResult(state);
    }

    public Task SaveAsync(ConversationState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        conversations[state.ConversationId] = state;
        return Task.CompletedTask;
    }
}
