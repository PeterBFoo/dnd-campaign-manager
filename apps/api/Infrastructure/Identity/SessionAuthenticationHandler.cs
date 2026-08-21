using System.Security.Claims;
using System.Text.Encodings.Web;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DndCampaign.Api.Infrastructure.Identity;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IIdentityStore identity,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "UserSession";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (token.Length is < 32 or > 128)
        {
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        var tokenHash = UserSession.HashToken(token);
        var now = timeProvider.GetUtcNow();
        var result = await identity.FindActiveByTokenHashAsync(tokenHash, now, Context.RequestAborted);

        if (result is null)
        {
            return AuthenticateResult.Fail("Invalid or expired bearer token.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
            new(ClaimTypes.Name, result.User.DisplayName),
            new(ClaimTypes.Email, result.User.Email),
            new("session_id", result.Session.Id.ToString()),
        };
        if (result.User.IsPlatformAdmin)
        {
            claims.Add(new Claim("platform_admin", "true"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme));
    }
}
