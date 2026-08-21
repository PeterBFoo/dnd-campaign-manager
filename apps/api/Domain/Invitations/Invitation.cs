using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace DndCampaign.Api.Domain.Invitations;

public enum InvitationKind
{
    Platform,
    Campaign,
}

public enum InvitationStatus
{
    Pending,
    Accepted,
    Expired,
    Revoked,
}

public enum InvitationAcceptanceResult
{
    Accepted,
    InvalidToken,
    Expired,
    AlreadyFinalized,
}

public sealed record IssuedInvitation(Invitation Invitation, string Token);

public sealed class Invitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly byte[] tokenHash;

    private Invitation(
        Guid id,
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        byte[] tokenHash,
        Guid issuedByUserId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        InvitationStatus status,
        Guid? acceptedByUserId,
        DateTimeOffset? acceptedAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        Kind = kind;
        RecipientEmail = recipientEmail;
        CampaignId = campaignId;
        this.tokenHash = tokenHash;
        IssuedByUserId = issuedByUserId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        Status = status;
        AcceptedByUserId = acceptedByUserId;
        AcceptedAt = acceptedAt;
        RevokedAt = revokedAt;
    }

    public Guid Id { get; }

    public InvitationKind Kind { get; }

    public string RecipientEmail { get; }

    public Guid? CampaignId { get; }

    public Guid IssuedByUserId { get; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public ReadOnlyMemory<byte> TokenHash => tokenHash;

    internal static Invitation Restore(
        Guid id,
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        byte[] tokenHash,
        Guid issuedByUserId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        InvitationStatus status,
        Guid? acceptedByUserId,
        DateTimeOffset? acceptedAt,
        DateTimeOffset? revokedAt) =>
        new(
            id,
            kind,
            recipientEmail,
            campaignId,
            tokenHash,
            issuedByUserId,
            issuedAt,
            expiresAt,
            status,
            acceptedByUserId,
            acceptedAt,
            revokedAt);

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

    public bool IsPending(DateTimeOffset now) =>
        Status == InvitationStatus.Pending && now < ExpiresAt;

    public InvitationAcceptanceResult Accept(string token, Guid acceptedByUserId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (acceptedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Acceptance requires a user identifier.", nameof(acceptedByUserId));
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
        AcceptedByUserId = acceptedByUserId;
        AcceptedAt = now;
        return InvitationAcceptanceResult.Accepted;
    }

    public bool Revoke(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return false;
        }

        if (now >= ExpiresAt)
        {
            Status = InvitationStatus.Expired;
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

    public bool MatchesToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var candidateHash = HashToken(token);
        return CryptographicOperations.FixedTimeEquals(tokenHash, candidateHash);
    }

    public static byte[] HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
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
            throw new ArgumentException("An invitation requires an issuer identifier.", nameof(issuedByUserId));
        }

        var normalizedEmail = NormalizeEmail(recipientEmail);
        var token = CreateToken();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var invitation = new Invitation(
            Guid.NewGuid(),
            kind,
            normalizedEmail,
            campaignId,
            hash,
            issuedByUserId,
            issuedAt,
            issuedAt.Add(Lifetime),
            InvitationStatus.Pending,
            acceptedByUserId: null,
            acceptedAt: null,
            revokedAt: null);

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
