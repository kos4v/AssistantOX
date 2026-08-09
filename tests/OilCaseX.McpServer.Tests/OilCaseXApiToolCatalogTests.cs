using OilCaseX.McpServer.Mcp;

namespace OilCaseX.McpServer.Tests;

public sealed class OilCaseXApiToolCatalogTests
{
    [Fact]
    public void CreateDescriptors_UsesGeneratedMethodsAndDefaults()
    {
        var descriptors = OilCaseXApiToolCatalog.Descriptors;

        Assert.Equal(["list_wellpads", "get_wellpad", "get_borehole", "prepare_create_borehole"],
            descriptors.Select(descriptor => descriptor.ToolName).ToArray());
        Assert.All(descriptors.Where(descriptor => descriptor.Confirmation is null), descriptor => Assert.True(descriptor.ReadOnly));
        Assert.NotNull(descriptors.Single(descriptor => descriptor.ToolName == "prepare_create_borehole").Confirmation);
        Assert.All(descriptors, descriptor => Assert.Equal(typeof(ApiClient.Generated.OilCaseXApiClientGenerated), descriptor.ClientType));
        Assert.Contains("wellpadId", descriptors.Single(descriptor => descriptor.ToolName == "get_wellpad").InputSchema);
        Assert.DoesNotContain(descriptors, descriptor =>
            descriptor.ToolName.Contains("delete", StringComparison.OrdinalIgnoreCase)
            || descriptor.ToolName.Contains("reset", StringComparison.OrdinalIgnoreCase)
            || descriptor.ToolName.Contains("restore", StringComparison.OrdinalIgnoreCase)
            || descriptor.ToolName.Contains("admin", StringComparison.OrdinalIgnoreCase));
    }
}
