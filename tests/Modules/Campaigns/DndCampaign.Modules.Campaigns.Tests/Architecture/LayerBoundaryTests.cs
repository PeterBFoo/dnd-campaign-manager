using DndCampaign.Modules.Campaigns;
using DndCampaign.Modules.Campaigns.Contracts.AdventureContent;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using Xunit;

namespace DndCampaign.Modules.Campaigns.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade_and_declared_contracts()
    {
        var exportedTypes = typeof(CampaignsModule).Assembly.GetExportedTypes();

        Assert.Equal(
            new[]
            {
                typeof(CampaignAdventureContent),
                typeof(CampaignAccess),
                typeof(CampaignRole),
                typeof(CampaignsModule),
                typeof(ICampaignAdventureContentReader),
                typeof(ICampaignAccessReader),
            }.OrderBy(type => type.FullName),
            exportedTypes.OrderBy(type => type.FullName));
    }
}
