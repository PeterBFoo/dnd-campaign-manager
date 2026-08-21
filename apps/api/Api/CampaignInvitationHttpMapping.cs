using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Invitations;

namespace DndCampaign.Api.Api;

internal static class CampaignInvitationHttpMapping
{
    internal static ListCampaignInvitationsCommand ToListCommand(Guid campaignId, Guid requesterUserId) =>
        new(campaignId, requesterUserId);

    internal static IssueCampaignInvitationCommand ToIssueCommand(
        Guid campaignId,
        IssueInvitationRequest request,
        Guid issuedByUserId) =>
        new(request.Email ?? string.Empty, campaignId, issuedByUserId);

    internal static ResendCampaignInvitationCommand ToResendCommand(
        Guid campaignId,
        Guid requesterUserId,
        Guid invitationId) =>
        new(campaignId, requesterUserId, invitationId);

    internal static InvitationResponse ToResponse(InvitationSummary summary) =>
        PlatformInvitationHttpMapping.ToResponse(summary);

    internal static IReadOnlyList<InvitationResponse> ToResponseList(IReadOnlyList<InvitationSummary> summaries) =>
        PlatformInvitationHttpMapping.ToResponseList(summaries);
}
