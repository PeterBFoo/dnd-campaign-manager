using DndCampaign.Modules.Journal;
using Xunit;

namespace DndCampaign.Modules.Journal.Tests.Architecture;

public sealed class LayerBoundaryTests
{
    [Fact]
    public void Public_surface_only_exposes_the_module_facade() =>
        Assert.Equal([typeof(JournalModule)], typeof(JournalModule).Assembly.GetExportedTypes());
}
