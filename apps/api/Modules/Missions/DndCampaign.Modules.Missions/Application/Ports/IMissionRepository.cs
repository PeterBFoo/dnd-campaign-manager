using DndCampaign.Modules.Missions.Domain.Missions;

namespace DndCampaign.Modules.Missions.Application.Ports;

internal interface IMissionRepository
{
    Task<IReadOnlyList<Mission>> ListAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<Mission?> FindForUpdateAsync(
        Guid campaignId,
        Guid missionId,
        CancellationToken cancellationToken = default);

    void Add(Mission mission);

    void Delete(Mission mission);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task SaveAsMainAsync(
        Guid campaignId,
        Mission mission,
        CancellationToken cancellationToken = default);
}
