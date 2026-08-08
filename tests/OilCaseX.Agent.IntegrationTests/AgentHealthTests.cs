using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OilCaseX.Agent.IntegrationTests;

public sealed class AgentHealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public AgentHealthTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task LiveHealthIsAnonymousAndHealthy()
    {
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }
}
