using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Application.Characters;
using DndCampaign.Modules.Characters.Application.Ports;
using DndCampaign.Modules.Characters.Domain.Characters;
using Xunit;

namespace DndCampaign.Modules.Characters.Tests.Application;

public sealed class CharacterHandlerTests
{
    [Fact]
    public async Task Player_creates_an_owned_first_active_character()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeRepository();
        var handler = CreateHandler(CampaignRole.Player, userId, repository);

        var result = await handler.HandleAsync(new CreateCharacterCommand(
            userId, Guid.NewGuid(), "Exploradora", 16, 3, null, null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value!.OwnerUserId);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task Dm_can_create_an_unowned_character()
    {
        var dmId = Guid.NewGuid();
        var repository = new FakeRepository();
        var handler = CreateHandler(CampaignRole.Dm, dmId, repository);

        var result = await handler.HandleAsync(new CreateCharacterCommand(
            dmId, Guid.NewGuid(), "Aliado", 12, 0, null, null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.OwnerUserId);
        Assert.False(result.Value.IsActive);
    }

    [Fact]
    public async Task Player_cannot_update_another_players_character_but_dm_can()
    {
        var ownerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var character = PlayerCharacter.Create(campaignId, ownerId, "Guerrero", 18, 1,
            null, null, null, true, DateTimeOffset.UtcNow);
        var repository = new FakeRepository();
        repository.Items.Add(character);
        var players = new FakePlayers([new CampaignPlayer(ownerId, "Propietario")]);

        var denied = await new UpdateCharacterHandler(
            new FakeAccess(CampaignRole.Player), players, repository, new FakeImages(), new FakeMetrics())
            .HandleAsync(new UpdateCharacterCommand(actorId, campaignId, character.Id, "Cambiado", 10, 0,
                null, false, null), TestContext.Current.CancellationToken);
        var allowed = await new UpdateCharacterHandler(
            new FakeAccess(CampaignRole.Dm), players, repository, new FakeImages(), new FakeMetrics())
            .HandleAsync(new UpdateCharacterCommand(actorId, campaignId, character.Id, "Cambiado", 10, 0,
                ownerId, false, null), TestContext.Current.CancellationToken);

        Assert.Equal("character.forbidden", denied.Error!.Code);
        Assert.True(allowed.IsSuccess);
        Assert.Equal("Cambiado", character.Name);
    }

    private static CreateCharacterHandler CreateHandler(
        CampaignRole role, Guid playerId, FakeRepository repository) => new(
        new FakeAccess(role),
        new FakePlayers(role == CampaignRole.Player ? [new CampaignPlayer(playerId, "Jugador")] : []),
        repository,
        new FakeImages(),
        new FakeMetrics(),
        TimeProvider.System);

    private sealed class FakeAccess(CampaignRole role) : ICampaignAccessReader
    {
        public Task<CampaignAccess> GetAccessAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CampaignAccess(true, role));
    }

    private sealed class FakePlayers(IReadOnlyList<CampaignPlayer> items) : ICampaignPlayerReader
    {
        public Task<IReadOnlyList<CampaignPlayer>> ListPlayersAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult(items);
    }

    private sealed class FakeRepository : ICharacterRepository
    {
        public List<PlayerCharacter> Items { get; } = [];
        public Task<IReadOnlyList<PlayerCharacter>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerCharacter>>(Items.Where(item => item.CampaignId == campaignId).ToArray());
        public Task<bool> HasAnyOwnedAsync(Guid campaignId, Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(item => item.CampaignId == campaignId && item.OwnerUserId == ownerUserId));
        public void Add(PlayerCharacter character) => Items.Add(character);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PlayerCharacter?> FindForUpdateAsync(Guid campaignId, Guid characterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.CampaignId == campaignId && item.Id == characterId));
        public Task<PlayerCharacter?> FindAsync(Guid campaignId, Guid characterId, CancellationToken cancellationToken = default) =>
            FindForUpdateAsync(campaignId, characterId, cancellationToken);
        public Task SaveOwnerChangeAsync(PlayerCharacter character, Guid? previousOwnerUserId, bool wasActive, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PlayerCharacter?> ActivateOwnedAsync(Guid campaignId, Guid ownerUserId, Guid characterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.CampaignId == campaignId && item.OwnerUserId == ownerUserId && item.Id == characterId));
        public Task DeleteAsync(PlayerCharacter character, CancellationToken cancellationToken = default)
        {
            Items.Remove(character);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeImages : ICharacterImageStore
    {
        public Task<StoredCharacterImage> StoreAsync(Guid campaignId, Guid characterId, CharacterImageUpload upload, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<CharacterImageContent?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<CharacterImageContent?>(null);
        public Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMetrics : ICharacterMetrics
    {
        public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds) { }
    }
}
