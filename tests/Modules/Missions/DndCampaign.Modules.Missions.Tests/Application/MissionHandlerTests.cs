using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Missions.Application.Missions;
using DndCampaign.Modules.Missions.Application.Ports;
using DndCampaign.Modules.Missions.Domain.Missions;
using Xunit;

namespace DndCampaign.Modules.Missions.Tests.Application;

public sealed class MissionHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");

    [Fact]
    public async Task Player_with_active_character_creates_an_authored_mission_without_dates()
    {
        var campaignId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var repository = new FakeRepository();
        var handler = new CreateMissionHandler(
            new FakeCampaignAccess(CampaignRole.Player),
            new FakeActiveCharacters(new ActiveCharacterSnapshot(characterId, "Exploradora")),
            repository,
            new FakeMetrics(),
            new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new CreateMissionCommand(userId, campaignId, "Objetivo", "Descripción", true),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Exploradora", result.Value!.AuthorDisplayName);
        Assert.Equal("player", result.Value.AuthorType);
        Assert.True(result.Value.IsMain);
        Assert.True(result.Value.CanDelete);
        Assert.Equal(Now, repository.Items.Single().CreatedAt);
    }

    [Fact]
    public async Task Dm_creates_without_reading_an_active_character()
    {
        var repository = new FakeRepository();
        var activeCharacters = new FakeActiveCharacters(null);
        var handler = new CreateMissionHandler(
            new FakeCampaignAccess(CampaignRole.Dm), activeCharacters, repository,
            new FakeMetrics(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new CreateMissionCommand(Guid.NewGuid(), Guid.NewGuid(), "Objetivo", null, false),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dirección de campaña", result.Value!.AuthorDisplayName);
        Assert.Equal(0, activeCharacters.Calls);
    }

    [Fact]
    public async Task Another_player_edits_but_cannot_delete_and_dm_can_delete()
    {
        var campaignId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var mission = Mission.CreateForPlayer(
            campaignId, creatorId, Guid.NewGuid(), "Autora", "Objetivo", null, false, Now.AddMinutes(-2));
        var repository = new FakeRepository(mission);
        var update = new UpdateMissionHandler(
            new FakeCampaignAccess(CampaignRole.Player), repository,
            new FakeMetrics(), new FixedTimeProvider(Now));

        var updated = await update.HandleAsync(
            new UpdateMissionCommand(otherId, campaignId, mission.Id, "Objetivo común", null, "completed"),
            TestContext.Current.CancellationToken);
        var playerDelete = await new DeleteMissionHandler(
            new FakeCampaignAccess(CampaignRole.Player), repository, new FakeMetrics()).HandleAsync(
                new DeleteMissionCommand(otherId, campaignId, mission.Id),
                TestContext.Current.CancellationToken);
        var dmDelete = await new DeleteMissionHandler(
            new FakeCampaignAccess(CampaignRole.Dm), repository, new FakeMetrics()).HandleAsync(
                new DeleteMissionCommand(Guid.NewGuid(), campaignId, mission.Id),
                TestContext.Current.CancellationToken);

        Assert.True(updated.IsSuccess);
        Assert.False(updated.Value!.CanDelete);
        Assert.False(playerDelete.IsSuccess);
        Assert.Equal("missions.forbidden", playerDelete.Error!.Code);
        Assert.True(dmDelete.IsSuccess);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Player_without_active_character_cannot_create()
    {
        var handler = new CreateMissionHandler(
            new FakeCampaignAccess(CampaignRole.Player), new FakeActiveCharacters(null),
            new FakeRepository(), new FakeMetrics(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new CreateMissionCommand(Guid.NewGuid(), Guid.NewGuid(), "Objetivo", null, false),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("missions.active_character_required", result.Error!.Code);
    }

    private sealed class FakeCampaignAccess(CampaignRole? role, bool exists = true) : ICampaignAccessReader
    {
        public Task<CampaignAccess> GetAccessAsync(
            Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CampaignAccess(exists, role));
    }

    private sealed class FakeActiveCharacters(ActiveCharacterSnapshot? active) : IActiveCharacterReader
    {
        public int Calls { get; private set; }

        public Task<ActiveCharacterSnapshot?> GetActiveAsync(
            Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(active);
        }
    }

    private sealed class FakeRepository(params Mission[] missions) : IMissionRepository
    {
        public List<Mission> Items { get; } = [.. missions];

        public Task<IReadOnlyList<Mission>> ListAsync(
            Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Mission>>(Items.Where(mission => mission.CampaignId == campaignId).ToArray());

        public Task<Mission?> FindForUpdateAsync(
            Guid campaignId, Guid missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(mission =>
                mission.CampaignId == campaignId && mission.Id == missionId));

        public void Add(Mission mission) => Items.Add(mission);

        public void Delete(Mission mission) => Items.Remove(mission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsMainAsync(
            Guid campaignId, Mission mission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMetrics : IMissionMetrics
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
