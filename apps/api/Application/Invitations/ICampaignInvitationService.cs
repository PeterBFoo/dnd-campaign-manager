namespace DndCampaign.Api.Application.Invitations;

public interface ICampaignInvitationService
{
    Task<(CampaignAccessStatus Access, IReadOnlyList<InvitationSummary>? Items)> ListAsync(
        ListCampaignInvitationsCommand command,
        CancellationToken cancellationToken);

    Task<(CampaignAccessStatus Access, InvitationSummary? Summary)> IssueAsync(
        IssueCampaignInvitationCommand command,
        CancellationToken cancellationToken);

    Task<(CampaignAccessStatus Access, ResendInvitationStatus Status, InvitationSummary? Summary)> ResendAsync(
        ResendCampaignInvitationCommand command,
        CancellationToken cancellationToken);

    Task<(CampaignAccessStatus Access, RevokeInvitationStatus Status)> RevokeAsync(
        Guid campaignId,
        Guid requesterUserId,
        RevokeInvitationCommand command,
        CancellationToken cancellationToken);
}
