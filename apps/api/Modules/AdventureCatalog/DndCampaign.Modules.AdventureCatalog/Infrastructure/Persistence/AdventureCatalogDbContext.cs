using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureCatalogDbContext(DbContextOptions<AdventureCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<AdventureModule> AdventureModules => Set<AdventureModule>();

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
    }

    private static void ConfigureProvenance(
        Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<AdventureModule, EditorialProvenance> owned,
        string prefix)
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
