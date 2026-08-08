using OilCaseX.Agent.Api.Configuration;
using Xunit;

namespace OilCaseX.Agent.UnitTests;

public sealed class AgentServiceOptionsTests
{
    [Fact]
    public void DefaultsDefineSafeAgentLimits()
    {
        var options = new AgentServiceOptions();

        Assert.Equal(6, options.MaxAgentSteps);
        Assert.Equal(4, options.MaxMcpCallsPerTurn);
        Assert.InRange(options.RequestTimeoutSeconds, 1, 120);
    }
}
