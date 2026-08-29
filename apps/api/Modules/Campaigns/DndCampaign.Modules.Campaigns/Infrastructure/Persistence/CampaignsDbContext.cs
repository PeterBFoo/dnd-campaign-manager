using DndCampaign.Modules.Campaigns.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Campaigns.Infrastructure.Persistence;

internal sealed class CampaignsDbContext(DbContextOptions<CampaignsDbContext> options) : DbContext(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("campaigns");
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("campaigns", table => table.HasCheckConstraint(
                "CK_campaigns_version", "\"Version\" >= 1"));
            entity.HasKey(campaign => campaign.Id);
            entity.Property(campaign => campaign.Name).HasMaxLength(100);
            entity.Property(campaign => campaign.Version).IsConcurrencyToken();
            entity.HasQueryFilter(campaign => campaign.DeletedAt == null);
            entity.HasIndex(campaign => campaign.DmUserId);
            entity.HasIndex(campaign => campaign.AdventureModuleId);
        });
    }
}
