using DndCampaign.Modules.Combat.Domain.Encounters;

namespace DndCampaign.Modules.Combat.Application.Ports;

internal interface IEncounterRepository
{
    Task<IReadOnlyList<Encounter>> ListAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<Encounter?> FindAsync(
        Guid campaignId,
        Guid encounterId,
        bool tracking,
        CancellationToken cancellationToken = default);

    Task<Encounter?> FindActiveAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOtherActiveAsync(
        Guid campaignId,
        Guid encounterId,
        CancellationToken cancellationToken = default);

    void Add(Encounter encounter);

    void Remove(Encounter encounter);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

internal sealed class CombatPersistenceConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
