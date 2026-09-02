namespace DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;

public interface ICampaignAdventureContext
{
    Task<CampaignAdventureContext> ResolveAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record CampaignAdventureContext(bool Exists, bool IsDm, Guid? AdventureModuleId);
