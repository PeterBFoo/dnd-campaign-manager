using DndCampaign.Modules.Characters;
using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Characters.Contracts.CombatParticipants;
using Xunit;

namespace DndCampaign.Modules.Characters.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade_and_active_character_contract() =>
        Assert.Equal(
            [
                typeof(ActiveCharacterSnapshot),
                typeof(CharactersModule),
                typeof(CombatCharacterSnapshot),
                typeof(IActiveCharacterReader),
                typeof(ICombatCharacterReader),
            ],
            typeof(CharactersModule).Assembly.GetExportedTypes().OrderBy(type => type.Name));
}
