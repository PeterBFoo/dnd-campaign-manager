using DndCampaign.Modules.Journal.Application.Ports;
using DndCampaign.Modules.Journal.Domain.Entries;
using DndCampaign.Modules.Journal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DndCampaign.Modules.Journal.Tests.Infrastructure;

public sealed class JournalPersistenceTests
{
    [Fact]
    public async Task Migration_creates_schema_and_keyset_pagination_is_stable()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Journal persistence tests.");
        }

        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var database = new JournalDbContext(options);
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await database.Entries.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        var repository = new JournalRepository(database);
        var campaignId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-08-23T10:00:00Z");
        for (var index = 1; index <= 3; index++)
        {
            repository.Add(JournalEntry.Create(
                campaignId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"Personaje {index}",
                $"Entrada {index}",
                createdAt));
        }
        repository.Add(JournalEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Otra campaña", "Aislada", createdAt));
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        var first = await repository.ListPageAsync(
            campaignId, null, 2, TestContext.Current.CancellationToken);
        var boundary = first[^1];
        var second = await repository.ListPageAsync(
            campaignId,
            new JournalPageCursor(boundary.CreatedAt, boundary.PaginationSequence),
            2,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, first.Count);
        Assert.Single(second);
        Assert.Empty(first.Select(entry => entry.Id).Intersect(second.Select(entry => entry.Id)));
        Assert.All(first.Concat(second), entry => Assert.Equal(campaignId, entry.CampaignId));

        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'journal'
              AND tablename = 'journal_entries'
            """;
        await database.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var indexCount = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        Assert.True(indexCount >= 3);
    }
}
