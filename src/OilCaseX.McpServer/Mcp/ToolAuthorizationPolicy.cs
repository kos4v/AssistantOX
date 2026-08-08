using System.Security.Claims;
using Microsoft.Extensions.Options;
using OilCaseX.McpServer.Configuration;

namespace OilCaseX.McpServer.Mcp;

public interface IToolAuthorizationPolicy
{
    bool CanInvoke(ApiToolDescriptor descriptor);

    bool CanInvoke(string toolName, bool readOnly, bool destructive);
}

public sealed class ToolAuthorizationPolicy(
    IHttpContextAccessor httpContextAccessor,
    IOptions<McpServerOptions> options) : IToolAuthorizationPolicy
{
    public bool CanInvoke(ApiToolDescriptor descriptor)
        => CanInvoke(descriptor.ToolName, descriptor.ReadOnly, descriptor.Destructive);

    public bool CanInvoke(string toolName, bool readOnly, bool destructive)
    {
        if (readOnly && !destructive)
        {
            return true;
        }

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var configuredRoles = options.Value.WriteRoles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return user.Claims
            .Where(IsRoleClaim)
            .Select(claim => claim.Value)
            .Any(configuredRoles.Contains);
    }

    private static bool IsRoleClaim(Claim claim)
        => claim.Type == ClaimTypes.Role
            || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase);
}
