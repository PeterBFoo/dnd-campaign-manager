using DndCampaign.Modules.Combat.Application.Ports;
using DndCampaign.Modules.Combat.Domain.Encounters;
using DndCampaign.Modules.Combat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DndCampaign.Modules.Combat.Tests.Infrastructure;

[Collection(CombatDatabaseCollection.Name)]
public sealed class CombatPersistenceTests
{
    [Fact]
    public async Task Migration_preserves_reordered_ties_and_prevents_two_active_encounters()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("IDENTITY_TEST_DATABASE is required for Combat persistence tests.");
        }
        var options = new DbContextOptionsBuilder<CombatDbContext>().UseNpgsql(connectionString).Options;
        var campaignId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");
        Guid firstId;
        Guid firstEnemyId;
        Guid secondId;

        await using (var database = new CombatDbContext(options))
        {
            await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await database.Participants.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await database.Encounters.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            var repository = new EncounterRepository(database);
            var first = Encounter.Create(campaignId, "Primero", now);
            var firstEnemy = first.AddEnemyGroup("Adversario A", 12, 15, 10, 3);
            firstEnemyId = firstEnemy.Id;
            var secondEnemy = first.AddEnemy("Adversario B", 12, 15, 10);
            first.ConfirmInitiativeOrder([secondEnemy.Id, firstEnemy.Id]);
            var second = Encounter.Create(campaignId, "Segundo", now.AddMinutes(1));
            second.AddEnemy("Adversario C", 12, 10, 10);
            firstId = first.Id;
            secondId = second.Id;
            repository.Add(first);
            repository.Add(second);
            await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var database = new CombatDbContext(options))
        {
            var repository = new EncounterRepository(database);
            var first = await repository.FindAsync(campaignId, firstId, true, TestContext.Current.CancellationToken);
            first!.Activate(now.AddMinutes(2));
            await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var database = new CombatDbContext(options))
        {
            var repository = new EncounterRepository(database);
            var second = await repository.FindAsync(campaignId, secondId, true, TestContext.Current.CancellationToken);
            second!.Activate(now.AddMinutes(3));
            await Assert.ThrowsAsync<CombatPersistenceConflictException>(() =>
                repository.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verification = new CombatDbContext(options);
        var activeCount = await verification.Encounters.AsNoTracking()
            .CountAsync(item => item.CampaignId == campaignId && item.Status == EncounterStatus.Active,
                TestContext.Current.CancellationToken);
        var ordered = await verification.Participants.AsNoTracking()
            .Where(item => item.EncounterId == firstId)
            .OrderBy(item => item.OrderPosition)
            .Select(item => item.NameSnapshot)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var groupMembers = await verification.EnemyGroupMembers.AsNoTracking()
            .CountAsync(item => item.ParticipantId == firstEnemyId, TestContext.Current.CancellationToken);

        Assert.Equal(1, activeCount);
        Assert.Equal(["Adversario B", "Adversario A"], ordered);
        Assert.Equal(3, groupMembers);
    }
}
