using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Campaigns.Infrastructure.Access;

internal sealed class CampaignAccessReader(
    CampaignsDbContext database,
    IPlayerCampaignAccessReader playerAccess) : ICampaignAccessReader
{
    public async Task<CampaignAccess> GetAccessAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var dmUserId = await database.Campaigns
            .AsNoTracking()
            .Where(campaign => campaign.Id == campaignId)
            .Select(campaign => (Guid?)campaign.DmUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (dmUserId is null)
        {
            return new CampaignAccess(Exists: false, Role: null);
        }

        if (dmUserId == userId)
        {
            return new CampaignAccess(Exists: true, CampaignRole.Dm);
        }

        var isPlayer = await playerAccess.HasPlayerAccessAsync(campaignId, userId, cancellationToken);
        return new CampaignAccess(
            Exists: true,
            Role: isPlayer ? CampaignRole.Player : null);
    }
}
