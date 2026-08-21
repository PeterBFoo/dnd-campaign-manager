using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;

namespace DndCampaign.Api.Application.Invitations;

public sealed class CampaignInvitationService(
    IInvitationStore invitations,
    IInvitationOutboxStore outbox,
    IIdentityStore identity,
    InvitationIssuanceCore issuance,
    TimeProvider timeProvider) : ICampaignInvitationService
{
    public async Task<(CampaignAccessStatus Access, IReadOnlyList<InvitationSummary>? Items)> ListAsync(
        ListCampaignInvitationsCommand command,
        CancellationToken cancellationToken)
    {
        if (!await identity.IsCampaignDmAsync(command.CampaignId, command.RequesterUserId, cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, null);
        }

        var items = await InvitationInternalOperations.ListInvitationsAsync(
            invitations,
            outbox,
            InvitationKind.Campaign,
            command.CampaignId,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return (CampaignAccessStatus.Allowed, items);
    }

    public async Task<(CampaignAccessStatus Access, InvitationSummary? Summary)> IssueAsync(
        IssueCampaignInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (!await identity.IsCampaignDmAsync(command.CampaignId, command.IssuedByUserId, cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, null);
        }

        var summary = await issuance.IssueAsync(
            InvitationKind.Campaign,
            command.Email,
            command.CampaignId,
            command.IssuedByUserId,
            cancellationToken);
        return (CampaignAccessStatus.Allowed, summary);
    }

    public async Task<(CampaignAccessStatus Access, ResendInvitationStatus Status, InvitationSummary? Summary)> ResendAsync(
        ResendCampaignInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (!await identity.IsCampaignDmAsync(command.CampaignId, command.RequesterUserId, cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, ResendInvitationStatus.NotFound, null);
        }

        var invitation = await invitations.FindByIdAsync(
            command.InvitationId,
            InvitationKind.Campaign,
            command.CampaignId,
            cancellationToken);
        if (invitation is null)
        {
            return (CampaignAccessStatus.Allowed, ResendInvitationStatus.NotFound, null);
        }

        var summary = await issuance.ResendAsync(invitation, cancellationToken);
        return (CampaignAccessStatus.Allowed, ResendInvitationStatus.Resent, summary);
    }

    public async Task<(CampaignAccessStatus Access, RevokeInvitationStatus Status)> RevokeAsync(
        Guid campaignId,
        Guid requesterUserId,
        RevokeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (!await identity.IsCampaignDmAsync(campaignId, requesterUserId, cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, RevokeInvitationStatus.NotFound);
        }

        var status = await InvitationInternalOperations.RevokeInvitationAsync(
            invitations,
            command.InvitationId,
            InvitationKind.Campaign,
            campaignId,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (status == RevokeInvitationStatus.Revoked)
        {
            IdentityTelemetry.InvitationsRevoked.Add(
                1,
                new KeyValuePair<string, object?>("invitation.kind", "campaign"));
        }

        return (CampaignAccessStatus.Allowed, status);
    }
}
