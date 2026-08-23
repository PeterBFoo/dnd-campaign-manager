using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.CombatParticipants;
using DndCampaign.Modules.Combat.Application.Encounters;
using DndCampaign.Modules.Combat.Application.Ports;
using DndCampaign.Modules.Combat.Domain.Encounters;
using Xunit;

namespace DndCampaign.Modules.Combat.Tests.Application;

public sealed class EncounterApplicationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");

    [Fact]
    public async Task Dm_prepares_and_activates_while_player_only_receives_the_safe_projection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var campaignId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var repository = new FakeRepository();
        var dmApplication = CreateApplication(
            CampaignRole.Dm, repository,
            new CombatCharacterSnapshot(characterId, "Exploradora", 16));

        var created = await dmApplication.CreateAsync(
            new CreateEncounterCommand(Guid.NewGuid(), campaignId, "Encuentro"), cancellationToken);
        var withCharacter = await dmApplication.AddCharacterAsync(new AddCharacterCommand(
            Guid.NewGuid(), campaignId, created.Value!.Id, characterId, 18, created.Value.Version), cancellationToken);
        var withEnemy = await dmApplication.AddEnemyAsync(new AddEnemyCommand(
            Guid.NewGuid(), campaignId, created.Value.Id, "Adversarios", 12, 14, 20, 8,
            withCharacter.Value!.Version), cancellationToken);
        var active = await dmApplication.ActivateAsync(new ActivateEncounterCommand(
            Guid.NewGuid(), campaignId, created.Value.Id, withEnemy.Value!.Version), cancellationToken);

        var playerApplication = CreateApplication(CampaignRole.Player, repository, null);
        var projection = await playerApplication.GetActiveAsync(
            new GetActiveEncounterQuery(Guid.NewGuid(), campaignId), cancellationToken);
        var forbidden = await playerApplication.AdvanceAsync(new AdvanceTurnCommand(
            Guid.NewGuid(), campaignId, created.Value.Id, active.Value!.Version), cancellationToken);

        Assert.True(projection.IsSuccess);
        Assert.Equal("Exploradora", projection.Value!.Encounter!.CurrentParticipantName);
        Assert.Equal(2, projection.Value.Encounter.Participants.Count);
        Assert.Equal(8, projection.Value.Encounter.Participants.Single(item => item.Kind == "enemy").Quantity);
        Assert.DoesNotContain(typeof(ActiveParticipantDto).GetProperties(), property =>
            property.Name.Contains("Armor", StringComparison.Ordinal)
            || property.Name.Contains("HitPoints", StringComparison.Ordinal));
        Assert.False(forbidden.IsSuccess);
        Assert.Equal("combat.forbidden", forbidden.Error!.Code);
    }

    [Fact]
    public async Task Character_from_another_campaign_and_stale_version_are_conflicts_without_changes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var campaignId = Guid.NewGuid();
        var repository = new FakeRepository();
        var application = CreateApplication(CampaignRole.Dm, repository, null);
        var created = await application.CreateAsync(
            new CreateEncounterCommand(Guid.NewGuid(), campaignId, "Encuentro"), cancellationToken);

        var missing = await application.AddCharacterAsync(new AddCharacterCommand(
            Guid.NewGuid(), campaignId, created.Value!.Id, Guid.NewGuid(), 10, created.Value.Version), cancellationToken);
        var stale = await application.AddEnemyAsync(new AddEnemyCommand(
            Guid.NewGuid(), campaignId, created.Value.Id, "Adversario", 10, 12, 8, 1, 99), cancellationToken);

        Assert.Equal("combat.character_not_found", missing.Error!.Code);
        Assert.Equal("combat.stale_version", stale.Error!.Code);
        Assert.Empty(repository.Items.Single().Participants);
    }

    [Fact]
    public async Task Only_dm_deletes_non_active_encounters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var campaignId = Guid.NewGuid();
        var repository = new FakeRepository();
        var dmApplication = CreateApplication(CampaignRole.Dm, repository, null);
        var created = await dmApplication.CreateAsync(
            new CreateEncounterCommand(Guid.NewGuid(), campaignId, "Encuentro"), cancellationToken);
        var playerApplication = CreateApplication(CampaignRole.Player, repository, null);

        var forbidden = await playerApplication.DeleteAsync(new DeleteEncounterCommand(
            Guid.NewGuid(), campaignId, created.Value!.Id, created.Value.Version), cancellationToken);
        var deleted = await dmApplication.DeleteAsync(new DeleteEncounterCommand(
            Guid.NewGuid(), campaignId, created.Value.Id, created.Value.Version), cancellationToken);

        Assert.Equal("combat.forbidden", forbidden.Error!.Code);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(repository.Items);
    }

    private static EncounterApplication CreateApplication(
        CampaignRole role,
        FakeRepository repository,
        CombatCharacterSnapshot? character) => new(
            new FakeCampaignAccess(role),
            new FakeCharacters(character),
            repository,
            new FakeMetrics(),
            new FixedTimeProvider(Now));

    private sealed class FakeCampaignAccess(CampaignRole role) : ICampaignAccessReader
    {
        public Task<CampaignAccess> GetAccessAsync(
            Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CampaignAccess(true, role));
    }

    private sealed class FakeCharacters(CombatCharacterSnapshot? character) : ICombatCharacterReader
    {
        public Task<CombatCharacterSnapshot?> GetAsync(
            Guid campaignId, Guid characterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(character?.CharacterId == characterId ? character : null);
    }

    private sealed class FakeRepository : IEncounterRepository
    {
        public List<Encounter> Items { get; } = [];

        public Task<IReadOnlyList<Encounter>> ListAsync(
            Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Encounter>>(Items.Where(item => item.CampaignId == campaignId).ToArray());

        public Task<Encounter?> FindAsync(
            Guid campaignId, Guid encounterId, bool tracking,
            CancellationToken cancellationToken = default) => Task.FromResult(
                Items.SingleOrDefault(item => item.CampaignId == campaignId && item.Id == encounterId));

        public Task<Encounter?> FindActiveAsync(
            Guid campaignId, CancellationToken cancellationToken = default) => Task.FromResult(
                Items.SingleOrDefault(item => item.CampaignId == campaignId && item.Status == EncounterStatus.Active));

        public Task<bool> HasOtherActiveAsync(
            Guid campaignId, Guid encounterId, CancellationToken cancellationToken = default) => Task.FromResult(
                Items.Any(item => item.CampaignId == campaignId
                    && item.Id != encounterId
                    && item.Status == EncounterStatus.Active));

        public void Add(Encounter encounter) => Items.Add(encounter);

        public void Remove(Encounter encounter) => Items.Remove(encounter);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMetrics : ICombatMetrics
    {
        public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
        {
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
