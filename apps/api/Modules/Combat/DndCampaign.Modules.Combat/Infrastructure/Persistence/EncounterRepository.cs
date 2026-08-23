using DndCampaign.Modules.Combat.Application.Ports;
using DndCampaign.Modules.Combat.Domain.Encounters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DndCampaign.Modules.Combat.Infrastructure.Persistence;

internal sealed class EncounterRepository(CombatDbContext database) : IEncounterRepository
{
    public async Task<IReadOnlyList<Encounter>> ListAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default) =>
        await database.Encounters.AsNoTracking()
            .Include(encounter => encounter.Participants)
                .ThenInclude(participant => participant.EnemyMembers)
            .Where(encounter => encounter.CampaignId == campaignId)
            .OrderBy(encounter => encounter.Status == EncounterStatus.Active ? 0
                : encounter.Status == EncounterStatus.Draft ? 1 : 2)
            .ThenByDescending(encounter => encounter.CreatedAt)
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);

    public Task<Encounter?> FindAsync(
        Guid campaignId,
        Guid encounterId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        var query = database.Encounters.Include(encounter => encounter.Participants)
                .ThenInclude(participant => participant.EnemyMembers)
            .Where(encounter => encounter.CampaignId == campaignId && encounter.Id == encounterId)
            .AsSplitQuery();
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Encounter?> FindActiveAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default) =>
        database.Encounters.AsNoTracking()
            .Include(encounter => encounter.Participants)
                .ThenInclude(participant => participant.EnemyMembers)
            .Where(encounter => encounter.CampaignId == campaignId && encounter.Status == EncounterStatus.Active)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasOtherActiveAsync(
        Guid campaignId,
        Guid encounterId,
        CancellationToken cancellationToken = default) =>
        database.Encounters.AsNoTracking().AnyAsync(
            encounter => encounter.CampaignId == campaignId
                && encounter.Status == EncounterStatus.Active
                && encounter.Id != encounterId,
            cancellationToken);

    public void Add(Encounter encounter) => database.Encounters.Add(encounter);

    public void Remove(Encounter encounter) => database.Encounters.Remove(encounter);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CombatPersistenceConflictException("The encounter changed before this operation completed.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new CombatPersistenceConflictException("The encounter conflicts with the current campaign state.", exception);
        }
    }
}
