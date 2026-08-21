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
            ToDeliveryStatus(summary.DeliveryStatus),
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

    internal static string ToDeliveryStatus(InvitationDeliveryStatus status) =>
        status switch
        {
            InvitationDeliveryStatus.Pending => "pending",
            InvitationDeliveryStatus.Sent => "sent",
            InvitationDeliveryStatus.Discarded => "discarded",
            InvitationDeliveryStatus.Failed => "failed",
            _ => throw new InvalidOperationException("The invitation delivery status is not supported."),
        };
}
