using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Domain.Chapters;
using DndCampaign.Modules.AdventureCatalog.Domain.Locations;
using DndCampaign.Modules.AdventureCatalog.Domain.Maps;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence;

internal sealed class AdventureCatalogDbContext(DbContextOptions<AdventureCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<AdventureModule> AdventureModules => Set<AdventureModule>();
    public DbSet<AdventureMap> AdventureMaps => Set<AdventureMap>();
    public DbSet<AdventureChapter> AdventureChapters => Set<AdventureChapter>();
    public DbSet<AdventureLocation> AdventureLocations => Set<AdventureLocation>();
    public DbSet<AdventurePointOfInterest> AdventurePointsOfInterest => Set<AdventurePointOfInterest>();
    public DbSet<AdventureLocationPlacement> AdventureLocationPlacements => Set<AdventureLocationPlacement>();
    public DbSet<AdventureLocationChapter> AdventureLocationChapters => Set<AdventureLocationChapter>();

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
            entity.HasAlternateKey(chapter => new { chapter.ModuleId, chapter.Id });
            entity.HasOne<AdventureModule>().WithMany().HasForeignKey(chapter => chapter.ModuleId).OnDelete(DeleteBehavior.Cascade);
            ConfigureProvenance(entity.OwnsOne(chapter => chapter.Provenance), "Chapter");
            entity.Navigation(chapter => chapter.Provenance).IsRequired();
        });

        modelBuilder.Entity<AdventureMap>(entity =>
        {
            entity.ToTable("adventure_maps", table => table.HasCheckConstraint("CK_adventure_maps_version", "\"Version\" >= 1"));
            entity.HasKey(map => map.Id);
            entity.Property(map => map.Id).ValueGeneratedNever();
            entity.Property(map => map.Name).HasMaxLength(120);
            entity.Property(map => map.Description).HasMaxLength(10000);
            entity.Property(map => map.Version).IsConcurrencyToken();
            entity.HasIndex(map => new { map.ModuleId, map.UpdatedAt });
            entity.HasOne<AdventureModule>().WithMany().HasForeignKey(map => map.ModuleId).OnDelete(DeleteBehavior.Cascade);
            entity.OwnsOne(map => map.Image, image =>
            {
                image.Property(value => value.ObjectKey).HasColumnName("ImageObjectKey").HasMaxLength(512);
                image.Property(value => value.ContentType).HasColumnName("ImageContentType").HasMaxLength(32);
                image.Property(value => value.SizeBytes).HasColumnName("ImageSizeBytes");
                image.Property(value => value.Width).HasColumnName("ImageWidth");
                image.Property(value => value.Height).HasColumnName("ImageHeight");
            });
            ConfigureProvenance(entity.OwnsOne(map => map.ImageProvenance), "Image");
            entity.HasMany(map => map.Chapters).WithOne().HasForeignKey(link => link.MapId).OnDelete(DeleteBehavior.Cascade);
            entity.HasAlternateKey(map => new { map.ModuleId, map.Id });
        });

        modelBuilder.Entity<AdventureMapChapter>(entity =>
        {
            entity.ToTable("adventure_map_chapters");
            entity.HasKey(link => new { link.MapId, link.ChapterId });
            entity.HasOne<AdventureChapter>().WithMany().HasForeignKey(link => link.ChapterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdventureLocation>(entity =>
        {
            entity.ToTable("adventure_locations", table =>
            {
                table.HasCheckConstraint("CK_adventure_locations_version", "\"Version\" >= 1");
                table.HasCheckConstraint("CK_adventure_locations_detail_map_pair", "(\"DetailMapId\" IS NULL AND \"DetailMapModuleId\" IS NULL) OR (\"DetailMapId\" IS NOT NULL AND \"DetailMapModuleId\" IS NOT NULL)");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Name).HasMaxLength(120);
            entity.Property(item => item.Description).HasMaxLength(10000);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.ModuleId, item.UpdatedAt });
            entity.HasAlternateKey(item => new { item.ModuleId, item.Id });
            entity.HasOne<AdventureModule>().WithMany().HasForeignKey(item => item.ModuleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AdventureMap>().WithMany().HasForeignKey(item => new { item.DetailMapModuleId, item.DetailMapId }).HasPrincipalKey(item => new { item.ModuleId, item.Id }).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(item => item.PointsOfInterest).WithOne().HasForeignKey(item => new { item.ModuleId, item.LocationId }).HasPrincipalKey(item => new { item.ModuleId, item.Id }).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Placements).WithOne().HasForeignKey(item => new { item.ModuleId, item.LocationId }).HasPrincipalKey(item => new { item.ModuleId, item.Id }).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Chapters).WithOne().HasForeignKey(item => new { item.ModuleId, item.LocationId }).HasPrincipalKey(item => new { item.ModuleId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdventurePointOfInterest>(entity =>
        {
            entity.ToTable("adventure_points_of_interest", table =>
            {
                table.HasCheckConstraint("CK_adventure_points_of_interest_version", "\"Version\" >= 1");
                table.HasCheckConstraint("CK_adventure_points_of_interest_coordinates", "(\"X\" IS NULL AND \"Y\" IS NULL) OR (\"X\" IS NOT NULL AND \"Y\" IS NOT NULL AND \"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1)");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Name).HasMaxLength(120);
            entity.Property(item => item.Description).HasMaxLength(5000);
            entity.Property(item => item.X).HasPrecision(18, 15);
            entity.Property(item => item.Y).HasPrecision(18, 15);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => new { item.ModuleId, item.LocationId });
        });

        modelBuilder.Entity<AdventureLocationPlacement>(entity =>
        {
            entity.ToTable("adventure_location_placements", table =>
            {
                table.HasCheckConstraint("CK_adventure_location_placements_coordinates", "\"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1");
            });
            entity.HasKey(item => new { item.MapId, item.LocationId });
            entity.Property(item => item.X).HasPrecision(18, 15);
            entity.Property(item => item.Y).HasPrecision(18, 15);
            entity.HasOne<AdventureMap>().WithMany().HasForeignKey(item => new { item.ModuleId, item.MapId }).HasPrincipalKey(item => new { item.ModuleId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdventureLocationChapter>(entity =>
        {
            entity.ToTable("adventure_location_chapters");
            entity.HasKey(item => new { item.LocationId, item.ChapterId });
            entity.HasOne<AdventureChapter>().WithMany().HasForeignKey(item => new { item.ModuleId, item.ChapterId }).HasPrincipalKey(item => new { item.ModuleId, item.Id }).OnDelete(DeleteBehavior.Cascade);
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
