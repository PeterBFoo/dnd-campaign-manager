using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

namespace DndCampaign.Modules.AdventureCatalog.Tests.Domain;

public sealed class AdventureModuleTests
{
    private static EditorialProvenance Provenance(string source = "") => EditorialProvenance.Create(
        string.IsNullOrEmpty(source) ? EditorialOriginKind.Original : EditorialOriginKind.Licensed,
        string.IsNullOrEmpty(source) ? null : source,
        "Autoría o licencia verificada",
        null,
        DateTimeOffset.UtcNow,
        Guid.NewGuid());

    [Fact]
    public void Normalizes_name_and_starts_at_version_one()
    {
        var module = AdventureModule.Create(Guid.NewGuid(), "  Módulo del Bosque  ", "Descripción", Provenance(), null, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal("Módulo del Bosque", module.Name);
        Assert.Equal("MÓDULO DEL BOSQUE", module.NormalizedName);
        Assert.Equal(1, module.Version);
    }

    [Fact]
    public void Requires_source_for_non_original_content()
    {
        Assert.Throws<ArgumentException>(() => EditorialProvenance.Create(EditorialOriginKind.Licensed, null, "licencia", null, DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Update_is_optimistic_and_removing_cover_clears_its_provenance()
    {
        var module = AdventureModule.Create(Guid.NewGuid(), "Bosque", null, Provenance(), AdventureModuleCover.Create("cover/a.png", "image/png", 10), Provenance("https://source.example/cover"), Guid.NewGuid(), DateTimeOffset.UtcNow);
        module.Update("Bosque renovado", "Nueva descripción", Provenance(), null, null, true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Null(module.Cover);
        Assert.Null(module.CoverProvenance);
        Assert.Equal(2, module.Version);
    }
}
