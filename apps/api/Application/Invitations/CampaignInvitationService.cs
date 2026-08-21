using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Application.Invitations;

/// <summary>
/// Campaign invitation administration. Temporary debt: uses <see cref="CampaignDbContext"/> directly.
/// </summary>
public sealed class CampaignInvitationService(
    CampaignDbContext database,
    InvitationTokenProtector protector,
    TimeProvider timeProvider) : ICampaignInvitationService
{
    private readonly InvitationIssuanceCore _issuance = new(database, protector, timeProvider);

    public async Task<(CampaignAccessStatus Access, IReadOnlyList<InvitationSummary>? Items)> ListAsync(
        ListCampaignInvitationsCommand command,
        CancellationToken cancellationToken)
    {
        if (!await InvitationInternalOperations.IsCampaignDmAsync(
                database,
                command.CampaignId,
                command.RequesterUserId,
                cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, null);
        }

        var items = await InvitationInternalOperations.ListInvitationsAsync(
            database,
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
        if (!await InvitationInternalOperations.IsCampaignDmAsync(
                database,
                command.CampaignId,
                command.IssuedByUserId,
                cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, null);
        }

        var summary = await _issuance.IssueAsync(
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
        if (!await InvitationInternalOperations.IsCampaignDmAsync(
                database,
                command.CampaignId,
                command.RequesterUserId,
                cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, ResendInvitationStatus.NotFound, null);
        }

        var invitation = await database.Invitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == command.InvitationId
                && candidate.Kind == InvitationKind.Campaign
                && candidate.CampaignId == command.CampaignId,
            cancellationToken);
        if (invitation is null)
        {
            return (CampaignAccessStatus.Allowed, ResendInvitationStatus.NotFound, null);
        }

        var summary = await _issuance.ResendAsync(invitation, cancellationToken);
        return (CampaignAccessStatus.Allowed, ResendInvitationStatus.Resent, summary);
    }

    public async Task<(CampaignAccessStatus Access, RevokeInvitationStatus Status)> RevokeAsync(
        Guid campaignId,
        Guid requesterUserId,
        RevokeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (!await InvitationInternalOperations.IsCampaignDmAsync(
                database,
                campaignId,
                requesterUserId,
                cancellationToken))
        {
            return (CampaignAccessStatus.Forbidden, RevokeInvitationStatus.NotFound);
        }

        var status = await InvitationInternalOperations.RevokeInvitationAsync(
            database,
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
