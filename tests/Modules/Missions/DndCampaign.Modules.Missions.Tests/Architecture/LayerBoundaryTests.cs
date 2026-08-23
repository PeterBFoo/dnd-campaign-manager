using DndCampaign.Modules.Missions;
using Xunit;

namespace DndCampaign.Modules.Missions.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade() =>
        Assert.Equal([typeof(MissionsModule)], typeof(MissionsModule).Assembly.GetExportedTypes());
}
