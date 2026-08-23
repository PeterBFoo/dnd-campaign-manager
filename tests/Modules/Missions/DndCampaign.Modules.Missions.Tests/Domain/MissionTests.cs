using DndCampaign.Modules.Missions.Domain.Missions;
using Xunit;

namespace DndCampaign.Modules.Missions.Tests.Domain;

public sealed class MissionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");

    [Fact]
    public void Player_mission_normalizes_content_and_preserves_author_on_update()
    {
        var campaignId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var mission = Mission.CreateForPlayer(
            campaignId, creatorId, characterId, " Exploradora ",
            " Objetivo compartido ", " Descripción\n ", true, Now);

        mission.Update("Objetivo actualizado", null, MissionStatus.Completed, Now.AddMinutes(5));

        Assert.Equal("Objetivo actualizado", mission.Title);
        Assert.Null(mission.Description);
        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.False(mission.IsMain);
        Assert.Equal(characterId, mission.AuthorCharacterId);
        Assert.Equal("Exploradora", mission.AuthorCharacterName);
        Assert.Equal(creatorId, mission.CreatedByUserId);
        Assert.Equal(Now, mission.CreatedAt);
    }

    [Fact]
    public void Dm_mission_has_no_character_and_closed_mission_cannot_be_main()
    {
        var mission = Mission.CreateForDm(
            Guid.NewGuid(), Guid.NewGuid(), "Misión genérica", null, false, Now);
        mission.Update("Misión genérica", null, MissionStatus.Cancelled, Now.AddMinutes(1));

        Assert.Equal(MissionAuthorType.Dm, mission.AuthorType);
        Assert.Null(mission.AuthorCharacterId);
        Assert.Throws<InvalidOperationException>(() => mission.MarkAsMain(Now.AddMinutes(2)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("x")]
    public void Title_requires_between_two_and_120_characters(string title)
    {
        Assert.Throws<ArgumentException>(() => Mission.CreateForDm(
            Guid.NewGuid(), Guid.NewGuid(), title, null, false, Now));
    }
}
