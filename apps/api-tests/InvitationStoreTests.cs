using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DndCampaign.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class InvitationStoreTests
{
    [Fact]
    public async Task FindByTokenHash_returns_the_persisted_invitation()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var (_, issued) = await SeedPendingInvitationAsync(database, now, cancellationToken);
            var tokenHash = Convert.ToHexString(issued.Invitation.TokenHash.Span);

            var found = await invitations.FindByTokenHashAsync(tokenHash, cancellationToken);

            Assert.NotNull(found);
            Assert.Equal(issued.Invitation.Id, found.Id);
            Assert.Equal(issued.Invitation.RecipientEmail, found.RecipientEmail);
            Assert.Equal(InvitationStatus.Pending, found.Status);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task Enqueue_then_TryClaimNext_returns_the_pending_message()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IInvitationOutboxStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var (invitationId, _) = await SeedPendingInvitationAsync(database, now, cancellationToken);
            await outbox.EnqueueAsync(invitationId, "encrypted-token", now, cancellationToken);

            var claimed = await outbox.TryClaimNextAsync(now, cancellationToken);

            Assert.NotNull(claimed);
            Assert.Equal(invitationId, claimed.InvitationId);
            Assert.Equal("encrypted-token", claimed.EncryptedToken);

            var row = await database.InvitationOutbox.AsNoTracking().SingleAsync(cancellationToken);
            Assert.NotNull(row.LeaseUntil);
            Assert.True(row.LeaseUntil > now);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task GetDeliveryStatuses_maps_pending_sent_discarded_and_failed()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IInvitationOutboxStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var pendingId = (await SeedPendingInvitationAsync(database, now, cancellationToken)).Id;
            var sentId = (await SeedPendingInvitationAsync(database, now, cancellationToken, "sent@example.com")).Id;
            var discardedId = (await SeedPendingInvitationAsync(database, now, cancellationToken, "discarded@example.com")).Id;
            var failedId = (await SeedPendingInvitationAsync(database, now, cancellationToken, "failed@example.com")).Id;

            await outbox.EnqueueAsync(pendingId, "pending-token", now, cancellationToken);

            var sentMessage = InvitationOutboxMessage.Create(sentId, "sent-token", now);
            sentMessage.MarkProcessed("provider-id", now);
            database.InvitationOutbox.Add(sentMessage);

            var discardedMessage = InvitationOutboxMessage.Create(discardedId, "discarded-token", now);
            discardedMessage.MarkDiscarded(now);
            database.InvitationOutbox.Add(discardedMessage);

            var failedMessage = InvitationOutboxMessage.Create(failedId, "failed-token", now);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                failedMessage.MarkFailed("provider_failure", now);
            }

            database.InvitationOutbox.Add(failedMessage);
            await database.SaveChangesAsync(cancellationToken);

            var statuses = await outbox.GetDeliveryStatusesAsync(
                [pendingId, sentId, discardedId, failedId],
                cancellationToken);

            Assert.Equal(InvitationDeliveryStatus.Pending, statuses[pendingId]);
            Assert.Equal(InvitationDeliveryStatus.Sent, statuses[sentId]);
            Assert.Equal(InvitationDeliveryStatus.Discarded, statuses[discardedId]);
            Assert.Equal(InvitationDeliveryStatus.Failed, statuses[failedId]);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task SaveAllAsync_persists_every_invitation_with_one_save_changes()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;
        var interceptor = new CountingSaveChangesInterceptor();

        await using var provider = BuildServiceProvider(connectionString!, interceptor);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationStore>();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var now = DateTimeOffset.UtcNow;

            var first = await SeedPendingInvitationAsync(database, now, cancellationToken);
            var second = await SeedPendingInvitationAsync(database, now, cancellationToken, "second@example.com");
            database.Entry(await database.Invitations.SingleAsync(row => row.Id == first.Id, cancellationToken))
                .Property(nameof(InvitationRecord.ExpiresAt)).CurrentValue = now.AddDays(-1);
            database.Entry(await database.Invitations.SingleAsync(row => row.Id == second.Id, cancellationToken))
                .Property(nameof(InvitationRecord.ExpiresAt)).CurrentValue = now.AddDays(-1);
            await database.SaveChangesAsync(cancellationToken);

            var pending = await invitations.ListAsync(InvitationKind.Platform, campaignId: null, cancellationToken);
            var expired = pending
                .Select(item => item.Invitation)
                .Where(invitation => invitation.Expire(now))
                .ToArray();
            interceptor.Reset();

            await invitations.SaveAllAsync(expired, cancellationToken);

            Assert.Equal(1, interceptor.SaveCount);
            var rows = await database.Invitations.AsNoTracking().ToListAsync(cancellationToken);
            Assert.All(rows, row => Assert.Equal(InvitationStatus.Expired, row.Status));
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    private static async Task<(Guid Id, IssuedInvitation Issued)> SeedPendingInvitationAsync(
        CampaignDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string email = "player@example.com")
    {
        var issuer = await database.Users.SingleOrDefaultAsync(cancellationToken);
        if (issuer is null)
        {
            issuer = UserAccount.Create("admin@example.com", "Admin", isPlatformAdmin: true, now);
            issuer.SetPasswordHash("hash");
            database.Users.Add(issuer);
            await database.SaveChangesAsync(cancellationToken);
        }

        var issued = Invitation.IssuePlatform(email, issuer.Id, now);
        database.Invitations.Add(InvitationRecord.FromIssued(issued, issuer.Id));
        await database.SaveChangesAsync(cancellationToken);
        return (issued.Invitation.Id, issued);
    }

    private static ServiceProvider BuildServiceProvider(
        string connectionString,
        CountingSaveChangesInterceptor? interceptor = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CampaignDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
        });
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<IInvitationStore, InvitationStore>();
        services.AddScoped<IInvitationOutboxStore, InvitationOutboxStore>();
        return services.BuildServiceProvider();
    }
}

internal sealed class CountingSaveChangesInterceptor : SaveChangesInterceptor
{
    public int SaveCount { get; private set; }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return new ValueTask<int>(result);
    }

    public void Reset() => SaveCount = 0;
}
