using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using DndCampaign.Modules.AdventureCatalog.Domain.Locations;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;

namespace DndCampaign.Modules.AdventureCatalog.Application.Ports;

internal interface IAdventureLocationRepository
{
    Task<bool> ModuleExistsAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<bool> MapExistsAsync(Guid moduleId, Guid mapId, CancellationToken cancellationToken = default);
    Task<bool> ChapterExistsAsync(Guid moduleId, Guid chapterId, CancellationToken cancellationToken = default);
    Task<AdventureLocation?> FindAsync(Guid moduleId, Guid locationId, bool tracking = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureLocation>> ListAsync(Guid moduleId, bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureChapter>> ListChaptersAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdventureMap>> ListMapsAsync(Guid moduleId, CancellationToken cancellationToken = default);
    void Add(AdventureLocation location);
    void Remove(AdventureLocation location);
    Task ClearMapDependenciesAsync(Guid moduleId, Guid mapId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

internal sealed class AdventureLocationConcurrencyException : Exception;
internal sealed class AdventureLocationRelationConflictException : Exception;
