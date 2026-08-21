namespace DndCampaign.Api.Application.Invitations;

public interface IInvitationAcceptanceService
{
    Task<InvitationPreviewOutcome> PreviewAsync(
        PreviewInvitationCommand command,
        CancellationToken cancellationToken);

    Task<AcceptInvitationResult> AcceptAsync(
        AcceptInvitationCommand command,
        AuthenticatedActor actor,
        CancellationToken cancellationToken);
}
