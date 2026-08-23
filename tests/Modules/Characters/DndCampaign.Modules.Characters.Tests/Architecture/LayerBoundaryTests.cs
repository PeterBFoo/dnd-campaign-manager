using DndCampaign.Modules.Characters;
using Xunit;

namespace DndCampaign.Modules.Characters.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade() =>
        Assert.Equal([typeof(CharactersModule)], typeof(CharactersModule).Assembly.GetExportedTypes());
}
