using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Campaigns.Infrastructure.Access;

internal sealed class CampaignInvitationContext(CampaignsDbContext database) : ICampaignInvitationContext
{
    public async Task<CampaignInvitationAccess> GetAccessAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await database.Campaigns
            .AsNoTracking()
            .Where(item => item.Id == campaignId)
            .Select(item => new { item.DmUserId })
            .SingleOrDefaultAsync(cancellationToken);
        return campaign is null
            ? new CampaignInvitationAccess(Exists: false, IsDm: false)
            : new CampaignInvitationAccess(Exists: true, IsDm: campaign.DmUserId == userId);
    }
}
