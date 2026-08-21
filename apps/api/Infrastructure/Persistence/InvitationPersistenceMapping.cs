using DndCampaign.Api.Domain.Invitations;

namespace DndCampaign.Api.Infrastructure.Persistence;

public static class InvitationPersistenceMapping
{
    public static InvitationRecord ToRecord(Invitation invitation) =>
        InvitationRecord.FromDomain(invitation);

    public static Invitation ToDomain(InvitationRecord record) =>
        Invitation.Restore(
            record.Id,
            record.Kind,
            record.RecipientEmail,
            record.CampaignId,
            Convert.FromHexString(record.TokenHash),
            record.IssuedByUserId,
            record.IssuedAt,
            record.ExpiresAt,
            record.Status,
            record.AcceptedByUserId,
            record.AcceptedAt,
            record.RevokedAt);
}
