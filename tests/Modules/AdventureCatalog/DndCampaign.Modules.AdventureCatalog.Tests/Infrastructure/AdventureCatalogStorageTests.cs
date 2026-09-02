using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Infrastructure.Storage;
using Xunit;

namespace DndCampaign.Modules.AdventureCatalog.Tests.Infrastructure;

public sealed class AdventureCatalogStorageTests
{
    [Fact]
    public async Task Map_image_round_trip_uses_private_module_prefix_and_reads_dimensions()
    {
        var connectionString = Environment.GetEnvironmentVariable("Storage__AdventureCatalog__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("Storage__AdventureCatalog__ConnectionString is required for blob integration tests.");
        var container = new BlobContainerClient(connectionString, $"map-tests-{Guid.NewGuid():N}");
        var store = new AzureBlobAdventureMapImageStore(new AdventureCatalogBlobContainer(container));
        var bytes = new byte[24] { 137,80,78,71,13,10,26,10, 0,0,0,13, 73,72,68,82, 0,0,0,2, 0,0,0,3 };
        try
        {
            await using var upload = new MemoryStream(bytes);
            var stored = await store.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), new AdventureMapImageUpload(upload, bytes.Length, "image/png"), TestContext.Current.CancellationToken);
            Assert.Equal(2, stored.Width); Assert.Equal(3, stored.Height); Assert.Contains("/maps/", stored.ObjectKey);
            Assert.NotNull(await store.OpenReadAsync(stored.ObjectKey, TestContext.Current.CancellationToken));
            Assert.Equal(PublicAccessType.None, (await container.GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken)).Value.PublicAccess);
        }
        finally { await container.DeleteIfExistsAsync(cancellationToken: CancellationToken.None); }
    }

    [Fact]
    public async Task Azurite_round_trip_validates_signature_and_keeps_container_private()
    {
        var connectionString = Environment.GetEnvironmentVariable("Storage__AdventureCatalog__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Storage__AdventureCatalog__ConnectionString is required for blob integration tests.");
        }

        var container = new BlobContainerClient(connectionString, $"adventure-tests-{Guid.NewGuid():N}");
        var store = new AzureBlobAdventureModuleCoverStore(new AdventureCatalogBlobContainer(container));
        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        try
        {
            await using var upload = new MemoryStream(bytes);
            var stored = await store.StoreAsync(Guid.NewGuid(), new AdventureModuleCoverUpload(upload, bytes.Length, "image/png"), TestContext.Current.CancellationToken);
            var downloaded = await store.OpenReadAsync(stored.ObjectKey, TestContext.Current.CancellationToken);
            Assert.Equal("image/png", stored.ContentType);
            Assert.NotNull(downloaded);
            await using var content = downloaded!.Content;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, TestContext.Current.CancellationToken);
            Assert.Equal(bytes, copy.ToArray());
            var properties = await container.GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(PublicAccessType.None, properties.Value.PublicAccess);
        }
        finally
        {
            await container.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
        }
    }
}
