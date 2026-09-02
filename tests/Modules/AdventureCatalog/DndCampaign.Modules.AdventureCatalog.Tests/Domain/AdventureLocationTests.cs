using DndCampaign.Modules.AdventureCatalog.Domain.Locations;
using Xunit;

namespace DndCampaign.Modules.AdventureCatalog.Tests.Domain;

public sealed class AdventureLocationTests
{
    [Fact]
    public void Location_accepts_poi_without_position_and_normalizes_text()
    {
        var now = DateTimeOffset.Parse("2026-09-02T12:00:00Z"); var actor = Guid.NewGuid();
        var location = AdventureLocation.Create(Guid.NewGuid(), Guid.NewGuid(), "  Villa  ", "  Descripción  ", actor, now);
        var point = location.AddPoint(Guid.NewGuid(), "  Entrada  ", "  POI  ", null, null, actor, now);
        Assert.Equal("Villa", location.Name); Assert.Equal("Descripción", location.Description); Assert.Equal("Entrada", point.Name); Assert.False(point.HasPosition);
    }

    [Fact]
    public void Position_requires_detail_map_and_normalized_coordinates()
    {
        var now = DateTimeOffset.UtcNow; var actor = Guid.NewGuid(); var location = AdventureLocation.Create(Guid.NewGuid(), Guid.NewGuid(), "Villa", null, actor, now);
        Assert.Throws<ArgumentException>(() => location.AddPoint(Guid.NewGuid(), "Entrada", null, .2m, .3m, actor, now));
        location.SetDetailMap(Guid.NewGuid(), actor, now.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => location.AddPoint(Guid.NewGuid(), "Entrada", null, 1.1m, .3m, actor, now.AddMinutes(2)));
        var point = location.AddPoint(Guid.NewGuid(), "Entrada", null, .2m, .3m, actor, now.AddMinutes(2));
        Assert.Equal(.2m, point.X); Assert.Equal(.3m, point.Y);
    }

    [Fact]
    public void Changing_detail_map_clears_existing_poi_positions_but_keeps_points()
    {
        var now = DateTimeOffset.UtcNow; var actor = Guid.NewGuid(); var location = AdventureLocation.Create(Guid.NewGuid(), Guid.NewGuid(), "Villa", null, actor, now);
        location.SetDetailMap(Guid.NewGuid(), actor, now.AddMinutes(1));
        var point = location.AddPoint(Guid.NewGuid(), "Entrada", null, .2m, .3m, actor, now.AddMinutes(2));
        location.SetDetailMap(Guid.NewGuid(), actor, now.AddMinutes(3));
        Assert.Single(location.PointsOfInterest); Assert.Null(point.X); Assert.Null(point.Y);
    }

    [Fact]
    public void Placement_is_unique_per_map_and_can_be_updated_without_duplicates()
    {
        var now = DateTimeOffset.UtcNow; var actor = Guid.NewGuid(); var location = AdventureLocation.Create(Guid.NewGuid(), Guid.NewGuid(), "Villa", null, actor, now); var map = Guid.NewGuid();
        Assert.True(location.SetPlacement(map, .1m, .2m, actor, now.AddMinutes(1)));
        Assert.True(location.SetPlacement(map, .7m, .8m, actor, now.AddMinutes(2)));
        Assert.Single(location.Placements); Assert.Equal(.7m, location.Placements.Single().X);
    }
}
