using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

namespace DndCampaign.Modules.AdventureCatalog.Application.Ports;

internal interface IAdventureModuleRepository
{
    Task<IReadOnlyList<AdventureModule>> ListAsync(CancellationToken cancellationToken = default);

    Task<AdventureModule?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default);

    void Add(AdventureModule module);

    void Remove(AdventureModule module);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

internal sealed class AdventureModuleNameConflictException : Exception
{
}

internal sealed class AdventureModuleConcurrencyException : Exception
{
}
