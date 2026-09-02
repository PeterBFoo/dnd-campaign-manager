using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;
using Xunit;

namespace DndCampaign.Modules.AdventureCatalog.Tests.Domain;

public sealed class AdventureMapTests
{
    [Fact]
    public void Map_normalizes_text_and_keeps_chapter_links_unique()
    {
        var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var map = AdventureMap.Create(Guid.NewGuid(), Guid.NewGuid(), "  Bosque antiguo  ", "  Senderos  ", actor, now);
        var chapter = Guid.NewGuid();
        Assert.True(map.AddChapter(chapter, actor, now.AddMinutes(1)));
        Assert.False(map.AddChapter(chapter, actor, now.AddMinutes(2)));
        Assert.Equal("Bosque antiguo", map.Name); Assert.Equal("Senderos", map.Description);
        Assert.Single(map.Chapters); Assert.Equal(2, map.Version);
    }

    [Fact]
    public void Replacing_and_removing_image_preserves_map_and_links()
    {
        var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var map = AdventureMap.Create(Guid.NewGuid(), Guid.NewGuid(), "Mapa regional", null, actor, now);
        map.AddChapter(Guid.NewGuid(), actor, now);
        var provenance = EditorialProvenance.Create(EditorialOriginKind.Original, null, "Creación propia", null, now, actor);
        map.SetImage(AdventureMapImage.Create("key", "image/png", 100, 10, 10), provenance, actor, now);
        map.RemoveImage(actor, now);
        Assert.Null(map.Image); Assert.Null(map.ImageProvenance); Assert.Single(map.Chapters);
    }

    [Theory]
    [InlineData(1, 120)]
    [InlineData(121, 0)]
    public void Map_rejects_invalid_name_lengths(int length, int unused)
    {
        _ = unused;
        Assert.Throws<ArgumentException>(() => AdventureMap.Create(Guid.NewGuid(), Guid.NewGuid(), new string('x', length), null, Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Image_rejects_more_than_fifty_megapixels() =>
        Assert.Throws<ArgumentException>(() => AdventureMapImage.Create("key", "image/png", 100, 10000, 5001));
}
