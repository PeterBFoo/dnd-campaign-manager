using System.Security.Cryptography;
using System.Text;

namespace DndCampaign.Api.Domain.Identity;

public sealed record IssuedUserSession(UserSession Session, string Token);

public sealed class UserSession
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    private UserSession()
    {
    }

    private UserSession(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt.Add(Lifetime);
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }

    public static IssuedUserSession Issue(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A session requires a user identifier.", nameof(userId));
        }

        var token = CreateToken();
        return new IssuedUserSession(
            new UserSession(Guid.NewGuid(), userId, HashToken(token), now),
            token);
    }

    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
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
