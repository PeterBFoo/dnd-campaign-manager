using DndCampaign.Modules.Campaigns.Contracts.AdventureContent;
using DndCampaign.Modules.Campaigns.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Campaigns.Infrastructure.Access;

internal sealed class CampaignAdventureContentReader(CampaignsDbContext database) : ICampaignAdventureContentReader
{
    public async Task<CampaignAdventureContent> ResolveAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        var campaign = await database.Campaigns.AsNoTracking().Where(x => x.Id == campaignId)
            .Select(x => new { x.DmUserId, x.AdventureModuleId }).SingleOrDefaultAsync(cancellationToken);
        return campaign is null
            ? new(false, false, null)
            : new(true, campaign.DmUserId == userId, campaign.AdventureModuleId);
    }
}
