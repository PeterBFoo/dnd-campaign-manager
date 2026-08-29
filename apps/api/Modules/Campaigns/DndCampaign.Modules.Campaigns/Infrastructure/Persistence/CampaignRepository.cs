using DndCampaign.Modules.Campaigns.Application.Ports;
using DndCampaign.Modules.Campaigns.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Campaigns.Infrastructure.Persistence;

internal sealed class CampaignRepository(CampaignsDbContext database) : ICampaignRepository
{
    public void Add(Campaign campaign) => database.Campaigns.Add(campaign);

    public Task<Campaign?> FindAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default) =>
        database.Campaigns.AsNoTracking().SingleOrDefaultAsync(
            campaign => campaign.Id == campaignId,
            cancellationToken);

    public Task<Campaign?> FindForUpdateAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default) =>
        database.Campaigns.SingleOrDefaultAsync(
            campaign => campaign.Id == campaignId,
            cancellationToken);

    public async Task<IReadOnlyList<Campaign>> ListAccessibleAsync(
        Guid dmUserId,
        IReadOnlyCollection<Guid> playerCampaignIds,
        CancellationToken cancellationToken = default) =>
        await database.Campaigns
            .AsNoTracking()
            .Where(campaign =>
                campaign.DmUserId == dmUserId
                || playerCampaignIds.Contains(campaign.Id))
            .OrderBy(campaign => campaign.Name)
            .ThenBy(campaign => campaign.Id)
            .ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        database.SaveChangesAsync(cancellationToken);
}
