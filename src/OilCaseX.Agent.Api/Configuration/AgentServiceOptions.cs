using System.ComponentModel.DataAnnotations;

namespace OilCaseX.Agent.Api.Configuration;

public sealed class AgentServiceOptions
{
    public const string SectionName = "AgentService";

    [Required, Url]
    public string McpEndpoint { get; set; } = "http://localhost:5089/mcp";

    [Required, Url]
    public string VllmBaseUrl { get; set; } = "http://192.168.19.120:1704/v1";

    [Required]
    public string VllmApiKey { get; set; } = "lm-studio";

    [Required]
    public string Model { get; set; } = "prism-ml/bonsai-27b";

    [Range(1, 120)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    [Range(1, 20)]
    public int MaxAgentSteps { get; set; } = 6;

    [Range(1, 20)]
    public int MaxMcpCallsPerTurn { get; set; } = 4;

    [Range(1_024, 16_777_216)]
    public long MaxRequestBodyBytes { get; set; } = 1_048_576;
}
