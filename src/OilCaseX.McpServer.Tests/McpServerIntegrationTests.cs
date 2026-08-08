using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OilCaseX.McpServer.ApiClient.Generated;

namespace OilCaseX.McpServer.Tests;

public sealed class McpServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    private readonly WebApplicationFactory<Program> factory;

    public McpServerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GeneratedTool_InvokesApiClientResolvedFromRequestServices()
    {
        var fake = new FakeOilCaseXApiClient();
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<OilCaseXApiClientGenerated>();
                services.AddScoped<OilCaseXApiClientGenerated>(_ => fake);
            });
        });
        using var generatedClient = customFactory.CreateClient();

        var initialize = await SendMcpAsync(
            generatedClient,
            "initialize",
            10,
            "{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}");
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);

        var response = await SendMcpAsync(
            generatedClient,
            "tools/call",
            11,
            "{\"name\":\"get_wellpad\",\"arguments\":{\"wellpadId\":42}}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, fake.GetWellpadCalls);
        Assert.Contains("42", body);
    }

    [Fact]
    public async Task LiveHealth_IsHealthy()
    {
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ToolsList_ContainsReadOnlyOilCaseXTools()
    {
        await InitializeAsync();
        var response = await SendMcpAsync("tools/list", 2, "{}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("list_wellpads", body);
        Assert.Contains("get_wellpad", body);
        Assert.Contains("get_borehole", body);
        Assert.Contains("prepare_create_borehole", body);
        Assert.Contains("\"readOnlyHint\":true", body);
    }

    private async Task InitializeAsync()
    {
        var response = await SendMcpAsync(
            "initialize",
            1,
            "{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendMcpAsync(string method, int id, string parameters)
        => await SendMcpAsync(client, method, id, parameters);

    private static async Task<HttpResponseMessage> SendMcpAsync(
        HttpClient targetClient,
        string method,
        int id,
        string parameters)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",\"params\":{parameters}}}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        return await targetClient.SendAsync(request);
    }

    private sealed class FakeOilCaseXApiClient : OilCaseXApiClientGenerated
    {
        public FakeOilCaseXApiClient() : base(new HttpClient()) { }
        public int GetWellpadCalls { get; private set; }

        public override Task<ICollection<WellpadResult2>> ListWellpadsAsync(CancellationToken cancellationToken)
            => Task.FromResult<ICollection<WellpadResult2>>([]);

        public override Task<WellpadResult2> GetWellpadAsync(int wellpadId, CancellationToken cancellationToken)
        {
            GetWellpadCalls++;
            return Task.FromResult(new WellpadResult2 { Id = wellpadId });
        }

        public override Task<BoreholeResult2> GetBoreholeAsync(int boreholeId, CancellationToken cancellationToken)
            => Task.FromResult(new BoreholeResult2 { BoreholeId = boreholeId });
    }
}
