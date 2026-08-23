using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Access.Domain.CampaignAccess;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class CampaignAccessRepository(AccessDbContext database) :
    ICampaignAccessRepository,
    IPlayerCampaignAccessReader,
    ICampaignPlayerReader
{
    public Task<bool> IsMemberAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        database.CampaignMemberships.AnyAsync(membership =>
            membership.CampaignId == campaignId && membership.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> ListPlayerCampaignIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await database.CampaignMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == userId
                && membership.Role == CampaignRole.Player)
            .Select(membership => membership.CampaignId)
            .ToArrayAsync(cancellationToken);

    public Task<IReadOnlyCollection<Guid>> ListCampaignIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ListPlayerCampaignIdsAsync(userId, cancellationToken);

    public Task<bool> HasPlayerAccessAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        database.CampaignMemberships.AsNoTracking().AnyAsync(membership =>
            membership.CampaignId == campaignId
            && membership.UserId == userId
            && membership.Role == CampaignRole.Player,
            cancellationToken);

    public async Task<IReadOnlyList<CampaignPlayer>> ListPlayersAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default) =>
        await (from membership in database.CampaignMemberships.AsNoTracking()
               join user in database.Users.AsNoTracking() on membership.UserId equals user.Id
               where membership.CampaignId == campaignId && membership.Role == CampaignRole.Player
               orderby user.DisplayName, user.Id
               select new CampaignPlayer(user.Id, user.DisplayName))
            .ToArrayAsync(cancellationToken);

    public void Add(CampaignMembership membership) => database.CampaignMemberships.Add(membership);
}
