namespace OilCaseX.McpServer.ApiClient;

public sealed class UpstreamResponseTooLargeException(long maxBytes)
    : Exception($"The OilCaseX API response exceeds the configured {maxBytes} byte limit.");

public sealed class UpstreamCircuitOpenException() : Exception("The OilCaseX API circuit is temporarily open.");
