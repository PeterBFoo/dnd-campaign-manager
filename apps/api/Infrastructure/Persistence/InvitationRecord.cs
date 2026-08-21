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
        FromDomain(issued.Invitation, issuedByUserId);

    internal static InvitationRecord FromDomain(Invitation invitation, Guid? issuedByUserId = null)
    {
        var record = new InvitationRecord(
            invitation.Id,
            invitation.Kind,
            invitation.RecipientEmail,
            invitation.CampaignId,
            Convert.ToHexString(invitation.TokenHash.Span),
            issuedByUserId ?? invitation.IssuedByUserId,
            invitation.IssuedAt,
            invitation.ExpiresAt);
        record.ApplyBusinessState(invitation);
        return record;
    }

    internal void ApplyBusinessState(Invitation invitation)
    {
        Status = invitation.Status;
        AcceptedByUserId = invitation.AcceptedByUserId;
        AcceptedAt = invitation.AcceptedAt;
        RevokedAt = invitation.RevokedAt;
    }

    public void MarkSent(DateTimeOffset now)
    {
        LastSentAt = now;
        SendCount++;
    }
}
