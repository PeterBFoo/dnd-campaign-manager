using DndCampaign.Api.Domain.Identity;

namespace DndCampaign.Api.Application.Identity;

public sealed record ActiveUserSession(UserAccount User, UserSession Session);

public interface IIdentityStore
{
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken);

    Task<UserAccount?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task AddUserAsync(UserAccount user, CancellationToken cancellationToken);

    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task PersistLoginAsync(
        Guid userId,
        string? rehashedPasswordHash,
        UserSession newSession,
        CancellationToken cancellationToken);

    Task<UserSession?> FindSessionByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task SaveSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task<ActiveUserSession?> FindActiveByTokenHashAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<bool> IsCampaignDmAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsCampaignMemberAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken);

    Task AddMembershipAsync(
        CampaignMembership membership,
        CancellationToken cancellationToken);
}
