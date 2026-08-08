using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OilCaseX.Agent.Api.Authentication;

/// <summary>Local-only identity for the multi-project development profile.</summary>
public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "local-user"),
            new Claim(ClaimTypes.Name, "local-user"),
            new Claim("team_id", "local-team"),
            new Claim(ClaimTypes.Role, "writer")
        ],
        Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
