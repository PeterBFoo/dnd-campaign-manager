using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DndCampaign.Modules.Characters.Application.Ports;

namespace DndCampaign.Modules.Characters.Infrastructure.Storage;

internal sealed class AzureBlobCharacterImageStore(BlobContainerClient container) : ICharacterImageStore
{
    private const long MaximumSize = 5 * 1024 * 1024;

    public async Task<StoredCharacterImage> StoreAsync(
        Guid campaignId,
        Guid characterId,
        CharacterImageUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (upload.Length is <= 0 or > MaximumSize)
        {
            throw new CharacterImageValidationException("La imagen debe ocupar entre 1 byte y 5 MiB.");
        }

        await using var buffer = new MemoryStream(capacity: checked((int)upload.Length));
        await upload.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != upload.Length || buffer.Length > MaximumSize)
        {
            throw new CharacterImageValidationException("El tamaño real de la imagen no coincide o supera 5 MiB.");
        }

        var (contentType, extension) = DetectFormat(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        if (!string.IsNullOrWhiteSpace(upload.DeclaredContentType)
            && !string.Equals(upload.DeclaredContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new CharacterImageValidationException("El tipo declarado no coincide con el contenido de la imagen.");
        }

        var objectKey = $"characters/{campaignId:N}/{characterId:N}/{Guid.NewGuid():N}.{extension}";
        buffer.Position = 0;
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await container.GetBlobClient(objectKey).UploadAsync(buffer, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                ContentDisposition = "inline",
                CacheControl = "private, max-age=3600",
            },
        }, cancellationToken);
        return new StoredCharacterImage(objectKey, contentType, buffer.Length);
    }

    public async Task<CharacterImageContent?> OpenReadAsync(
        string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await container.GetBlobClient(objectKey).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new CharacterImageContent(
                download.Value.Content,
                download.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken = default) =>
        await container.GetBlobClient(objectKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private static (string ContentType, string Extension) DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
        {
            return ("image/png", "png");
        }

        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
        {
            return ("image/jpeg", "jpg");
        }

        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ("image/webp", "webp");
        }

        throw new CharacterImageValidationException("El archivo debe ser una imagen JPEG, PNG o WebP válida.");
    }
}
