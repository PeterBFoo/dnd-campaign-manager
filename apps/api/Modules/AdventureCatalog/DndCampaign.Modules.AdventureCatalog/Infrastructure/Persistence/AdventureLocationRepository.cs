using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using DndCampaign.Modules.AdventureCatalog.Domain.Locations;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureLocationRepository(AdventureCatalogDbContext database) : IAdventureLocationRepository
{
    public Task<bool> ModuleExistsAsync(Guid moduleId, CancellationToken cancellationToken = default) =>
        database.AdventureModules.AnyAsync(item => item.Id == moduleId, cancellationToken);

    public Task<bool> MapExistsAsync(Guid moduleId, Guid mapId, CancellationToken cancellationToken = default) =>
        database.AdventureMaps.AnyAsync(item => item.ModuleId == moduleId && item.Id == mapId, cancellationToken);

    public Task<bool> ChapterExistsAsync(Guid moduleId, Guid chapterId, CancellationToken cancellationToken = default) =>
        database.AdventureChapters.AnyAsync(item => item.ModuleId == moduleId && item.Id == chapterId, cancellationToken);

    public Task<AdventureLocation?> FindAsync(Guid moduleId, Guid locationId, bool tracking = true, CancellationToken cancellationToken = default)
    {
        IQueryable<AdventureLocation> query = database.AdventureLocations
            .Include(item => item.PointsOfInterest)
            .Include(item => item.Placements)
            .Include(item => item.Chapters)
            .AsSplitQuery();
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.ModuleId == moduleId && item.Id == locationId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdventureLocation>> ListAsync(Guid moduleId, bool tracking = false, CancellationToken cancellationToken = default)
    {
        IQueryable<AdventureLocation> query = database.AdventureLocations
            .Include(item => item.PointsOfInterest)
            .Include(item => item.Placements)
            .Include(item => item.Chapters)
            .AsSplitQuery()
            .Where(item => item.ModuleId == moduleId)
            .OrderBy(item => item.Name).ThenBy(item => item.Id);
        if (!tracking) query = query.AsNoTracking();
        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdventureChapter>> ListChaptersAsync(Guid moduleId, CancellationToken cancellationToken = default) =>
        await database.AdventureChapters.AsNoTracking().Where(item => item.ModuleId == moduleId)
            .OrderBy(item => item.Position).ThenBy(item => item.Id).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<AdventureMap>> ListMapsAsync(Guid moduleId, CancellationToken cancellationToken = default) =>
        await database.AdventureMaps.AsNoTracking().Where(item => item.ModuleId == moduleId)
            .OrderBy(item => item.Name).ThenBy(item => item.Id).ToArrayAsync(cancellationToken);

    public void Add(AdventureLocation location) => database.AdventureLocations.Add(location);
    public void Remove(AdventureLocation location) => database.AdventureLocations.Remove(location);

    public async Task ClearMapDependenciesAsync(Guid moduleId, Guid mapId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var locations = await database.AdventureLocations
            .Include(item => item.PointsOfInterest)
            .Include(item => item.Placements)
            .AsSplitQuery()
            .Where(item => item.ModuleId == moduleId && (item.DetailMapId == mapId || item.Placements.Any(placement => placement.MapId == mapId)))
            .ToArrayAsync(cancellationToken);
        foreach (var location in locations)
        {
            if (location.DetailMapId == mapId) location.SetDetailMap(null, actorId, now);
            location.RemovePlacement(mapId, actorId, now);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new AdventureLocationConcurrencyException { Source = exception.Source }; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new AdventureLocationRelationConflictException { Source = exception.Source }; }
    }
}
