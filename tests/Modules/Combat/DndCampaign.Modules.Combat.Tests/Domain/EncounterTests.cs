using DndCampaign.Modules.Combat.Domain.Encounters;
using Xunit;

namespace DndCampaign.Modules.Combat.Tests.Domain;

public sealed class EncounterTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");

    [Fact]
    public void Ties_require_confirmation_and_turns_wrap_into_the_next_round()
    {
        var encounter = Encounter.Create(Guid.NewGuid(), " Encuentro genérico ", Now);
        var character = encounter.AddCharacter(Guid.NewGuid(), "Exploradora", 16, 15);
        var enemy = encounter.AddEnemy("Adversario", 14, 15, 20);

        Assert.Equal("Encuentro genérico", encounter.Name);
        Assert.False(encounter.TiesResolved);
        Assert.Throws<InvalidOperationException>(() => encounter.Activate(Now.AddMinutes(1)));

        encounter.ConfirmInitiativeOrder([enemy.Id, character.Id]);
        encounter.Activate(Now.AddMinutes(1));
        encounter.AdvanceTurn();
        encounter.AdvanceTurn();

        Assert.Equal(EncounterStatus.Active, encounter.Status);
        Assert.Equal(2, encounter.Round);
        Assert.Equal(enemy.Id, encounter.CurrentParticipantId);
        Assert.Equal([enemy.Id, character.Id], encounter.Participants
            .OrderBy(participant => participant.OrderPosition)
            .Select(participant => participant.Id));
    }

    [Fact]
    public void Enemy_hit_points_are_bounded_and_finished_encounter_is_read_only()
    {
        var encounter = Encounter.Create(Guid.NewGuid(), "Encuentro", Now);
        var enemy = encounter.AddEnemy("Adversario", 12, 10, 8);
        var member = enemy.EnemyMembers.Single();
        encounter.Activate(Now.AddMinutes(1));

        encounter.AdjustEnemyHitPoints(enemy.Id, member.Id, HitPointAdjustmentKind.Damage, 20);
        Assert.Equal(0, member.CurrentHitPoints);
        encounter.AdjustEnemyHitPoints(enemy.Id, member.Id, HitPointAdjustmentKind.Healing, 20);
        Assert.Equal(8, member.CurrentHitPoints);

        encounter.Finish(Now.AddMinutes(2));
        Assert.Equal(EncounterStatus.Finished, encounter.Status);
        Assert.Throws<InvalidOperationException>(() => encounter.AdvanceTurn());
        Assert.Throws<InvalidOperationException>(() => encounter.AddEnemy("Otro", 10, 2, 3));
    }

    [Fact]
    public void Initiative_order_rejects_missing_or_ascending_participants()
    {
        var encounter = Encounter.Create(Guid.NewGuid(), "Encuentro", Now);
        var first = encounter.AddEnemy("Primero", 12, 18, 10);
        var second = encounter.AddEnemy("Segundo", 12, 10, 10);

        Assert.Throws<ArgumentException>(() => encounter.ConfirmInitiativeOrder([first.Id]));
        Assert.Throws<ArgumentException>(() => encounter.ConfirmInitiativeOrder([second.Id, first.Id]));
    }

    [Fact]
    public void Enemy_group_has_one_turn_independent_hit_points_and_is_skipped_when_defeated()
    {
        var encounter = Encounter.Create(Guid.NewGuid(), "Encuentro", Now);
        var character = encounter.AddCharacter(Guid.NewGuid(), "Exploradora", 16, 18);
        var group = encounter.AddEnemyGroup("Lobos", 13, 12, 11, 8);
        var members = group.EnemyMembers.OrderBy(member => member.Ordinal).ToArray();
        encounter.Activate(Now.AddMinutes(1));

        encounter.AdvanceTurn();
        encounter.AdjustEnemyHitPoints(group.Id, members[0].Id, HitPointAdjustmentKind.Damage, 5);

        Assert.Equal(8, members.Length);
        Assert.Equal(6, members[0].CurrentHitPoints);
        Assert.All(members[1..], member => Assert.Equal(11, member.CurrentHitPoints));

        foreach (var member in members)
        {
            encounter.AdjustEnemyHitPoints(group.Id, member.Id, HitPointAdjustmentKind.Damage, 100);
        }
        encounter.AdvanceTurn();

        Assert.Equal(character.Id, encounter.CurrentParticipantId);
        Assert.Equal(2, encounter.Round);
    }

    [Fact]
    public void Active_encounter_cannot_be_deleted()
    {
        var encounter = Encounter.Create(Guid.NewGuid(), "Encuentro", Now);
        encounter.EnsureCanDelete();
        encounter.AddEnemy("Adversario", 12, 10, 8);
        encounter.Activate(Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(encounter.EnsureCanDelete);
        encounter.Finish(Now.AddMinutes(2));
        encounter.EnsureCanDelete();
    }
}
