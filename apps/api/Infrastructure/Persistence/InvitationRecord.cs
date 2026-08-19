using DndCampaign.Api.Domain.Invitations;

namespace DndCampaign.Api.Infrastructure.Persistence;

public sealed class InvitationRecord
{
    private InvitationRecord()
    {
    }

    private InvitationRecord(
        Guid id,
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        string tokenHash,
        Guid issuedByUserId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        Kind = kind;
        RecipientEmail = recipientEmail;
        CampaignId = campaignId;
        TokenHash = tokenHash;
        IssuedByUserId = issuedByUserId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public InvitationKind Kind { get; private set; }

    public string RecipientEmail { get; private set; } = string.Empty;

    public Guid? CampaignId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid IssuedByUserId { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastSentAt { get; private set; }

    public int SendCount { get; private set; }

    public static InvitationRecord FromIssued(IssuedInvitation issued, Guid issuedByUserId) =>
        new(
            issued.Invitation.Id,
            issued.Invitation.Kind,
            issued.Invitation.RecipientEmail,
            issued.Invitation.CampaignId,
            Convert.ToHexString(issued.Invitation.TokenHash.Span),
            issuedByUserId,
            issued.Invitation.IssuedAt,
            issued.Invitation.ExpiresAt);

    public bool IsPending(DateTimeOffset now) => Status == InvitationStatus.Pending && now < ExpiresAt;

    public void MarkAccepted(Guid userId, DateTimeOffset now)
    {
        if (!IsPending(now))
        {
            throw new InvalidOperationException("Only a pending invitation can be accepted.");
        }

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = now;
    }

    public bool Revoke(DateTimeOffset now)
    {
        if (!IsPending(now))
        {
            if (Status == InvitationStatus.Pending && now >= ExpiresAt)
            {
                Status = InvitationStatus.Expired;
            }

            return false;
        }

        Status = InvitationStatus.Revoked;
        RevokedAt = now;
        return true;
    }

    public void MarkExpired(DateTimeOffset now)
    {
        if (Status == InvitationStatus.Pending && now >= ExpiresAt)
        {
            Status = InvitationStatus.Expired;
        }
    }

    public void MarkSent(DateTimeOffset now)
    {
        LastSentAt = now;
        SendCount++;
    }
}
