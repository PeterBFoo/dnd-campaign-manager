namespace DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;

public interface ICampaignAccessReader
{
    Task<CampaignAccess> GetAccessAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record CampaignAccess(bool Exists, CampaignRole? Role);

public enum CampaignRole
{
    Dm,
    Player,
}
