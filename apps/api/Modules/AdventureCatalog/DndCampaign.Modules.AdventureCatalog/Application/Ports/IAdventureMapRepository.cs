using DndCampaign.Modules.AdventureCatalog.Domain.Maps;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;

namespace DndCampaign.Modules.AdventureCatalog.Application.Ports;

internal interface IAdventureMapRepository
{
    Task<bool> ModuleExistsAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<AdventureMap?> FindAsync(Guid moduleId, Guid mapId, bool tracking = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureMap>> ListAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureChapter>> ListChaptersAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<bool> ChapterExistsAsync(Guid moduleId, Guid chapterId, CancellationToken cancellationToken = default);
    void Add(AdventureMap map);
    void Remove(AdventureMap map);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

internal sealed class AdventureMapConcurrencyException : Exception;
internal sealed class AdventureMapChapterConflictException : Exception;
