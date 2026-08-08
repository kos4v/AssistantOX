using System.Text.Json;
using OilCaseX.Agent.Application;
using Xunit;

namespace OilCaseX.Agent.UnitTests;

public sealed class AgentOrchestratorTests
{
    [Fact]
    public async Task PrepareStopsBeforeExecuteAndConfirmExecutesOnlyAfterExplicitCall()
    {
        var model = new FakeModelClient(new ModelCompletion(
            null,
            [new AgentToolCall("prepare_create_borehole", Json("{\"wellpadId\":1,\"orderId\":2}"), "call-1")]));
        var tools = new FakeToolClient();
        tools.Observations["prepare_create_borehole"] = new ToolObservation(
            "prepare_create_borehole",
            true,
            Json("{\"confirmationId\":\"confirmation-1\",\"expiresAtUtc\":\"2099-01-01T00:00:00Z\",\"preview\":{\"name\":\"B-1\"}}"),
            null,
            null,
            null);
        tools.Observations["execute_create_borehole"] = new ToolObservation(
            "execute_create_borehole", true, Json("{\"id\":42}"), null, null, null);

        var orchestrator = Create(model, tools);
        var context = new AgentRequestContext("user-1", "team-1", null);

        var prepared = await orchestrator.ProcessAsync("conversation-1", "создай скважину", context, CancellationToken.None);

        Assert.Equal("confirmation_required", prepared.Status);
        Assert.Equal("confirmation-1", prepared.Confirmation?.ConfirmationId);
        Assert.DoesNotContain("execute_create_borehole", tools.Calls);

        var executed = await orchestrator.ConfirmAsync("conversation-1", context, CancellationToken.None);

        Assert.Equal("completed", executed.Status);
        Assert.Equal(["prepare_create_borehole", "execute_create_borehole"], tools.Calls);
    }

    private static AgentOrchestrator Create(FakeModelClient model, FakeToolClient tools)
        => new(model, tools, new InMemoryConversationStore(), TimeProvider.System, new AgentRuntimeOptions());

    private static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();

    private sealed class FakeModelClient(params ModelCompletion[] completions) : IAgentModelClient
    {
        private readonly Queue<ModelCompletion> queue = new(completions);

        public Task<ModelCompletion> CompleteAsync(IReadOnlyList<AgentMessage> messages, IReadOnlyList<AgentToolDefinition> tools, CancellationToken cancellationToken)
            => Task.FromResult(queue.Count == 0 ? new ModelCompletion("готово", []) : queue.Dequeue());
    }

    private sealed class FakeToolClient : IAgentToolClient
    {
        public Dictionary<string, ToolObservation> Observations { get; } = new(StringComparer.Ordinal);
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<AgentToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AgentToolDefinition>>([
                new AgentToolDefinition("prepare_create_borehole", "prepare", Json("{\"type\":\"object\"}"), false, true, true),
                new AgentToolDefinition("execute_create_borehole", "execute", Json("{\"type\":\"object\"}"), false, true, true)]);

        public Task<ToolObservation> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken)
        {
            Calls.Add(toolName);
            return Task.FromResult(Observations[toolName]);
        }
    }
}
