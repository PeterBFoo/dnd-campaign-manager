using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DndCampaign.Api.Infrastructure.Persistence;

public sealed class IdentityStore(CampaignDbContext database) : IIdentityStore
{
    public Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().AnyAsync(cancellationToken);

    public Task<UserAccount?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public async Task AddUserAsync(UserAccount user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        database.Users.Add(user);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, user);
    }

    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        database.UserSessions.Add(session);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, session);
    }

    public async Task PersistLoginAsync(
        Guid userId,
        string? rehashedPasswordHash,
        UserSession newSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newSession);
        UserAccount? user = null;
        if (rehashedPasswordHash is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rehashedPasswordHash);
            user = await database.Users.SingleAsync(
                candidate => candidate.Id == userId,
                cancellationToken);
            RestrictToPasswordHash(database.Entry(user), rehashedPasswordHash);
        }

        database.UserSessions.Add(newSession);
        if (user is null)
        {
            await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, newSession);
            return;
        }

        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, user, newSession);
    }

    public Task<UserSession?> FindSessionByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        database.UserSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

    public async Task SaveSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var target = AttachSession(session);
        database.Entry(target).Property(candidate => candidate.RevokedAt).IsModified = true;
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, target);
    }

    public async Task<ActiveUserSession?> FindActiveByTokenHashAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var result = await (
            from session in database.UserSessions.AsNoTracking()
            join user in database.Users.AsNoTracking() on session.UserId equals user.Id
            where session.TokenHash == tokenHash
                && session.RevokedAt == null
                && session.ExpiresAt > now
            select new { Session = session, User = user })
            .SingleOrDefaultAsync(cancellationToken);

        return result is null ? null : new ActiveUserSession(result.User, result.Session);
    }

    public Task<bool> IsCampaignDmAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken) =>
        database.CampaignMemberships.AsNoTracking().AnyAsync(
            membership =>
                membership.CampaignId == campaignId
                && membership.UserId == userId
                && membership.Role == CampaignRole.Dm,
            cancellationToken);

    public Task<bool> IsCampaignMemberAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken) =>
        database.CampaignMemberships.AsNoTracking().AnyAsync(
            membership =>
                membership.CampaignId == campaignId
                && membership.UserId == userId,
            cancellationToken);

    public async Task AddMembershipAsync(
        CampaignMembership membership,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(membership);
        database.CampaignMemberships.Add(membership);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, membership);
    }

    private UserSession AttachSession(UserSession session)
    {
        var tracked = database.UserSessions.Local.FirstOrDefault(candidate => candidate.Id == session.Id);
        if (tracked is null)
        {
            database.UserSessions.Attach(session);
            return session;
        }

        if (!ReferenceEquals(tracked, session))
        {
            database.Entry(tracked).Property(candidate => candidate.RevokedAt).CurrentValue = session.RevokedAt;
        }

        return tracked;
    }

    private static void RestrictToPasswordHash(EntityEntry<UserAccount> entry, string passwordHash)
    {
        entry.Entity.SetPasswordHash(passwordHash);
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.Name == nameof(UserAccount.PasswordHash))
            {
                property.IsModified = true;
                continue;
            }

            if (!property.IsModified)
            {
                continue;
            }

            property.CurrentValue = property.OriginalValue;
            property.IsModified = false;
        }
    }
}
