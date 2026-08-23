using DndCampaign.Modules.Access.Domain.Accounts;
using DndCampaign.Modules.Access.Domain.CampaignAccess;
using DndCampaign.Modules.Access.Domain.Invitations;
using DndCampaign.Modules.Access.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<CampaignMembership> CampaignMemberships => Set<CampaignMembership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<InvitationOutboxMessage> InvitationOutbox => Set<InvitationOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("access");

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.DisplayName).HasMaxLength(80);
            entity.Property(user => user.PasswordHash).HasMaxLength(512);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.TokenHash).HasMaxLength(64);
            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.HasIndex(session => new { session.UserId, session.ExpiresAt });
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CampaignMembership>(entity =>
        {
            entity.ToTable("campaign_memberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(membership => new { membership.CampaignId, membership.UserId }).IsUnique();
            entity.HasIndex(membership => new { membership.CampaignId, membership.Role });
            entity.HasIndex(membership => membership.CampaignId)
                .IsUnique()
                .HasFilter("\"Role\" = 'Dm'");
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations");
            entity.HasKey(invitation => invitation.Id);
            entity.Property(invitation => invitation.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(invitation => invitation.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(invitation => invitation.RecipientEmail).HasMaxLength(320);
            entity.Property(invitation => invitation.TokenHash).HasMaxLength(64);
            entity.HasIndex(invitation => invitation.TokenHash).IsUnique();
            entity.HasIndex(invitation => new
            {
                invitation.Kind,
                invitation.CampaignId,
                invitation.RecipientEmail,
                invitation.IssuedAt,
            });
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(invitation => invitation.IssuedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(invitation => invitation.AcceptedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvitationOutboxMessage>(entity =>
        {
            entity.ToTable("invitation_outbox");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.EncryptedToken).HasMaxLength(1024);
            entity.Property(message => message.ProviderMessageId).HasMaxLength(256);
            entity.Property(message => message.LastErrorCode).HasMaxLength(64);
            entity.HasIndex(message => new { message.ProcessedAt, message.NextAttemptAt });
            entity.HasOne<Invitation>()
                .WithMany()
                .HasForeignKey(message => message.InvitationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
