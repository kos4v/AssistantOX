using System.ComponentModel.DataAnnotations;

namespace OilCaseX.Agent.Ui;

public sealed class AgentUiOptions
{
    public const string SectionName = "AgentUi";

    [Required, Url]
    public string HubUrl { get; set; } = "https://localhost:7080/hubs/agent";

    public string? AccessToken { get; set; }
}
