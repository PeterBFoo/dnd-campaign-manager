using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;

namespace DndCampaign.Modules.AdventureCatalog.Application.Ports;

internal interface IAdventureChapterRepository
{
    Task<AdventureModule?> FindModuleAsync(Guid moduleId, bool tracked, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureChapter>> ListAsync(Guid moduleId, bool tracked, CancellationToken cancellationToken = default);
    Task<AdventureChapter?> FindAsync(Guid moduleId, Guid chapterId, bool tracked, CancellationToken cancellationToken = default);
    void Add(AdventureChapter chapter);
    void Remove(AdventureChapter chapter);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task ReorderAsync(AdventureModule module, IReadOnlyList<(AdventureChapter Chapter, int Position)> order, CancellationToken cancellationToken = default);
    Task DeleteAndCompactAsync(AdventureModule module, AdventureChapter chapter, IReadOnlyList<AdventureChapter> remaining, CancellationToken cancellationToken = default);
}
