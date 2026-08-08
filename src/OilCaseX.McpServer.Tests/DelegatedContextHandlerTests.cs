using System.Net;
using Microsoft.AspNetCore.Http;
using OilCaseX.McpServer.ApiClient;

namespace OilCaseX.McpServer.Tests;

public sealed class DelegatedContextHandlerTests
{
    [Fact]
    public async Task DelegatedJwtAndCorrelationAreCopiedWithoutChangingTheToken()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer test-jwt";
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-123";
        httpContext.Request.Headers["traceparent"] =
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var capture = new CaptureHandler();
        var correlationHandler = new CorrelationPropagationHandler(accessor) { InnerHandler = capture };
        var handler = new DelegatedJwtHandler(accessor) { InnerHandler = correlationHandler };
        using var client = new HttpClient(handler);

        await client.GetAsync("https://oilcasex.test/Api/V1/Purchased/Wellpad");

        Assert.NotNull(capture.Request);
        Assert.Equal("Bearer test-jwt", capture.Request!.Headers.Authorization?.ToString());
        Assert.Equal("corr-123", capture.Request.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            capture.Request.Headers.GetValues("traceparent").Single());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
