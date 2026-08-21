using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Application.Invitations;

internal static class InvitationInternalOperations
{
    internal static async Task<InvitationRecord?> FindInvitationByTokenAsync(
        CampaignDbContext database,
        string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 32 or > 128)
        {
            return null;
        }

        var tokenHash = Convert.ToHexString(Invitation.HashToken(token));
        return await database.Invitations.SingleOrDefaultAsync(
            invitation => invitation.TokenHash == tokenHash,
            cancellationToken);
    }

    internal static async Task<IReadOnlyList<InvitationSummary>> ListInvitationsAsync(
        CampaignDbContext database,
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitations = await database.Invitations
            .Where(invitation => invitation.Kind == kind && invitation.CampaignId == campaignId)
            .OrderByDescending(invitation => invitation.IssuedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var invitation in invitations)
        {
            invitation.MarkExpired(now);
        }

        if (database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        var invitationIds = invitations.Select(invitation => invitation.Id).ToArray();
        var outbox = await database.InvitationOutbox
            .Where(message => invitationIds.Contains(message.InvitationId))
            .ToListAsync(cancellationToken);

        return invitations
            .Select(invitation =>
            {
                var delivery = outbox
                    .Where(message => message.InvitationId == invitation.Id)
                    .OrderByDescending(message => message.CreatedAt)
                    .FirstOrDefault();
                var deliveryStatus = delivery switch
                {
                    { ProcessedAt: not null, ProviderMessageId: not "discarded" } => "sent",
                    { ProcessedAt: not null, ProviderMessageId: "discarded" } => "discarded",
                    { Attempts: >= 5 } => "failed",
                    _ => "pending",
                };
                return ToSummary(invitation, deliveryStatus);
            })
            .ToArray();
    }

    internal static async Task<RevokeInvitationStatus> RevokeInvitationAsync(
        CampaignDbContext database,
        Guid invitationId,
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await database.Invitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == invitationId
                && candidate.Kind == kind
                && candidate.CampaignId == campaignId,
            cancellationToken);
        if (invitation is null)
        {
            return RevokeInvitationStatus.NotFound;
        }

        if (!invitation.Revoke(now))
        {
            return RevokeInvitationStatus.Conflict;
        }

        await database.SaveChangesAsync(cancellationToken);
        return RevokeInvitationStatus.Revoked;
    }

    internal static async Task<bool> IsCampaignDmAsync(
        CampaignDbContext database,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await database.CampaignMemberships.AnyAsync(
            membership =>
                membership.CampaignId == campaignId
                && membership.UserId == userId
                && membership.Role == CampaignRole.Dm,
            cancellationToken);

    internal static InvitationSummary ToSummary(InvitationRecord invitation, string deliveryStatus) =>
        new(
            invitation.Id,
            invitation.Kind.ToString().ToLowerInvariant(),
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.Status.ToString().ToLowerInvariant(),
            deliveryStatus,
            invitation.IssuedAt,
            invitation.ExpiresAt,
            invitation.LastSentAt);

    internal static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return "***";
        }

        var local = email[..separator];
        var visible = local[..Math.Min(2, local.Length)];
        return $"{visible}***{email[separator..]}";
    }
}
