using System.Diagnostics;

namespace OilCaseX.McpServer.Diagnostics;

public static class McpDiagnostics
{
    public const string ActivitySourceName = "OilCaseX.McpServer";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
