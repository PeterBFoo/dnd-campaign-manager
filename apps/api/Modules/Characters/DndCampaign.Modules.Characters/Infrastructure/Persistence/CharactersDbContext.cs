using DndCampaign.Modules.Characters.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Characters.Infrastructure.Persistence;

internal sealed class CharactersDbContext(DbContextOptions<CharactersDbContext> options) : DbContext(options)
{
    public DbSet<PlayerCharacter> Characters => Set<PlayerCharacter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("characters");
        modelBuilder.Entity<PlayerCharacter>(entity =>
        {
            entity.ToTable("characters");
            entity.HasKey(character => character.Id);
            entity.Property(character => character.Name).HasMaxLength(80);
            entity.Property(character => character.ImageObjectKey).HasMaxLength(512);
            entity.Property(character => character.ImageContentType).HasMaxLength(32);
            entity.HasIndex(character => character.CampaignId);
            entity.HasIndex(character => new { character.CampaignId, character.OwnerUserId });
            entity.HasIndex(character => new { character.CampaignId, character.OwnerUserId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"OwnerUserId\" IS NOT NULL");
        });
    }
}
