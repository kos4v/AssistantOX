using OilCaseX.McpServer.ApiClient;
using OilCaseX.McpServer.ApiClient.Generated;

namespace OilCaseX.McpServer.Tests;

public sealed class OilCaseXErrorMapperTests
{
    [Theory]
    [InlineData(401, "unauthorized", false)]
    [InlineData(403, "forbidden", false)]
    [InlineData(404, "not_found", false)]
    [InlineData(429, "rate_limited", true)]
    [InlineData(503, "upstream_unavailable", true)]
    public void ApiError_IsMappedToStableToolError(int statusCode, string code, bool retryable)
    {
        var exception = new ApiException(
            "upstream details are intentionally not exposed",
            statusCode,
            "secret response body",
            new Dictionary<string, IEnumerable<string>>(),
            new InvalidOperationException());

        var result = OilCaseXErrorMapper.Map(exception);

        Assert.Equal(code, result.Code);
        Assert.Equal(retryable, result.Retryable);
        Assert.DoesNotContain("secret", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Timeout_IsMappedToUpstreamUnavailable()
    {
        var result = OilCaseXErrorMapper.Map(new TimeoutException());

        Assert.Equal("upstream_unavailable", result.Code);
        Assert.True(result.Retryable);
    }
}
