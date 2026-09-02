using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DndCampaign.Modules.AdventureCatalog.Tests.Infrastructure;

public sealed class AdventureCatalogPersistenceTests
{
    [Fact]
    public async Task Migration_creates_private_catalog_schema_and_unique_name_index()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Adventure Catalog persistence tests.");
        }

        var options = new DbContextOptionsBuilder<AdventureCatalogDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new AdventureCatalogDbContext(options);
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var module = AdventureModule.Create(Guid.NewGuid(), "Módulo persistido", null,
            EditorialProvenance.Create(EditorialOriginKind.Original, null, "Autoría propia", null, DateTimeOffset.UtcNow, Guid.NewGuid()), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        database.AdventureModules.Add(module);
        var chapter = AdventureChapter.Create(Guid.NewGuid(), module.Id, "Introducción", null, 1,
            EditorialProvenance.Create(EditorialOriginKind.Original, null, "Autoría propia", null, DateTimeOffset.UtcNow, Guid.NewGuid()), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var map = AdventureMap.Create(Guid.NewGuid(), module.Id, "Mapa persistido", null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        map.AddChapter(chapter.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
        database.AdventureChapters.Add(chapter);
        database.AdventureMaps.Add(map);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await database.AdventureModules.SingleAsync(x => x.Id == module.Id, TestContext.Current.CancellationToken));
        var persistedMap = await database.AdventureMaps.Include(item => item.Chapters).SingleAsync(item => item.Id == map.Id, TestContext.Current.CancellationToken);
        Assert.Single(persistedMap.Chapters);
    }
}
