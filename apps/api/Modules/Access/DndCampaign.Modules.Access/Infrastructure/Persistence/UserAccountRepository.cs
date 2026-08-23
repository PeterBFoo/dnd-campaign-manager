using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class UserAccountRepository(AccessDbContext database) :
    IUserAccountRepository,
    IEligibleUserReadStore
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        database.Users.AnyAsync(cancellationToken);

    public Task<UserAccount?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default) =>
        database.Users.SingleOrDefaultAsync(
            user => user.Email == normalizedEmail,
            cancellationToken);

    public Task<UserAccount?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        database.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<EligibleUserPage> SearchAsync(
        Guid campaignId,
        Guid actorUserId,
        string? query,
        int offset,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var pendingRecipients = database.Invitations
            .Where(invitation =>
                invitation.CampaignId == campaignId
                && invitation.Status == Domain.Invitations.InvitationStatus.Pending
                && invitation.ExpiresAt > now)
            .Select(invitation => invitation.RecipientEmail);
        var memberIds = database.CampaignMemberships
            .Where(membership => membership.CampaignId == campaignId)
            .Select(membership => membership.UserId);
        var users = database.Users
            .AsNoTracking()
            .Where(user =>
                user.Id != actorUserId
                && !memberIds.Contains(user.Id)
                && !pendingRecipients.Contains(user.Email));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{EscapeLikePattern(query.Trim())}%";
            users = users.Where(user =>
                EF.Functions.ILike(user.DisplayName, pattern, "\\")
                || EF.Functions.ILike(user.Email, pattern, "\\"));
        }

        var page = await users
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(user => new EligibleUserRecord(user.Id, user.DisplayName, user.Email))
            .ToArrayAsync(cancellationToken);
        return new EligibleUserPage(page.Take(limit).ToArray(), page.Length > limit);
    }

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    public void Add(UserAccount user) => database.Users.Add(user);
}
