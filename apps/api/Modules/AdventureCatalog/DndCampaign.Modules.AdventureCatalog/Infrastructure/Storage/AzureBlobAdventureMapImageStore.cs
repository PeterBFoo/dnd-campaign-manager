using System.Buffers.Binary;
using Azure;
using Azure.Storage.Blobs.Models;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Storage;

internal sealed class AzureBlobAdventureMapImageStore(AdventureCatalogBlobContainer container) : IAdventureMapImageStore
{
    private const int MaximumSize = 20 * 1024 * 1024;

    public async Task<StoredAdventureMapImage> StoreAsync(Guid moduleId, Guid mapId, AdventureMapImageUpload upload, CancellationToken cancellationToken = default)
    {
        if (moduleId == Guid.Empty || mapId == Guid.Empty) throw new ArgumentException("Los identificadores son obligatorios.");
        if (upload.Length is < 1 or > MaximumSize) throw new AdventureMapImageValidationException("La imagen debe ocupar entre 1 byte y 20 MiB.");
        using var buffer = new MemoryStream(checked((int)upload.Length));
        await upload.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != upload.Length || buffer.Length > MaximumSize) throw new AdventureMapImageValidationException("El tamaño real de la imagen no coincide o supera 20 MiB.");
        var bytes = buffer.ToArray();
        var detected = Detect(bytes);
        if (!string.Equals(upload.DeclaredContentType, detected.Type, StringComparison.OrdinalIgnoreCase))
            throw new AdventureMapImageValidationException("El tipo declarado no coincide con el contenido de la imagen.");
        if ((long)detected.Width * detected.Height > 50_000_000) throw new AdventureMapImageValidationException("La imagen no puede superar 50 megapíxeles.");

        await container.Client.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var key = $"adventure-modules/{moduleId:N}/maps/{mapId:N}/{Guid.NewGuid():N}.{detected.Extension}";
        buffer.Position = 0;
        await container.Client.GetBlobClient(key).UploadAsync(buffer, new BlobUploadOptions
        { HttpHeaders = new BlobHttpHeaders { ContentType = detected.Type } }, cancellationToken);
        return new(key, detected.Type, buffer.Length, detected.Width, detected.Height);
    }

    public async Task<AdventureMapImageContent?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await container.Client.GetBlobClient(objectKey).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new(download.Value.Content, download.Value.Details.ContentType);
        }
        catch (RequestFailedException exception) when (exception.Status == 404) { return null; }
    }

    public Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken = default) =>
        container.Client.GetBlobClient(objectKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private static (string Type, string Extension, int Width, int Height) Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 24 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes[12..16].SequenceEqual("IHDR"u8))
            return ("image/png", "png", checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[16..20])), checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[20..24])));
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return DetectWebp(bytes);
        if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8)
            return DetectJpeg(bytes);
        throw new AdventureMapImageValidationException("La imagen debe ser JPEG, PNG o WebP válido.");
    }

    private static (string, string, int, int) DetectWebp(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 30 && bytes[12..16].SequenceEqual("VP8X"u8))
            return ("image/webp", "webp", 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16), 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16));
        if (bytes.Length >= 30 && bytes[12..16].SequenceEqual("VP8 "u8) && bytes[23..26].SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
            return ("image/webp", "webp", BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..28]) & 0x3fff, BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..30]) & 0x3fff);
        if (bytes.Length >= 25 && bytes[12..16].SequenceEqual("VP8L"u8) && bytes[20] == 0x2f)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(bytes[21..25]);
            return ("image/webp", "webp", 1 + (int)(bits & 0x3fff), 1 + (int)((bits >> 14) & 0x3fff));
        }
        throw new AdventureMapImageValidationException("El WebP no contiene dimensiones válidas.");
    }

    private static (string, string, int, int) DetectJpeg(ReadOnlySpan<byte> bytes)
    {
        var offset = 2;
        while (offset + 8 < bytes.Length)
        {
            if (bytes[offset++] != 0xff) continue;
            var marker = bytes[offset++];
            if (marker is 0xd8 or 0xd9) continue;
            if (offset + 2 > bytes.Length) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || offset + length > bytes.Length) break;
            if (marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
                return ("image/jpeg", "jpg", BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2)), BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2)));
            offset += length;
        }
        throw new AdventureMapImageValidationException("El JPEG no contiene dimensiones válidas.");
    }
}
