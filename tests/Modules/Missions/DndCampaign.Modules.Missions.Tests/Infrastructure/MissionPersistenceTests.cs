using DndCampaign.Modules.Missions.Domain.Missions;
using DndCampaign.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DndCampaign.Modules.Missions.Tests.Infrastructure;

public sealed class MissionPersistenceTests
{
    [Fact]
    public async Task Migration_orders_missions_and_concurrent_promotions_keep_one_main()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Missions persistence tests.");
        }

        var options = new DbContextOptionsBuilder<MissionsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var campaignId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");
        Guid firstId;
        Guid secondId;
        await using (var database = new MissionsDbContext(options))
        {
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await database.Missions.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            var repository = new MissionRepository(database);
            var first = Mission.CreateForDm(campaignId, Guid.NewGuid(), "Primera misión", null, false, now);
            var second = Mission.CreateForDm(campaignId, Guid.NewGuid(), "Segunda misión", null, false, now.AddSeconds(1));
            firstId = first.Id;
            secondId = second.Id;
            repository.Add(first);
            repository.Add(second);
            repository.Add(Mission.CreateForDm(
                Guid.NewGuid(), Guid.NewGuid(), "Otra campaña", null, true, now));
            await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var firstDatabase = new MissionsDbContext(options);
        await using var secondDatabase = new MissionsDbContext(options);
        var firstRepository = new MissionRepository(firstDatabase);
        var secondRepository = new MissionRepository(secondDatabase);
        var firstMission = await firstRepository.FindForUpdateAsync(
            campaignId, firstId, TestContext.Current.CancellationToken);
        var secondMission = await secondRepository.FindForUpdateAsync(
            campaignId, secondId, TestContext.Current.CancellationToken);
        firstMission!.MarkAsMain(now.AddMinutes(1));
        secondMission!.MarkAsMain(now.AddMinutes(2));

        await Task.WhenAll(
            firstRepository.SaveAsMainAsync(campaignId, firstMission, TestContext.Current.CancellationToken),
            secondRepository.SaveAsMainAsync(campaignId, secondMission, TestContext.Current.CancellationToken));

        await using var verification = new MissionsDbContext(options);
        var main = await verification.Missions.AsNoTracking()
            .Where(mission => mission.CampaignId == campaignId && mission.IsMain)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var ordered = await new MissionRepository(verification).ListAsync(
            campaignId, TestContext.Current.CancellationToken);

        Assert.Single(main);
        Assert.True(ordered[0].IsMain);
        Assert.Equal(2, ordered.Count);
        Assert.All(ordered, mission => Assert.Equal(campaignId, mission.CampaignId));

        await using var command = verification.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'missions'
              AND indexname = 'IX_missions_CampaignId_IsMain'
            """;
        await verification.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var indexDefinition = (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(indexDefinition);
        Assert.Contains("UNIQUE", indexDefinition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsMain", indexDefinition, StringComparison.Ordinal);
    }
}
