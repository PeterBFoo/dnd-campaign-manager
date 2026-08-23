using DndCampaign.Modules.Combat.Domain.Encounters;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Combat.Infrastructure.Persistence;

internal sealed class CombatDbContext(DbContextOptions<CombatDbContext> options) : DbContext(options)
{
    public DbSet<Encounter> Encounters => Set<Encounter>();

    public DbSet<EncounterParticipant> Participants => Set<EncounterParticipant>();

    public DbSet<EnemyGroupMember> EnemyGroupMembers => Set<EnemyGroupMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("combat");
        modelBuilder.Entity<Encounter>(entity =>
        {
            entity.ToTable("encounters", table =>
            {
                table.HasCheckConstraint("CK_encounters_status", "\"Status\" BETWEEN 0 AND 2");
                table.HasCheckConstraint(
                    "CK_encounters_lifecycle",
                    "(\"Status\" = 0 AND \"Round\" IS NULL AND \"CurrentParticipantId\" IS NULL AND \"ActivatedAt\" IS NULL AND \"FinishedAt\" IS NULL) OR "
                    + "(\"Status\" = 1 AND \"Round\" >= 1 AND \"CurrentParticipantId\" IS NOT NULL AND \"ActivatedAt\" IS NOT NULL AND \"FinishedAt\" IS NULL) OR "
                    + "(\"Status\" = 2 AND \"Round\" >= 1 AND \"CurrentParticipantId\" IS NOT NULL AND \"ActivatedAt\" IS NOT NULL AND \"FinishedAt\" IS NOT NULL)");
                table.HasCheckConstraint("CK_encounters_version", "\"Version\" >= 1");
            });
            entity.HasKey(encounter => encounter.Id);
            entity.Property(encounter => encounter.Id).ValueGeneratedNever();
            entity.Property(encounter => encounter.Name).HasMaxLength(120);
            entity.Property(encounter => encounter.Version).IsConcurrencyToken();
            entity.HasIndex(encounter => new { encounter.CampaignId, encounter.Status, encounter.CreatedAt });
            entity.HasIndex(encounter => new { encounter.CampaignId, encounter.Status })
                .IsUnique()
                .HasFilter("\"Status\" = 1");
            entity.HasMany(encounter => encounter.Participants)
                .WithOne()
                .HasForeignKey(participant => participant.EncounterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(encounter => encounter.Participants)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<EncounterParticipant>(entity =>
        {
            entity.ToTable("encounter_participants", table =>
            {
                table.HasCheckConstraint("CK_encounter_participants_kind", "\"Kind\" BETWEEN 0 AND 1");
                table.HasCheckConstraint("CK_encounter_participants_armor", "\"ArmorClass\" BETWEEN 0 AND 40");
                table.HasCheckConstraint("CK_encounter_participants_initiative", "\"InitiativeTotal\" BETWEEN -20 AND 30");
                table.HasCheckConstraint("CK_encounter_participants_order", "\"OrderPosition\" >= 0 AND \"CreatedOrder\" >= 1");
                table.HasCheckConstraint(
                    "CK_encounter_participants_shape",
                    "(\"Kind\" = 0 AND \"SourceCharacterId\" IS NOT NULL) OR "
                    + "(\"Kind\" = 1 AND \"SourceCharacterId\" IS NULL)");
            });
            entity.HasKey(participant => participant.Id);
            entity.Property(participant => participant.Id).ValueGeneratedNever();
            entity.Property(participant => participant.NameSnapshot).HasMaxLength(80);
            entity.HasIndex(participant => new { participant.EncounterId, participant.OrderPosition }).IsUnique();
            entity.HasIndex(participant => new { participant.EncounterId, participant.SourceCharacterId })
                .IsUnique()
                .HasFilter("\"SourceCharacterId\" IS NOT NULL");
            entity.HasIndex(participant => new { participant.EncounterId, participant.CreatedOrder }).IsUnique();
            entity.HasMany(participant => participant.EnemyMembers)
                .WithOne()
                .HasForeignKey(member => member.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(participant => participant.EnemyMembers)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<EnemyGroupMember>(entity =>
        {
            entity.ToTable("enemy_group_members", table =>
            {
                table.HasCheckConstraint("CK_enemy_group_members_ordinal", "\"Ordinal\" BETWEEN 1 AND 100");
                table.HasCheckConstraint(
                    "CK_enemy_group_members_hit_points",
                    "\"MaximumHitPoints\" BETWEEN 1 AND 100000 AND \"CurrentHitPoints\" BETWEEN 0 AND \"MaximumHitPoints\"");
            });
            entity.HasKey(member => member.Id);
            entity.Property(member => member.Id).ValueGeneratedNever();
            entity.HasIndex(member => new { member.ParticipantId, member.Ordinal }).IsUnique();
        });
    }
}
