namespace DndCampaign.Modules.AdventureCatalog.Application.Ports;

internal sealed record AdventureMapImageUpload(Stream Content, long Length, string DeclaredContentType);
internal sealed record StoredAdventureMapImage(string ObjectKey, string ContentType, long SizeBytes, int Width, int Height);
internal sealed record AdventureMapImageContent(Stream Content, string ContentType);
internal sealed class AdventureMapImageValidationException(string message) : Exception(message);

internal interface IAdventureMapImageStore
{
    Task<StoredAdventureMapImage> StoreAsync(Guid moduleId, Guid mapId, AdventureMapImageUpload upload, CancellationToken cancellationToken = default);
    Task<AdventureMapImageContent?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken = default);
}
