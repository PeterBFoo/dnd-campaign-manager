using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureCatalogDbContext(DbContextOptions<AdventureCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<AdventureModule> AdventureModules => Set<AdventureModule>();
    public DbSet<AdventureChapter> AdventureChapters => Set<AdventureChapter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("adventure_catalog");
        modelBuilder.Entity<AdventureModule>(entity =>
        {
            entity.ToTable("adventure_modules", table =>
            {
                table.HasCheckConstraint("CK_adventure_modules_version", "\"Version\" >= 1");
                table.HasCheckConstraint(
                    "CK_adventure_modules_cover_shape",
                    "(\"CoverObjectKey\" IS NULL AND \"CoverContentType\" IS NULL AND \"CoverSizeBytes\" IS NULL) OR "
                    + "(\"CoverObjectKey\" IS NOT NULL AND \"CoverContentType\" IS NOT NULL AND \"CoverSizeBytes\" BETWEEN 1 AND 10485760)");
            });
            entity.HasKey(module => module.Id);
            entity.Property(module => module.Id).ValueGeneratedNever();
            entity.Property(module => module.Name).HasMaxLength(120);
            entity.Property(module => module.NormalizedName).HasMaxLength(120);
            entity.Property(module => module.Description).HasMaxLength(5000);
            entity.Property(module => module.Version).IsConcurrencyToken();
            entity.Property(module => module.ChaptersVersion).IsConcurrencyToken();
            entity.HasIndex(module => module.NormalizedName).IsUnique();
            entity.HasIndex(module => new { module.UpdatedAt, module.Id });

            ConfigureProvenance(entity.OwnsOne(module => module.TextProvenance), "Text");
            entity.Navigation(module => module.TextProvenance).IsRequired();

            entity.OwnsOne(module => module.Cover, cover =>
            {
                cover.Property(value => value.ObjectKey).HasColumnName("CoverObjectKey").HasMaxLength(512);
                cover.Property(value => value.ContentType).HasColumnName("CoverContentType").HasMaxLength(32);
                cover.Property(value => value.SizeBytes).HasColumnName("CoverSizeBytes");
            });

            ConfigureProvenance(entity.OwnsOne(module => module.CoverProvenance), "Cover");
        });

        modelBuilder.Entity<AdventureChapter>(entity =>
        {
            entity.ToTable("adventure_chapters", table =>
            {
                table.HasCheckConstraint("CK_adventure_chapters_position", "\"Position\" >= 1");
                table.HasCheckConstraint("CK_adventure_chapters_version", "\"Version\" >= 1");
            });
            entity.HasKey(chapter => chapter.Id);
            entity.Property(chapter => chapter.Id).ValueGeneratedNever();
            entity.Property(chapter => chapter.Name).HasMaxLength(120);
            entity.Property(chapter => chapter.Description).HasMaxLength(20000);
            entity.Property(chapter => chapter.Version).IsConcurrencyToken();
            entity.HasIndex(chapter => new { chapter.ModuleId, chapter.Position }).IsUnique();
            entity.HasOne<AdventureModule>().WithMany().HasForeignKey(chapter => chapter.ModuleId).OnDelete(DeleteBehavior.Cascade);
            ConfigureProvenance(entity.OwnsOne(chapter => chapter.Provenance), "Chapter");
            entity.Navigation(chapter => chapter.Provenance).IsRequired();
        });
    }

    private static void ConfigureProvenance<TOwner>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<TOwner, EditorialProvenance> owned,
        string prefix)
        where TOwner : class
    {
        owned.Property(value => value.OriginKind).HasColumnName($"{prefix}OriginKind");
        owned.Property(value => value.SourceReference)
            .HasColumnName($"{prefix}SourceReference").HasMaxLength(2000);
        owned.Property(value => value.RightsBasis)
            .HasColumnName($"{prefix}RightsBasis").HasMaxLength(2000);
        owned.Property(value => value.Attribution)
            .HasColumnName($"{prefix}Attribution").HasMaxLength(2000);
        owned.Property(value => value.VerifiedAt).HasColumnName($"{prefix}VerifiedAt");
        owned.Property(value => value.VerifiedByUserId).HasColumnName($"{prefix}VerifiedByUserId");
    }
}
