using DndCampaign.Modules.Campaigns;
using Xunit;

namespace DndCampaign.Modules.Campaigns.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade()
    {
        var exportedTypes = typeof(CampaignsModule).Assembly.GetExportedTypes();

        Assert.Equal([typeof(CampaignsModule)], exportedTypes);
    }
}
