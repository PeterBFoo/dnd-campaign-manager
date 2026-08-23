using DndCampaign.Modules.Missions.Application.Ports;
using DndCampaign.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionRepository(MissionsDbContext database) : IMissionRepository
{
    public async Task<IReadOnlyList<Mission>> ListAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default) =>
        await database.Missions
            .AsNoTracking()
            .Where(mission => mission.CampaignId == campaignId)
            .OrderBy(mission => mission.IsMain ? 0 : mission.Status == MissionStatus.Active ? 1 : 2)
            .ThenByDescending(mission => mission.Status == MissionStatus.Active
                ? mission.CreatedAt
                : mission.UpdatedAt ?? mission.CreatedAt)
            .ThenByDescending(mission => mission.SortSequence)
            .ToArrayAsync(cancellationToken);

    public Task<Mission?> FindForUpdateAsync(
        Guid campaignId,
        Guid missionId,
        CancellationToken cancellationToken = default) =>
        database.Missions.SingleOrDefaultAsync(
            mission => mission.CampaignId == campaignId && mission.Id == missionId,
            cancellationToken);

    public void Add(Mission mission) => database.Missions.Add(mission);

    public void Delete(Mission mission) => database.Missions.Remove(mission);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        database.SaveChangesAsync(cancellationToken);

    public async Task SaveAsMainAsync(
        Guid campaignId,
        Mission mission,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({campaignId.ToString()}, 0));",
            cancellationToken);
        await database.Missions
            .Where(candidate => candidate.CampaignId == campaignId
                && candidate.IsMain
                && candidate.Id != mission.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.IsMain, false),
                cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
