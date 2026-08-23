using DndCampaign.Modules.Journal.Domain.Entries;
using Xunit;

namespace DndCampaign.Modules.Journal.Tests.Domain;

public sealed class JournalEntryTests
{
    [Fact]
    public void Create_normalizes_content_and_preserves_original_author_on_update()
    {
        var campaignId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-08-23T10:00:00Z");
        var entry = JournalEntry.Create(
            campaignId, creatorId, characterId, " Exploradora ", " Pista inicial\n ", createdAt);

        entry.UpdateContent("Contenido compartido", createdAt.AddMinutes(5));

        Assert.Equal("Exploradora", entry.AuthorCharacterName);
        Assert.Equal("Contenido compartido", entry.Content);
        Assert.Equal(campaignId, entry.CampaignId);
        Assert.Equal(creatorId, entry.CreatedByUserId);
        Assert.Equal(characterId, entry.AuthorCharacterId);
        Assert.Equal(createdAt, entry.CreatedAt);
        Assert.Equal(createdAt.AddMinutes(5), entry.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Content_is_required(string content)
    {
        Assert.Throws<ArgumentException>(() => JournalEntry.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Personaje", content, DateTimeOffset.UtcNow));
    }
}
