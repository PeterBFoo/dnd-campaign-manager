using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Journal.Application.Entries;
using DndCampaign.Modules.Journal.Application.Ports;
using DndCampaign.Modules.Journal.Domain.Entries;
using Xunit;

namespace DndCampaign.Modules.Journal.Tests.Application;

public sealed class JournalHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");

    [Fact]
    public async Task Player_with_active_character_creates_an_authored_entry()
    {
        var campaignId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var repository = new FakeRepository();
        var handler = new CreateJournalEntryHandler(
            new FakeCampaignAccess(CampaignRole.Player),
            new FakeActiveCharacters(new ActiveCharacterSnapshot(characterId, "Exploradora")),
            repository,
            new FakeMetrics(),
            new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new CreateJournalEntryCommand(userId, campaignId, "Una pista"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Exploradora", result.Value!.AuthorCharacterName);
        Assert.True(result.Value.CanEdit);
        Assert.True(result.Value.CanDelete);
        Assert.Equal(userId, repository.Items.Single().CreatedByUserId);
        Assert.Equal(Now, repository.Items.Single().CreatedAt);
    }

    [Fact]
    public async Task Another_player_can_edit_without_replacing_original_author()
    {
        var campaignId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var entry = JournalEntry.Create(
            campaignId, creatorId, Guid.NewGuid(), "Autora original", "Inicial", Now.AddMinutes(-5));
        var repository = new FakeRepository(entry);
        var handler = new UpdateJournalEntryHandler(
            new FakeCampaignAccess(CampaignRole.Player), repository, new FakeMetrics(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new UpdateJournalEntryCommand(editorId, campaignId, entry.Id, "Editada por otro jugador"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Autora original", result.Value!.AuthorCharacterName);
        Assert.True(result.Value.CanEdit);
        Assert.False(result.Value.CanDelete);
        Assert.Equal(creatorId, entry.CreatedByUserId);
    }

    [Fact]
    public async Task Another_player_cannot_delete_the_entry()
    {
        var campaignId = Guid.NewGuid();
        var entry = JournalEntry.Create(
            campaignId, Guid.NewGuid(), Guid.NewGuid(), "Autora", "Contenido", Now);
        var repository = new FakeRepository(entry);
        var handler = new DeleteJournalEntryHandler(
            new FakeCampaignAccess(CampaignRole.Player), repository, new FakeMetrics());

        var result = await handler.HandleAsync(
            new DeleteJournalEntryCommand(Guid.NewGuid(), campaignId, entry.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("journal.forbidden", result.Error!.Code);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task Dm_lists_entries_as_read_only()
    {
        var campaignId = Guid.NewGuid();
        var entry = JournalEntry.Create(
            campaignId, Guid.NewGuid(), Guid.NewGuid(), "Autora", "Contenido", Now);
        var repository = new FakeRepository(entry);
        var handler = new ListJournalEntriesHandler(
            new FakeCampaignAccess(CampaignRole.Dm), repository, new FakeCursorCodec(), new FakeMetrics());

        var result = await handler.HandleAsync(
            new ListJournalEntriesQuery(Guid.NewGuid(), campaignId, null, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Items.Single().CanEdit);
        Assert.False(result.Value.Items.Single().CanDelete);
    }

    private sealed class FakeCampaignAccess(CampaignRole? role, bool exists = true) : ICampaignAccessReader
    {
        public Task<CampaignAccess> GetAccessAsync(
            Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CampaignAccess(exists, role));
    }

    private sealed class FakeActiveCharacters(ActiveCharacterSnapshot? active) : IActiveCharacterReader
    {
        public Task<ActiveCharacterSnapshot?> GetActiveAsync(
            Guid campaignId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(active);
    }

    private sealed class FakeRepository(params JournalEntry[] entries) : IJournalEntryRepository
    {
        public List<JournalEntry> Items { get; } = [.. entries];

        public Task<IReadOnlyList<JournalEntry>> ListPageAsync(
            Guid campaignId, JournalPageCursor? cursor, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JournalEntry>>(Items
                .Where(entry => entry.CampaignId == campaignId)
                .OrderByDescending(entry => entry.CreatedAt)
                .Take(take)
                .ToArray());

        public Task<JournalEntry?> FindForUpdateAsync(
            Guid campaignId, Guid entryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(entry =>
                entry.CampaignId == campaignId && entry.Id == entryId));

        public void Add(JournalEntry entry) => Items.Add(entry);

        public void Delete(JournalEntry entry) => Items.Remove(entry);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMetrics : IJournalMetrics
    {
        public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
        {
        }
    }

    private sealed class FakeCursorCodec : IJournalCursorCodec
    {
        public string Encode(JournalPageCursor cursor) => "next";

        public bool TryDecode(string value, out JournalPageCursor? cursor)
        {
            cursor = null;
            return false;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
