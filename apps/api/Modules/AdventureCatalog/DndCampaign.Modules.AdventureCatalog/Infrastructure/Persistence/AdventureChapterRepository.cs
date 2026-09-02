using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureChapterRepository(AdventureCatalogDbContext database) : IAdventureChapterRepository
{
    public Task<AdventureModule?> FindModuleAsync(Guid moduleId, bool tracked, CancellationToken cancellationToken = default)
    {
        var query = database.AdventureModules.AsQueryable();
        return (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == moduleId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdventureChapter>> ListAsync(Guid moduleId, bool tracked, CancellationToken cancellationToken = default)
    {
        var query = database.AdventureChapters.Where(x => x.ModuleId == moduleId);
        return await (tracked ? query : query.AsNoTracking()).OrderBy(x => x.Position).ThenBy(x => x.Id).ToArrayAsync(cancellationToken);
    }

    public Task<AdventureChapter?> FindAsync(Guid moduleId, Guid chapterId, bool tracked, CancellationToken cancellationToken = default)
    {
        var query = database.AdventureChapters.Where(x => x.ModuleId == moduleId && x.Id == chapterId);
        return (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(AdventureChapter chapter) => database.AdventureChapters.Add(chapter);
    public void Remove(AdventureChapter chapter) => database.AdventureChapters.Remove(chapter);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new AdventureModuleConcurrencyException(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new AdventureModuleConcurrencyException(); }
    }

    public async Task ReorderAsync(AdventureModule module, IReadOnlyList<(AdventureChapter Chapter, int Position)> order, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in order) item.Chapter.MoveTo(item.Position + 100000);
            await database.SaveChangesAsync(cancellationToken);
            foreach (var item in order) item.Chapter.MoveTo(item.Position);
            module.AdvanceChaptersVersion();
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
        { await transaction.RollbackAsync(cancellationToken); throw new AdventureModuleConcurrencyException(); }
    }

    public async Task DeleteAndCompactAsync(AdventureModule module, AdventureChapter chapter, IReadOnlyList<AdventureChapter> remaining, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            database.AdventureChapters.Remove(chapter);
            foreach (var item in remaining.Where(x => x.Position > chapter.Position)) item.MoveTo(item.Position + 100000);
            await database.SaveChangesAsync(cancellationToken);
            foreach (var item in remaining.Where(x => x.Position > 100000)) item.MoveTo(item.Position - 100001);
            module.AdvanceChaptersVersion();
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
        { await transaction.RollbackAsync(cancellationToken); throw new AdventureModuleConcurrencyException(); }
    }
}
