using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureModuleRepository(AdventureCatalogDbContext database)
    : IAdventureModuleRepository
{
    public async Task<IReadOnlyList<AdventureModule>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await database.AdventureModules
            .AsNoTracking()
            .OrderByDescending(module => module.UpdatedAt)
            .ThenBy(module => module.Id)
            .ToArrayAsync(cancellationToken);

    public Task<AdventureModule?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.AdventureModules.SingleOrDefaultAsync(module => module.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default) =>
        database.AdventureModules.AnyAsync(module =>
            module.NormalizedName == normalizedName
            && (!excludedId.HasValue || module.Id != excludedId.Value), cancellationToken);

    public void Add(AdventureModule module) => database.AdventureModules.Add(module);

    public void Remove(AdventureModule module) => database.AdventureModules.Remove(module);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new AdventureModuleConcurrencyException { Source = exception.Source };
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new AdventureModuleNameConflictException { Source = exception.Source };
        }
    }
}
