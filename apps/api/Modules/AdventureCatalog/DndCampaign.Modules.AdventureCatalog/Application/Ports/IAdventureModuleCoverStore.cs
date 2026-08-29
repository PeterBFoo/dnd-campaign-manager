namespace DndCampaign.Modules.AdventureCatalog.Application.Ports;

internal sealed record AdventureModuleCoverUpload(Stream Content, long Length, string DeclaredContentType);

internal sealed record StoredAdventureModuleCover(string ObjectKey, string ContentType, long SizeBytes);

internal sealed record AdventureModuleCoverContent(Stream Content, string ContentType);

internal sealed class AdventureModuleCoverValidationException(string message) : Exception(message);

internal interface IAdventureModuleCoverStore
{
    Task<StoredAdventureModuleCover> StoreAsync(
        Guid moduleId,
        AdventureModuleCoverUpload upload,
        CancellationToken cancellationToken = default);

    Task<AdventureModuleCoverContent?> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken = default);
}
