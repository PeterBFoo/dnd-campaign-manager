using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace DndCampaign.Modules.Access.Domain.Invitations;

internal enum InvitationKind
{
    Platform,
    Campaign,
}

internal enum InvitationStatus
{
    Pending,
    Accepted,
    Expired,
    Revoked,
}

internal enum InvitationAcceptanceResult
{
    Accepted,
    InvalidToken,
    Expired,
    AlreadyFinalized,
}

internal sealed record IssuedInvitation(Invitation Invitation, string Token);

internal sealed class Invitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
    }

    private Invitation(
        Guid id,
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        string tokenHash,
        Guid issuedByUserId,
        DateTimeOffset issuedAt)
    {
        Id = id;
        Kind = kind;
        RecipientEmail = recipientEmail;
        CampaignId = campaignId;
        TokenHash = tokenHash;
        IssuedByUserId = issuedByUserId;
        IssuedAt = issuedAt;
        ExpiresAt = issuedAt.Add(Lifetime);
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

    public static IssuedInvitation IssuePlatform(
        string recipientEmail,
        Guid issuedByUserId,
        DateTimeOffset issuedAt) =>
        Issue(InvitationKind.Platform, recipientEmail, campaignId: null, issuedByUserId, issuedAt);

    public static IssuedInvitation IssueCampaign(
        string recipientEmail,
        Guid campaignId,
        Guid issuedByUserId,
        DateTimeOffset issuedAt)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("A campaign invitation requires a campaign identifier.", nameof(campaignId));
        }

        return Issue(InvitationKind.Campaign, recipientEmail, campaignId, issuedByUserId, issuedAt);
    }

    public bool IsPending(DateTimeOffset now) => Status == InvitationStatus.Pending && now < ExpiresAt;

    public InvitationAcceptanceResult Accept(string token, Guid userId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An accepted invitation requires a user identifier.", nameof(userId));
        }

        if (Status != InvitationStatus.Pending)
        {
            return InvitationAcceptanceResult.AlreadyFinalized;
        }

        if (now >= ExpiresAt)
        {
            Status = InvitationStatus.Expired;
            return InvitationAcceptanceResult.Expired;
        }

        if (!MatchesToken(token))
        {
            return InvitationAcceptanceResult.InvalidToken;
        }

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = now;
        return InvitationAcceptanceResult.Accepted;
    }

    public bool Revoke(DateTimeOffset now)
    {
        if (!IsPending(now))
        {
            Expire(now);
            return false;
        }

        Status = InvitationStatus.Revoked;
        RevokedAt = now;
        return true;
    }

    public bool Expire(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending || now < ExpiresAt)
        {
            return false;
        }

        Status = InvitationStatus.Expired;
        return true;
    }

    public void MarkSent(DateTimeOffset now)
    {
        LastSentAt = now;
        SendCount++;
    }

    public bool MatchesToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(TokenHash),
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static IssuedInvitation Issue(
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        Guid issuedByUserId,
        DateTimeOffset issuedAt)
    {
        if (issuedByUserId == Guid.Empty)
        {
            throw new ArgumentException("An invitation requires an issuer.", nameof(issuedByUserId));
        }

        var normalizedEmail = NormalizeEmail(recipientEmail);
        var token = CreateToken();
        var invitation = new Invitation(
            Guid.NewGuid(),
            kind,
            normalizedEmail,
            campaignId,
            HashToken(token),
            issuedByUserId,
            issuedAt);

        return new IssuedInvitation(invitation, token);
    }

    private static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        try
        {
            return new MailAddress(email.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The recipient email is not valid.", nameof(email), exception);
        }
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
