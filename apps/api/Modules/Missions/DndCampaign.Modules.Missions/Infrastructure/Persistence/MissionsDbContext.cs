using DndCampaign.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionsDbContext(DbContextOptions<MissionsDbContext> options) : DbContext(options)
{
    public DbSet<Mission> Missions => Set<Mission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("missions");
        modelBuilder.Entity<Mission>(entity =>
        {
            entity.ToTable("missions", table =>
            {
                table.HasCheckConstraint(
                    "CK_missions_author",
                    "(\"AuthorType\" = 0 AND \"AuthorCharacterId\" IS NULL AND \"AuthorCharacterName\" IS NULL) OR "
                    + "(\"AuthorType\" = 1 AND \"AuthorCharacterId\" IS NOT NULL AND \"AuthorCharacterName\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_missions_main_active",
                    "NOT \"IsMain\" OR \"Status\" = 0");
            });
            entity.HasKey(mission => mission.Id);
            entity.Property(mission => mission.AuthorCharacterName).HasMaxLength(80);
            entity.Property(mission => mission.Title).HasMaxLength(120);
            entity.Property(mission => mission.Description).HasMaxLength(5_000);
            entity.Property(mission => mission.SortSequence)
                .UseIdentityByDefaultColumn()
                .ValueGeneratedOnAdd();
            entity.HasIndex(mission => mission.CampaignId);
            entity.HasIndex(mission => new { mission.CampaignId, mission.CreatedByUserId });
            entity.HasIndex(mission => new { mission.CampaignId, mission.IsMain })
                .IsUnique()
                .HasFilter("\"IsMain\" = TRUE");
            entity.HasIndex(mission => new
            {
                mission.CampaignId,
                mission.Status,
                mission.CreatedAt,
                mission.SortSequence,
            });
        });
    }
}
