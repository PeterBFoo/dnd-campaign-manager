using DndCampaign.Modules.Combat;
using Xunit;

namespace DndCampaign.Modules.Combat.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade() =>
        Assert.Equal([typeof(CombatModule)], typeof(CombatModule).Assembly.GetExportedTypes());
}
