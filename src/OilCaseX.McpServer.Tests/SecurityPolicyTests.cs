using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OilCaseX.McpServer.Configuration;
using OilCaseX.McpServer.Mcp;
using OilCaseX.McpServer.Mcp.Dtos;

namespace OilCaseX.McpServer.Tests;

public sealed class SecurityPolicyTests
{
    [Fact]
    public void WriteToolRequiresConfiguredRole()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var policy = new ToolAuthorizationPolicy(accessor, Options.Create(new McpServerOptions()));

        Assert.False(policy.CanInvoke("execute_create_borehole", readOnly: false, destructive: true));

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "OilCaseX.Writer")],
            authenticationType: "test"));

        Assert.True(policy.CanInvoke("execute_create_borehole", readOnly: false, destructive: true));
    }

    [Fact]
    public void ConfirmationCanBeConsumedOnlyOnce()
    {
        var store = new InMemoryConfirmationStore(Options.Create(new McpServerOptions { ConfirmationTtlSeconds = 300 }));
        var confirmation = store.Create(
            "jwt:one",
            "wellpad:1",
            BoreholePayloadCanonicalizer.ToolName,
            "{\"orderId\":2,\"wellpadId\":1}",
            "hash",
            new BoreholePurchasePreview(1, 2, 3, 1, 2, 3, "v1"));

        Assert.True(store.TryConsume(confirmation.ConfirmationId, out var consumed));
        Assert.Equal(confirmation.ConfirmationId, consumed?.ConfirmationId);
        Assert.False(store.TryConsume(confirmation.ConfirmationId, out _));
    }

    [Fact]
    public void McpAssemblyDoesNotReferenceProductDatabaseProjects()
    {
        var references = typeof(Program).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("OilCaseX.Domain", references);
        Assert.DoesNotContain("OilCaseX.Domain.Services", references);
    }

    [Fact]
    public void OilCaseXBaseUrlRejectsQueryAndCredentials()
    {
        var validator = new McpServerOptionsValidator();
        var options = new McpServerOptions { OilCaseXBaseUrl = "https://user:password@example.test/?next=internal" };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }
}
