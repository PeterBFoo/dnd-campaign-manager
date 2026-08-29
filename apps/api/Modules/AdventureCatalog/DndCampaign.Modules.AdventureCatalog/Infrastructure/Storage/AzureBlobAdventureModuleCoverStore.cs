using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Storage;

internal sealed class AdventureCatalogBlobContainer(BlobContainerClient client)
{
    public BlobContainerClient Client { get; } = client;
}

internal sealed class AzureBlobAdventureModuleCoverStore(AdventureCatalogBlobContainer container)
    : IAdventureModuleCoverStore
{
    private const int MaximumSize = 10 * 1024 * 1024;

    public async Task<StoredAdventureModuleCover> StoreAsync(
        Guid moduleId,
        AdventureModuleCoverUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (moduleId == Guid.Empty)
        {
            throw new ArgumentException("A module identifier is required.", nameof(moduleId));
        }
        if (upload.Length is < 1 or > MaximumSize)
        {
            throw new AdventureModuleCoverValidationException(
                "La portada debe ocupar entre 1 byte y 10 MiB.");
        }

        using var buffer = new MemoryStream(capacity: checked((int)upload.Length));
        await upload.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != upload.Length || buffer.Length > MaximumSize)
        {
            throw new AdventureModuleCoverValidationException(
                "El tamaño real de la portada no coincide o supera 10 MiB.");
        }

        var bytes = buffer.ToArray();
        var (contentType, extension) = DetectFormat(bytes);
        if (!string.Equals(upload.DeclaredContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new AdventureModuleCoverValidationException(
                "El tipo declarado no coincide con el contenido de la portada.");
        }

        await container.Client.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var objectKey = $"adventure-modules/{moduleId:N}/{Guid.NewGuid():N}.{extension}";
        buffer.Position = 0;
        await container.Client.GetBlobClient(objectKey).UploadAsync(buffer, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, cancellationToken);
        return new StoredAdventureModuleCover(objectKey, contentType, buffer.Length);
    }

    public async Task<AdventureModuleCoverContent?> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await container.Client.GetBlobClient(objectKey)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new AdventureModuleCoverContent(
                download.Value.Content,
                download.Value.Details.ContentType);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken = default) =>
        container.Client.GetBlobClient(objectKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private static (string ContentType, string Extension) DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8
            && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
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
        throw new AdventureModuleCoverValidationException(
            "La portada debe ser JPEG, PNG o WebP válido.");
    }
}
