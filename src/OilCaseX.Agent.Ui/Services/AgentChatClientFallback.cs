using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

namespace OilCaseX.Agent.Ui.Services;

/// <summary>
/// ChatClientAgent fallback used only when the secured AgentService hub is unavailable.
/// It deliberately has no OilCaseX tools and cannot mutate domain state.
/// </summary>
public sealed class AgentChatClientFallback(IConfiguration configuration)
{
    private ChatClientAgent? agent;
    private AgentSession? session;

    public async Task<string> ReplyAsync(string message, CancellationToken cancellationToken)
    {
        agent ??= CreateAgent();
        session ??= await agent.CreateSessionAsync(cancellationToken: cancellationToken);
        var response = await agent.RunAsync(message, session, cancellationToken: cancellationToken);
        return string.Join(Environment.NewLine, response.Messages
            .Select(item => item.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private ChatClientAgent CreateAgent()
    {
        var baseUrl = configuration["AgentUi:VllmBaseUrl"]
            ?? Environment.GetEnvironmentVariable("LOCAL_LLM_BASE_URL")
            ?? Environment.GetEnvironmentVariable("VLLM_BASE_URL")
            ?? "http://192.168.19.120:1704/v1";
        var model = configuration["AgentUi:Model"]
            ?? Environment.GetEnvironmentVariable("LOCAL_LLM_MODEL")
            ?? Environment.GetEnvironmentVariable("VLLM_MODEL")
            ?? "prism-ml/bonsai-27b";
        var apiKey = configuration["AgentUi:ApiKey"]
            ?? Environment.GetEnvironmentVariable("LOCAL_LLM_API_KEY")
            ?? Environment.GetEnvironmentVariable("VLLM_API_KEY")
            ?? "lm-studio";
        var endpoint = new Uri(baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/");
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = endpoint });
        IChatClient chatClient = client.GetChatClient(model).AsIChatClient();
        return new ChatClientAgent(
            chatClient,
            instructions: "Ты резервный чат-ассистент OilCaseX. Не вызывай инструменты и не утверждай, что изменил данные OilCaseX.",
            name: "OilCaseXUiFallbackAgent");
    }
}
