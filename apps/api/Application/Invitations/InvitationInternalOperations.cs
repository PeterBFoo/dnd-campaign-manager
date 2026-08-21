using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;

namespace DndCampaign.Api.Application.Invitations;

internal static class InvitationInternalOperations
{
    internal static async Task<Invitation?> FindInvitationByTokenAsync(
        IInvitationStore invitations,
        string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 32 or > 128)
        {
            return null;
        }

        var tokenHash = Convert.ToHexString(Invitation.HashToken(token));
        return await invitations.FindByTokenHashAsync(tokenHash, cancellationToken);
    }

    internal static async Task<IReadOnlyList<InvitationSummary>> ListInvitationsAsync(
        IInvitationStore invitations,
        IInvitationOutboxStore outbox,
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var items = await invitations.ListAsync(kind, campaignId, cancellationToken);
        var expired = items
            .Select(item => item.Invitation)
            .Where(invitation => invitation.Expire(now))
            .ToArray();
        if (expired.Length > 0)
        {
            await invitations.SaveAllAsync(expired, cancellationToken);
        }

        var invitationIds = items.Select(item => item.Invitation.Id).ToArray();
        var deliveryStatuses = await outbox.GetDeliveryStatusesAsync(invitationIds, cancellationToken);

        return items
            .Select(item => ToSummary(
                item.Invitation,
                item.LastSentAt,
                deliveryStatuses.GetValueOrDefault(item.Invitation.Id, InvitationDeliveryStatus.Pending)))
            .ToArray();
    }

    internal static async Task<RevokeInvitationStatus> RevokeInvitationAsync(
        IInvitationStore invitations,
        Guid invitationId,
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindByIdAsync(invitationId, kind, campaignId, cancellationToken);
        if (invitation is null)
        {
            return RevokeInvitationStatus.NotFound;
        }

        if (!invitation.Revoke(now))
        {
            return RevokeInvitationStatus.Conflict;
        }

        await invitations.SaveAsync(invitation, cancellationToken);
        return RevokeInvitationStatus.Revoked;
    }

    internal static InvitationSummary ToSummary(
        Invitation invitation,
        DateTimeOffset? lastSentAt,
        InvitationDeliveryStatus deliveryStatus) =>
        new(
            invitation.Id,
            invitation.Kind.ToString().ToLowerInvariant(),
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.Status.ToString().ToLowerInvariant(),
            deliveryStatus,
            invitation.IssuedAt,
            invitation.ExpiresAt,
            lastSentAt);

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
