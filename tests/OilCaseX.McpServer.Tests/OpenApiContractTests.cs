using System.Text.Json;

namespace OilCaseX.McpServer.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public void CuratedSnapshot_ContainsOnlyTheSevenMvpOperations()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "openapi",
            "oilcasex.v1.mcp.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var operationIds = document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(pathProperty => pathProperty.Value.EnumerateObject())
            .Where(method => method.Name is "get" or "post")
            .Select(method => method.Value.GetProperty("operationId").GetString())
            .ToArray();

        Assert.Equal(7, operationIds.Length);
        Assert.Contains("listWellpads", operationIds);
        Assert.Contains("getWellpad", operationIds);
        Assert.Contains("listBoreholes", operationIds);
        Assert.Contains("getBorehole", operationIds);
        Assert.Contains("getBoreholeProduction", operationIds);
        Assert.Contains("createPurchasedBorehole", operationIds);
        Assert.Contains("validatePurchasedBorehole", operationIds);
    }
}
