using Azure.Storage.Blobs;
using DndCampaign.Modules.Characters.Application.Ports;
using DndCampaign.Modules.Characters.Domain.Characters;
using DndCampaign.Modules.Characters.Infrastructure.Persistence;
using DndCampaign.Modules.Characters.Infrastructure.Storage;
using DndCampaign.Modules.Characters.Infrastructure.Access;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DndCampaign.Modules.Characters.Tests.Infrastructure;

public sealed class CharacterPersistenceAndStorageTests
{
    [Fact]
    public async Task Migration_creates_the_character_schema_and_filtered_active_index()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Characters persistence tests.");
        }

        var options = new DbContextOptionsBuilder<CharactersDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var database = new CharactersDbContext(options);
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'characters'
              AND indexname = 'IX_characters_CampaignId_OwnerUserId'
            """;
        await database.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var indexDefinition = (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(indexDefinition);
        Assert.Contains("UNIQUE", indexDefinition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsActive", indexDefinition, StringComparison.Ordinal);

        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var active = PlayerCharacter.Create(campaignId, ownerId, "Primero", 15, 2,
            null, null, null, true, DateTimeOffset.UtcNow);
        var waiting = PlayerCharacter.Create(campaignId, ownerId, "Segundo", 14, 1,
            null, null, null, false, DateTimeOffset.UtcNow.AddSeconds(1));
        var repository = new CharacterRepository(database);
        repository.Add(active);
        repository.Add(waiting);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        await repository.DeleteAsync(active, TestContext.Current.CancellationToken);

        database.ChangeTracker.Clear();
        var remaining = await database.Characters.SingleAsync(
            character => character.CampaignId == campaignId,
            TestContext.Current.CancellationToken);
        Assert.True(remaining.IsActive);

        var snapshot = await new ActiveCharacterReader(database).GetActiveAsync(
            campaignId, ownerId, TestContext.Current.CancellationToken);
        Assert.NotNull(snapshot);
        Assert.Equal(remaining.Id, snapshot.CharacterId);
        Assert.Equal(remaining.Name, snapshot.Name);
    }

    [Fact]
    public async Task Azurite_round_trip_detects_format_and_keeps_the_container_private()
    {
        var connectionString = Environment.GetEnvironmentVariable("Storage__Characters__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Storage__Characters__ConnectionString is required for blob integration tests.");
        }

        var containerName = $"character-tests-{Guid.NewGuid():N}";
        var container = new BlobContainerClient(
            connectionString,
            containerName,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05));
        var store = new AzureBlobCharacterImageStore(container);
        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        try
        {
            await using var upload = new MemoryStream(bytes);
            var stored = await store.StoreAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CharacterImageUpload(upload, bytes.Length, "image/png"),
                TestContext.Current.CancellationToken);

            var downloaded = await store.OpenReadAsync(stored.ObjectKey, TestContext.Current.CancellationToken);

            Assert.Equal("image/png", stored.ContentType);
            Assert.NotNull(downloaded);
            await using var content = downloaded!.Content;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, TestContext.Current.CancellationToken);
            Assert.Equal(bytes, copy.ToArray());
            var properties = await container.GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(Azure.Storage.Blobs.Models.PublicAccessType.None, properties.Value.PublicAccess);
        }
        finally
        {
            await container.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
        }
    }
}
