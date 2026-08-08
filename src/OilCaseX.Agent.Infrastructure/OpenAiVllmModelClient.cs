using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OilCaseX.Agent.Application;
using System.ClientModel;

namespace OilCaseX.Agent.Infrastructure;

/// <summary>OpenAI-compatible adapter for vLLM/LM Studio, based on the same SDK setup as first_agent.</summary>
public sealed class OpenAiVllmModelClient(
    IOptions<AgentRuntimeOptions> options,
    McpSdkToolClient mcpToolClient) : IAgentModelClient
{
    private const string Instructions = """
        Ты — чат-ассистент OilCaseX. Отвечай на языке пользователя и кратко объясняй результат.
        Для данных OilCaseX используй только доступные MCP-инструменты, не выдумывай сущности и ID.
        Перед созданием скважины всегда вызывай prepare_create_borehole и показывай пользователю
        кустовую площадку, позицию и сгенерированное имя. Не вызывай execute_create_borehole в той
        же реплике: дождись отдельного явного подтверждения пользователя. После подтверждения используй
        ровно тот confirmationId, который вернул prepare_create_borehole.
        """;

    private readonly IChatClient chatClient = CreateChatClient(options.Value);

    public async Task<ModelCompletion> CompleteAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        var modelTools = await mcpToolClient.ListModelToolsAsync(cancellationToken);
        var allowed = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var selectedTools = modelTools.Where(tool => allowed.Contains(tool.Name)).ToList();
        var chatMessages = messages.Select(ToChatMessage).ToList();
        var response = await chatClient.GetResponseAsync(
            chatMessages,
            new ChatOptions
            {
                Instructions = Instructions,
                Tools = selectedTools,
                AllowMultipleToolCalls = false,
                MaxOutputTokens = 4096
            },
            cancellationToken);

        var calls = response.Messages
            .SelectMany(message => message.Contents.OfType<FunctionCallContent>())
            .Where(call => !call.InformationalOnly)
            .Select(call => new AgentToolCall(
                call.Name,
                JsonSerializer.SerializeToElement(call.Arguments ?? new Dictionary<string, object?>()),
                call.CallId))
            .ToArray();

        return new ModelCompletion(response.Text, calls);
    }

    private static IChatClient CreateChatClient(AgentRuntimeOptions options)
    {
        var endpoint = new Uri(options.VllmBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? options.VllmBaseUrl
            : options.VllmBaseUrl + "/");
        var client = new OpenAIClient(
            new ApiKeyCredential(options.VllmApiKey),
            new OpenAIClientOptions { Endpoint = endpoint });
        return client.GetChatClient(options.Model).AsIChatClient();
    }

    private static ChatMessage ToChatMessage(AgentMessage message)
        => new(ToRole(message.Role), message.Content);

    private static ChatRole ToRole(string role)
        => role switch
        {
            "assistant" => ChatRole.Assistant,
            "tool" => ChatRole.Tool,
            "system" => ChatRole.System,
            _ => ChatRole.User
        };
}
