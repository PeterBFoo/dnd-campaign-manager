using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Application.Invitations;

/// <summary>
/// Platform invitation administration. Temporary debt: uses <see cref="CampaignDbContext"/> directly.
/// </summary>
public sealed class PlatformInvitationService(
    CampaignDbContext database,
    InvitationTokenProtector protector,
    TimeProvider timeProvider) : IPlatformInvitationService
{
    private readonly InvitationIssuanceCore _issuance = new(database, protector, timeProvider);

    public Task<IReadOnlyList<InvitationSummary>> ListAsync(
        ListPlatformInvitationsCommand command,
        CancellationToken cancellationToken) =>
        InvitationInternalOperations.ListInvitationsAsync(
            database,
            InvitationKind.Platform,
            campaignId: null,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<InvitationSummary> IssueAsync(
        IssuePlatformInvitationCommand command,
        CancellationToken cancellationToken) =>
        _issuance.IssueAsync(
            InvitationKind.Platform,
            command.Email,
            campaignId: null,
            command.IssuedByUserId,
            cancellationToken);

    public async Task<(ResendInvitationStatus Status, InvitationSummary? Summary)> ResendAsync(
        ResendInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var invitation = await database.Invitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == command.InvitationId
                && candidate.Kind == InvitationKind.Platform,
            cancellationToken);
        if (invitation is null)
        {
            return (ResendInvitationStatus.NotFound, null);
        }

        var summary = await _issuance.ResendAsync(invitation, cancellationToken);
        return (ResendInvitationStatus.Resent, summary);
    }

    public async Task<RevokeInvitationStatus> RevokeAsync(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var status = await InvitationInternalOperations.RevokeInvitationAsync(
            database,
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
