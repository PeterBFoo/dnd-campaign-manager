namespace DndCampaign.Api.Application.Invitations;

public interface IPlatformInvitationService
{
    Task<IReadOnlyList<InvitationSummary>> ListAsync(
        ListPlatformInvitationsCommand command,
        CancellationToken cancellationToken);

    Task<InvitationSummary> IssueAsync(
        IssuePlatformInvitationCommand command,
        CancellationToken cancellationToken);

    Task<(ResendInvitationStatus Status, InvitationSummary? Summary)> ResendAsync(
        ResendInvitationCommand command,
        CancellationToken cancellationToken);

    Task<RevokeInvitationStatus> RevokeAsync(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken);
}
