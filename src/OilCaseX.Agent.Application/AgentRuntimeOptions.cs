namespace OilCaseX.Agent.Application;

/// <summary>Runtime settings shared by the API host and infrastructure adapters.</summary>
public sealed class AgentRuntimeOptions
{
    public string McpEndpoint { get; set; } = "http://localhost:5089/mcp";
    public string VllmBaseUrl { get; set; } = "http://192.168.19.120:1704/v1";
    public string VllmApiKey { get; set; } = "lm-studio";
    public string Model { get; set; } = "prism-ml/bonsai-27b";
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxAgentSteps { get; set; } = 6;
    public int MaxMcpCallsPerTurn { get; set; } = 4;
}
