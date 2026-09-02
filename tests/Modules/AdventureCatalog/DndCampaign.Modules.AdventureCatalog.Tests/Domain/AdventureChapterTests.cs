using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;

namespace DndCampaign.Modules.AdventureCatalog.Tests.Domain;

public sealed class AdventureChapterTests
{
    [Fact]
    public void Create_normalizes_plain_text_and_preserves_position()
    {
        var now = DateTimeOffset.Parse("2026-08-30T12:00:00Z");
        var actor = Guid.NewGuid();
        var chapter = AdventureChapter.Create(Guid.NewGuid(), Guid.NewGuid(), "  Primer capítulo  ",
            "  Descripción genérica  ", 1, Provenance(actor, now), actor, now);

        Assert.Equal("Primer capítulo", chapter.Name);
        Assert.Equal("Descripción genérica", chapter.Description);
        Assert.Equal(1, chapter.Position);
        Assert.Equal(1, chapter.Version);
    }

    [Fact]
    public void Create_rejects_invalid_name_description_and_position()
    {
        var now = DateTimeOffset.UtcNow; var actor = Guid.NewGuid(); var module = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => AdventureChapter.Create(Guid.NewGuid(), module, "x", null, 1, Provenance(actor, now), actor, now));
        Assert.Throws<ArgumentException>(() => AdventureChapter.Create(Guid.NewGuid(), module, "Válido", new string('x', 20001), 1, Provenance(actor, now), actor, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => AdventureChapter.Create(Guid.NewGuid(), module, "Válido", null, 0, Provenance(actor, now), actor, now));
    }

    [Fact]
    public void Update_keeps_identity_and_position_while_advancing_version()
    {
        var now = DateTimeOffset.UtcNow; var actor = Guid.NewGuid();
        var chapter = AdventureChapter.Create(Guid.NewGuid(), Guid.NewGuid(), "Nombre inicial", null, 2, Provenance(actor, now), actor, now);
        var id = chapter.Id;
        chapter.Update("Nombre editado", "Texto", Provenance(actor, now.AddMinutes(1)), actor, now.AddMinutes(1));
        Assert.Equal(id, chapter.Id); Assert.Equal(2, chapter.Position); Assert.Equal(2, chapter.Version);
    }

    private static EditorialProvenance Provenance(Guid actor, DateTimeOffset now) =>
        EditorialProvenance.Create(EditorialOriginKind.Original, null, "Contenido original verificable.", null, now, actor);
}
