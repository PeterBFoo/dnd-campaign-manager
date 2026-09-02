using DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace DndCampaign.Modules.AdventureCatalog.Tests.Infrastructure;

public sealed class AdventureChapterMigrationTests
{
    [Fact]
    public void Chapter_migration_adopts_the_provisional_table_without_dropping_data()
    {
        var migration = new InspectableAdventureChapters();
        var operations = migration.BuildUp();
        var sql = Assert.Single(operations.OfType<SqlOperation>()).Sql;

        Assert.Contains("CREATE TABLE IF NOT EXISTS adventure_catalog.adventure_chapters", sql);
        Assert.Contains("ADD COLUMN IF NOT EXISTS \"ChapterRightsBasis\"", sql);
        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
    }

    [Fact]
    public void Map_migration_adopts_provisional_tables_without_dropping_data()
    {
        var migration = new InspectableAdventureMaps();
        var operations = migration.BuildUp();
        var sql = Assert.Single(operations.OfType<SqlOperation>()).Sql;

        Assert.Contains("CREATE TABLE IF NOT EXISTS adventure_catalog.adventure_maps", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS adventure_catalog.adventure_map_chapters", sql);
        Assert.Contains("ADD COLUMN IF NOT EXISTS \"ImageObjectKey\"", sql);
        Assert.Contains("CREATE INDEX IF NOT EXISTS", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
    }

    private sealed class InspectableAdventureChapters : AdventureChapters
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }

    private sealed class InspectableAdventureMaps : AdventureMaps
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }
}
