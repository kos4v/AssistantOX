namespace OilCaseX.Agent.ContractTests;

using Xunit;

public sealed class AgentContractBaselineTests
{
    [Fact]
    public void BaselineDocumentsArePresent()
    {
        var root = FindRepositoryRoot();
        Assert.True(File.Exists(Path.Combine(root, "docs", "OilCaseX.AgentService", "mcp-contract-baseline.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "OilCaseX.AgentService", "error-policy.md")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "AssistantOX.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("AssistantOX repository root was not found.");
    }
}
