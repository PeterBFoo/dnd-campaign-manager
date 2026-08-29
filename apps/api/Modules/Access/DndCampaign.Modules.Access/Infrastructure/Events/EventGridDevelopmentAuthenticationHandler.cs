using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DndCampaign.Modules.Access.Infrastructure.Events;

internal sealed class EventGridDevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly bool enabledForDevelopment = environment.IsDevelopment();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!enabledForDevelopment
            || !Context.Request.Headers.TryGetValue("X-Event-Grid-Test", out var value)
            || value != "1")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim("roles", "AzureEventGridSecureWebhookSubscriber") },
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
