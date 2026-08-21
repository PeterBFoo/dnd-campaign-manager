using System.Security.Claims;
using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;

namespace DndCampaign.Api.Api;

internal static class IdentityHttpMapping
{
    internal static BootstrapAccountCommand ToCommand(BootstrapRequest request) =>
        new(request.Token, request.Email, request.DisplayName, request.Password);

    internal static LoginCommand ToCommand(LoginRequest request) =>
        new(request.Email, request.Password);

    internal static LogoutCommand ToCommand(ClaimsPrincipal principal) =>
        new(principal.GetSessionId());

    internal static UserResponse ToResponse(UserProfile profile) =>
        new(profile.Id, profile.Email, profile.DisplayName, profile.IsPlatformAdmin);

    internal static SessionResponse ToSessionResponse(LoginOutcome outcome) =>
        new(outcome.AccessToken, outcome.ExpiresAt, ToResponse(outcome.User));

    internal static UserResponse ToResponse(ClaimsPrincipal principal) =>
        new(
            principal.GetUserId(),
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            principal.Identity?.Name ?? string.Empty,
            principal.HasClaim("platform_admin", "true"));

    internal static BootstrapStatusResponse ToResponse(BootstrapStatus status) =>
        new(status == BootstrapStatus.Completed ? "completed" : "required");
}
