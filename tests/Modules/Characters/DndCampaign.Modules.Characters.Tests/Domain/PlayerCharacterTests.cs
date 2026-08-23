using DndCampaign.Modules.Characters.Domain.Characters;
using Xunit;

namespace DndCampaign.Modules.Characters.Tests.Domain;

public sealed class PlayerCharacterTests
{
    [Fact]
    public void Unowned_character_can_never_start_active()
    {
        var character = PlayerCharacter.Create(Guid.NewGuid(), null, "Guardián", 17, 2,
            null, null, null, isActive: true, DateTimeOffset.UtcNow);

        Assert.Null(character.OwnerUserId);
        Assert.False(character.IsActive);
        Assert.Null(character.ImageObjectKey);
    }

    [Fact]
    public void Character_validates_game_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCharacter.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Exploradora", 41, 2, null, null, null, false, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCharacter.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Exploradora", 15, -21, null, null, null, false, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Removing_owner_deactivates_the_character()
    {
        var character = PlayerCharacter.Create(Guid.NewGuid(), Guid.NewGuid(), "Bardo", 13, 4,
            null, null, null, true, DateTimeOffset.UtcNow);

        character.Update("Bardo", 14, 5, null, null, null, null);

        Assert.False(character.IsActive);
        Assert.Null(character.OwnerUserId);
    }
}
