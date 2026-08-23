using DndCampaign.Modules.Campaigns.Domain.Campaigns;

namespace DndCampaign.Modules.Campaigns.Application.Ports;

internal interface ICampaignRepository
{
    void Add(Campaign campaign);

    Task<Campaign?> FindAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campaign>> ListAccessibleAsync(
        Guid dmUserId,
        IReadOnlyCollection<Guid> playerCampaignIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
