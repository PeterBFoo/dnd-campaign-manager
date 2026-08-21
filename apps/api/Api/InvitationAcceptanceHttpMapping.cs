using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using System.Security.Claims;

namespace DndCampaign.Api.Api;

internal static class InvitationAcceptanceHttpMapping
{
    internal static PreviewInvitationCommand ToCommand(InvitationTokenRequest request) =>
        new(request.Token);

    internal static AcceptInvitationCommand ToCommand(AcceptInvitationRequest request) =>
        new(request.Token, request.DisplayName, request.Password);

    internal static AuthenticatedActor ToActor(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new AuthenticatedActor(false, null);
        }

        return new AuthenticatedActor(true, principal.GetUserId());
    }

    internal static InvitationPreviewResponse ToResponse(InvitationPreviewOutcome outcome) =>
        new(
            outcome.State,
            outcome.Kind,
            outcome.RecipientEmail,
            outcome.ExpiresAt,
            outcome.RequiresAuthentication);

    internal static InvitationAcceptanceResponse ToResponse(InvitationAcceptanceOutcome outcome) =>
        new(
            IdentityHttpMapping.ToResponse(outcome.User),
            outcome.AccessToken,
            outcome.ExpiresAt,
            outcome.Kind);
}
