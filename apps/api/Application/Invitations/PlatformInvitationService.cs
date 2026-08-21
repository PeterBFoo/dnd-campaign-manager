using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;

namespace DndCampaign.Api.Application.Invitations;

public sealed class PlatformInvitationService(
    IInvitationStore invitations,
    IInvitationOutboxStore outbox,
    InvitationIssuanceCore issuance,
    TimeProvider timeProvider) : IPlatformInvitationService
{
    public Task<IReadOnlyList<InvitationSummary>> ListAsync(
        ListPlatformInvitationsCommand command,
        CancellationToken cancellationToken) =>
        InvitationInternalOperations.ListInvitationsAsync(
            invitations,
            outbox,
            InvitationKind.Platform,
            campaignId: null,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<InvitationSummary> IssueAsync(
        IssuePlatformInvitationCommand command,
        CancellationToken cancellationToken) =>
        issuance.IssueAsync(
            InvitationKind.Platform,
            command.Email,
            campaignId: null,
            command.IssuedByUserId,
            cancellationToken);

    public async Task<(ResendInvitationStatus Status, InvitationSummary? Summary)> ResendAsync(
        ResendInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindByIdAsync(
            command.InvitationId,
            InvitationKind.Platform,
            campaignId: null,
            cancellationToken);
        if (invitation is null)
        {
            return (ResendInvitationStatus.NotFound, null);
        }

        var summary = await issuance.ResendAsync(invitation, cancellationToken);
        return (ResendInvitationStatus.Resent, summary);
    }

    public async Task<RevokeInvitationStatus> RevokeAsync(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var status = await InvitationInternalOperations.RevokeInvitationAsync(
            invitations,
            command.InvitationId,
            InvitationKind.Platform,
            campaignId: null,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (status == RevokeInvitationStatus.Revoked)
        {
            IdentityTelemetry.InvitationsRevoked.Add(
                1,
                new KeyValuePair<string, object?>("invitation.kind", "platform"));
        }

        return status;
    }
}
