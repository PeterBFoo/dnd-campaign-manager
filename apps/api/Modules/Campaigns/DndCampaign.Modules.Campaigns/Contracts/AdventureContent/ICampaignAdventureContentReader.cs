namespace DndCampaign.Modules.Campaigns.Contracts.AdventureContent;

public interface ICampaignAdventureContentReader
{
    Task<CampaignAdventureContent> ResolveAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record CampaignAdventureContent(bool Exists, bool IsDm, Guid? AdventureModuleId);
