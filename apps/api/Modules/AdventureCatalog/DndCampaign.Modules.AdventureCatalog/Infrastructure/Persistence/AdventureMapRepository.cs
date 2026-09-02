using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureMapRepository(AdventureCatalogDbContext database) : IAdventureMapRepository
{
    public Task<bool> ModuleExistsAsync(Guid moduleId, CancellationToken cancellationToken = default) =>
        database.AdventureModules.AnyAsync(module => module.Id == moduleId, cancellationToken);

    public Task<AdventureMap?> FindAsync(Guid moduleId, Guid mapId, bool tracking = true, CancellationToken cancellationToken = default)
    {
        IQueryable<AdventureMap> query = database.AdventureMaps.Include(map => map.Chapters);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(map => map.ModuleId == moduleId && map.Id == mapId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdventureMap>> ListAsync(Guid moduleId, CancellationToken cancellationToken = default) =>
        await database.AdventureMaps.AsNoTracking().Include(map => map.Chapters)
            .Where(map => map.ModuleId == moduleId).OrderBy(map => map.Name).ThenBy(map => map.Id).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<AdventureChapter>> ListChaptersAsync(Guid moduleId, CancellationToken cancellationToken = default) =>
        await database.AdventureChapters.AsNoTracking().Where(chapter => chapter.ModuleId == moduleId)
            .OrderBy(chapter => chapter.Position).ThenBy(chapter => chapter.Id).ToArrayAsync(cancellationToken);

    public Task<bool> ChapterExistsAsync(Guid moduleId, Guid chapterId, CancellationToken cancellationToken = default) =>
        database.AdventureChapters.AnyAsync(chapter => chapter.ModuleId == moduleId && chapter.Id == chapterId, cancellationToken);

    public void Add(AdventureMap map) => database.AdventureMaps.Add(map);
    public void Remove(AdventureMap map) => database.AdventureMaps.Remove(map);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new AdventureMapConcurrencyException { Source = exception.Source }; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new AdventureMapChapterConflictException { Source = exception.Source }; }
    }
}
