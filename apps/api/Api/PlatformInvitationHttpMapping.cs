using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Invitations;

namespace DndCampaign.Api.Api;

internal static class PlatformInvitationHttpMapping
{
    internal static IssuePlatformInvitationCommand ToCommand(IssueInvitationRequest request, Guid issuedByUserId) =>
        new(request.Email ?? string.Empty, issuedByUserId);

    internal static InvitationResponse ToResponse(InvitationSummary summary) =>
        new(
            summary.Id,
            summary.Kind,
            summary.RecipientEmail,
            summary.CampaignId,
            summary.Status,
            summary.DeliveryStatus,
            summary.IssuedAt,
            summary.ExpiresAt,
            summary.LastSentAt);

    internal static IReadOnlyList<InvitationResponse> ToResponseList(IReadOnlyList<InvitationSummary> summaries)
    {
        var responses = new InvitationResponse[summaries.Count];
        for (var index = 0; index < summaries.Count; index++)
        {
            responses[index] = ToResponse(summaries[index]);
        }

        return responses;
    }
}
