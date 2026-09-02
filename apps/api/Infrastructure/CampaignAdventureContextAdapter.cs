using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.Campaigns.Contracts.AdventureContent;

namespace DndCampaign.Api.Infrastructure;

internal sealed class CampaignAdventureContextAdapter(ICampaignAdventureContentReader campaigns) : ICampaignAdventureContext
{
    public async Task<CampaignAdventureContext> ResolveAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await campaigns.ResolveAsync(campaignId, userId, cancellationToken);
        return new(result.Exists, result.IsDm, result.AdventureModuleId);
    }
}
