using System.Text.Json;

namespace OilCaseX.Agent.Application;

public sealed class AgentOrchestrator(
    IAgentModelClient modelClient,
    IAgentToolClient toolClient,
    IConversationStore conversationStore,
    TimeProvider timeProvider,
    AgentRuntimeOptions runtimeOptions)
{
    private const string PrepareToolName = "prepare_create_borehole";
    private const string ExecuteToolName = "execute_create_borehole";

    public async Task<AgentTurnResult> ProcessAsync(
        string conversationId,
        string userMessage,
        AgentRequestContext context,
        CancellationToken cancellationToken)
    {
        var state = await conversationStore.GetOrCreateAsync(conversationId, context, cancellationToken);
        if (state.PendingConfirmation is not null)
        {
            return new AgentTurnResult(
                conversationId,
                "confirmation_required",
                "Операция уже подготовлена. Подтвердите или отклоните preview.",
                state.PendingConfirmation,
                null,
                ["A pending confirmation must be resolved before a new write operation."]);
        }

        state = state.AddMessage(new AgentMessage("user", userMessage, timeProvider.GetUtcNow()));
        await conversationStore.SaveAsync(state, cancellationToken);

        IReadOnlyList<AgentToolDefinition> tools;
        try
        {
            tools = (await toolClient.ListToolsAsync(cancellationToken))
                .Where(IsModelToolAllowed)
                .ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new AgentTurnResult(conversationId, "degraded", "OilCaseX MCP сейчас недоступен. Попробуйте позже.", null, null, ["MCP unavailable."]);
        }
        var messages = state.Messages.ToList();
        var toolCalls = 0;

        for (var step = 0; step < runtimeOptions.MaxAgentSteps; step++)
        {
            ModelCompletion completion;
            try
            {
                completion = await modelClient.CompleteAsync(messages, tools, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new AgentTurnResult(conversationId, "degraded", "LLM сейчас недоступна. Попробуйте позже.", null, null, ["LLM unavailable."]);
            }
            if (completion.ToolCalls.Count == 0)
            {
                var text = string.IsNullOrWhiteSpace(completion.Text)
                    ? "Не удалось сформировать ответ по доступным данным."
                    : completion.Text.Trim();
                state = state.AddMessage(new AgentMessage("assistant", text, timeProvider.GetUtcNow()));
                await conversationStore.SaveAsync(state, cancellationToken);
                return new AgentTurnResult(conversationId, "completed", text, null, null, []);
            }

            foreach (var toolCall in completion.ToolCalls)
            {
                if (++toolCalls > runtimeOptions.MaxMcpCallsPerTurn || tools.All(tool => tool.Name != toolCall.Name))
                {
                    return new AgentTurnResult(conversationId, "policy_blocked", "Запрошенная операция недоступна.", null, null, ["Tool policy blocked the requested operation."]);
                }

                if (toolCall.Name == ExecuteToolName)
                {
                    return new AgentTurnResult(conversationId, "confirmation_required", "Для выполнения операции требуется явное подтверждение.", state.PendingConfirmation, null, []);
                }

                var observation = await toolClient.CallToolAsync(toolCall.Name, toolCall.Arguments, cancellationToken);
                if (!observation.Success)
                {
                    var error = observation.ErrorMessage ?? "OilCaseX MCP не выполнил операцию.";
                    state = state.AddMessage(new AgentMessage("tool", error, timeProvider.GetUtcNow()));
                    await conversationStore.SaveAsync(state, cancellationToken);
                    return new AgentTurnResult(conversationId, "failed", error, null, observation, []);
                }

                if (toolCall.Name == PrepareToolName && TryReadConfirmation(observation, out var confirmation))
                {
                    state = state with { PendingConfirmation = confirmation };
                    await conversationStore.SaveAsync(state, cancellationToken);
                    return new AgentTurnResult(
                        conversationId,
                        "confirmation_required",
                        "Проверьте preview и подтвердите создание скважины.",
                        confirmation,
                        observation,
                        []);
                }

                var toolMessage = new AgentMessage("tool", SerializeObservation(observation), timeProvider.GetUtcNow());
                state = state.AddMessage(toolMessage);
                await conversationStore.SaveAsync(state, cancellationToken);
                messages = state.Messages.ToList();
            }
        }

        return new AgentTurnResult(conversationId, "step_limit", "Превышен лимит шагов агентного цикла.", null, null, ["Agent step limit reached."]);
    }

    public async Task<AgentTurnResult> ConfirmAsync(
        string conversationId,
        AgentRequestContext context,
        CancellationToken cancellationToken)
    {
        var state = await conversationStore.GetOrCreateAsync(conversationId, context, cancellationToken);
        var pending = state.PendingConfirmation;
        if (pending is null || pending.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return new AgentTurnResult(conversationId, "confirmation_invalid", "Подтверждение отсутствует или истекло.", null, null, []);
        }

        using var argumentsDocument = JsonDocument.Parse($"{{\"confirmationId\":{JsonSerializer.Serialize(pending.ConfirmationId)}}}");
        var observation = await toolClient.CallToolAsync(ExecuteToolName, argumentsDocument.RootElement, cancellationToken);
        state = state with { PendingConfirmation = null };
        state = state.AddMessage(new AgentMessage("tool", SerializeObservation(observation), timeProvider.GetUtcNow()));
        await conversationStore.SaveAsync(state, cancellationToken);

        return observation.Success
            ? new AgentTurnResult(conversationId, "completed", "Скважина создана. Результат подтверждён OilCaseX.", null, observation, [])
            : new AgentTurnResult(conversationId, "failed", observation.ErrorMessage, null, observation, []);
    }

    public async Task<AgentTurnResult> RejectAsync(
        string conversationId,
        AgentRequestContext context,
        CancellationToken cancellationToken)
    {
        var state = await conversationStore.GetOrCreateAsync(conversationId, context, cancellationToken);
        if (state.PendingConfirmation is null)
        {
            return new AgentTurnResult(conversationId, "confirmation_invalid", "Активное подтверждение отсутствует.", null, null, []);
        }

        await conversationStore.SaveAsync(state with { PendingConfirmation = null }, cancellationToken);
        return new AgentTurnResult(conversationId, "rejected", "Операция отклонена. Данные не изменены.", null, null, []);
    }

    private static bool IsModelToolAllowed(AgentToolDefinition tool)
        => tool.ReadOnly || string.Equals(tool.Name, PrepareToolName, StringComparison.Ordinal);

    private static bool TryReadConfirmation(ToolObservation observation, out PendingConfirmation confirmation)
    {
        confirmation = null!;
        if (observation.Data is not { ValueKind: JsonValueKind.Object } data)
        {
            return false;
        }

        var root = data;
        if (root.TryGetProperty("data", out var nested) && nested.ValueKind == JsonValueKind.Object)
        {
            root = nested;
        }

        if (!root.TryGetProperty("confirmationId", out var id)
            || !root.TryGetProperty("expiresAtUtc", out var expires)
            || !DateTimeOffset.TryParse(expires.GetString(), out var expiresAt))
        {
            return false;
        }

        var preview = root.TryGetProperty("preview", out var previewValue)
            ? previewValue.Clone()
            : root.Clone();
        confirmation = new PendingConfirmation(id.GetString()!, expiresAt, preview);
        return true;
    }

    private static string SerializeObservation(ToolObservation observation)
        => observation.Success
            ? observation.Data?.GetRawText() ?? "{}"
            : JsonSerializer.Serialize(new { error = observation.ErrorCode, message = observation.ErrorMessage });
}
