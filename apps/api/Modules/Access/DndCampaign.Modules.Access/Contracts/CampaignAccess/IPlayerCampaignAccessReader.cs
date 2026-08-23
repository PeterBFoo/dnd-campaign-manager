namespace DndCampaign.Modules.Access.Contracts.CampaignAccess;

public interface IPlayerCampaignAccessReader
{
    Task<IReadOnlyCollection<Guid>> ListCampaignIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPlayerAccessAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
