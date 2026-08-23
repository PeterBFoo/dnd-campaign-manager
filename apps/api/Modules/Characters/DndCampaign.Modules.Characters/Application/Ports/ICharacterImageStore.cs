namespace DndCampaign.Modules.Characters.Application.Ports;

internal interface ICharacterImageStore
{
    Task<StoredCharacterImage> StoreAsync(
        Guid campaignId,
        Guid characterId,
        CharacterImageUpload upload,
        CancellationToken cancellationToken = default);

    Task<CharacterImageContent?> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}

internal sealed record CharacterImageUpload(Stream Content, long Length, string? DeclaredContentType);
internal sealed record StoredCharacterImage(string ObjectKey, string ContentType, long SizeBytes);
internal sealed record CharacterImageContent(Stream Content, string ContentType);
internal sealed class CharacterImageValidationException(string message) : Exception(message);
