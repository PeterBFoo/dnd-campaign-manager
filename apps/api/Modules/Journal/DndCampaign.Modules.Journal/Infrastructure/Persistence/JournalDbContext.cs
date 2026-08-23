using DndCampaign.Modules.Journal.Domain.Entries;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Journal.Infrastructure.Persistence;

internal sealed class JournalDbContext(DbContextOptions<JournalDbContext> options) : DbContext(options)
{
    public DbSet<JournalEntry> Entries => Set<JournalEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("journal");
        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("journal_entries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.AuthorCharacterName).HasMaxLength(80);
            entity.Property(entry => entry.Content).HasMaxLength(5_000);
            entity.Property(entry => entry.PaginationSequence)
                .UseIdentityByDefaultColumn()
                .ValueGeneratedOnAdd();
            entity.HasIndex(entry => new { entry.CampaignId, entry.CreatedAt, entry.PaginationSequence });
            entity.HasIndex(entry => new { entry.CampaignId, entry.CreatedByUserId });
        });
    }
}
