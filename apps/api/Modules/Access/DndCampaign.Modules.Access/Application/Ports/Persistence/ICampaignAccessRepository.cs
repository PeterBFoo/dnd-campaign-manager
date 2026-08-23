using DndCampaign.Modules.Access.Domain.CampaignAccess;

namespace DndCampaign.Modules.Access.Application.Ports.Persistence;

internal interface ICampaignAccessRepository
{
    Task<bool> IsMemberAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> ListPlayerCampaignIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    void Add(CampaignMembership membership);
}
