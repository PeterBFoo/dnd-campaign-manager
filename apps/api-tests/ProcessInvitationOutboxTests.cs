using DndCampaign.Api.Application;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DndCampaign.Api.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class ProcessInvitationOutboxTests
{
    [Fact]
    public async Task ProcessNext_sends_without_sql_transaction_then_persists_processed_and_sent_together()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationStore>();
            var outbox = scope.ServiceProvider.GetRequiredService<IInvitationOutboxStore>();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessInvitationOutbox>();
            var probe = scope.ServiceProvider.GetRequiredService<TransactionProbeEmailSender>();
            var protector = scope.ServiceProvider.GetRequiredService<InvitationTokenProtector>();
            var now = DateTimeOffset.UtcNow;

            var issued = await SeedPendingOutboxAsync(
                database,
                invitations,
                outbox,
                protector,
                now,
                cancellationToken);

            var processed = await processor.ProcessNextAsync(cancellationToken);

            Assert.True(processed);
            Assert.False(probe.SendOccurredWithOpenTransaction);
            Assert.Equal("provider-message-id", probe.LastProviderMessageId);

            var invitation = await database.Invitations.AsNoTracking().SingleAsync(cancellationToken);
            Assert.NotNull(invitation.LastSentAt);
            Assert.Equal(1, invitation.SendCount);
            Assert.Equal(issued.Invitation.Id, invitation.Id);

            var message = await database.InvitationOutbox.AsNoTracking().SingleAsync(cancellationToken);
            Assert.NotNull(message.ProcessedAt);
            Assert.Equal("provider-message-id", message.ProviderMessageId);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task ProcessNext_discards_expired_invitation_and_outbox_together()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationStore>();
            var outbox = scope.ServiceProvider.GetRequiredService<IInvitationOutboxStore>();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessInvitationOutbox>();
            var protector = scope.ServiceProvider.GetRequiredService<InvitationTokenProtector>();
            var now = DateTimeOffset.UtcNow;

            var issued = await SeedPendingOutboxAsync(
                database,
                invitations,
                outbox,
                protector,
                now,
                cancellationToken);
            var record = await database.Invitations.SingleAsync(cancellationToken);
            database.Entry(record).Property(nameof(InvitationRecord.ExpiresAt)).CurrentValue = now.AddDays(-1);
            await database.SaveChangesAsync(cancellationToken);

            var processed = await processor.ProcessNextAsync(cancellationToken);

            Assert.True(processed);
            var invitation = await database.Invitations.AsNoTracking().SingleAsync(
                candidate => candidate.Id == issued.Invitation.Id,
                cancellationToken);
            Assert.Equal(InvitationStatus.Expired, invitation.Status);
            Assert.Null(invitation.LastSentAt);

            var message = await database.InvitationOutbox.AsNoTracking().SingleAsync(cancellationToken);
            Assert.NotNull(message.ProcessedAt);
            Assert.Equal("discarded", message.ProviderMessageId);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task ProcessNext_provider_failure_does_not_update_last_sent_at()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!, failingSender: true);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationStore>();
            var outbox = scope.ServiceProvider.GetRequiredService<IInvitationOutboxStore>();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessInvitationOutbox>();
            var protector = scope.ServiceProvider.GetRequiredService<InvitationTokenProtector>();
            var now = DateTimeOffset.UtcNow;

            await SeedPendingOutboxAsync(
                database,
                invitations,
                outbox,
                protector,
                now,
                cancellationToken);

            var processed = await processor.ProcessNextAsync(cancellationToken);

            Assert.True(processed);
            var invitation = await database.Invitations.AsNoTracking().SingleAsync(cancellationToken);
            Assert.Null(invitation.LastSentAt);
            Assert.Equal(0, invitation.SendCount);

            var message = await database.InvitationOutbox.AsNoTracking().SingleAsync(cancellationToken);
            Assert.Null(message.ProcessedAt);
            Assert.Equal("provider_failure", message.LastErrorCode);
            Assert.Equal(1, message.Attempts);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    [Fact]
    public async Task ProcessNext_rolls_back_processed_when_mark_sent_fails()
    {
        var connectionString = PostgreSqlIntegrationTestHelper.RequireTestConnectionString();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var provider = BuildServiceProvider(connectionString!, throwOnMarkSent: true);
        await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
            var invitations = scope.ServiceProvider.GetRequiredService<IInvitationStore>();
            var outbox = scope.ServiceProvider.GetRequiredService<IInvitationOutboxStore>();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessInvitationOutbox>();
            var protector = scope.ServiceProvider.GetRequiredService<InvitationTokenProtector>();
            var now = DateTimeOffset.UtcNow;

            await SeedPendingOutboxAsync(
                database,
                invitations,
                outbox,
                protector,
                now,
                cancellationToken);

            await Assert.ThrowsAsync<IOException>(() => processor.ProcessNextAsync(cancellationToken));

            var invitation = await database.Invitations.AsNoTracking().SingleAsync(cancellationToken);
            Assert.Null(invitation.LastSentAt);

            var message = await database.InvitationOutbox.AsNoTracking().SingleAsync(cancellationToken);
            Assert.Null(message.ProcessedAt);
            Assert.Null(message.ProviderMessageId);
        }
        finally
        {
            await PostgreSqlIntegrationTestHelper.ResetDatabaseAsync(provider, cancellationToken);
        }
    }

    private static async Task<IssuedInvitation> SeedPendingOutboxAsync(
        CampaignDbContext database,
        IInvitationStore invitations,
        IInvitationOutboxStore outbox,
        InvitationTokenProtector protector,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var issuer = UserAccount.Create("admin@example.com", "Admin", isPlatformAdmin: true, now);
        issuer.SetPasswordHash("hash");
        database.Users.Add(issuer);
        await database.SaveChangesAsync(cancellationToken);

        var issued = Invitation.IssuePlatform("player@example.com", issuer.Id, now);
        await invitations.AddAsync(issued.Invitation, cancellationToken);
        await outbox.EnqueueAsync(
            issued.Invitation.Id,
            protector.Protect(issued.Token),
            now,
            cancellationToken);
        return issued;
    }

    private static ServiceProvider BuildServiceProvider(
        string connectionString,
        bool failingSender = false,
        bool throwOnMarkSent = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CampaignDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<InvitationStore>();
        if (throwOnMarkSent)
        {
            services.AddScoped<IInvitationStore>(provider =>
                new ThrowingMarkSentInvitationStore(provider.GetRequiredService<InvitationStore>()));
        }
        else
        {
            services.AddScoped<IInvitationStore>(provider => provider.GetRequiredService<InvitationStore>());
        }

        services.AddScoped<IInvitationOutboxStore, InvitationOutboxStore>();
        services.AddScoped<ITransactionalBoundary, SerializableTransactionalBoundary>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(IdentitySecurityTestsHelper.CreateOptions());
        services.AddSingleton<InvitationTokenProtector>();
        services.AddSingleton<InvitationEmailComposer>();
        if (failingSender)
        {
            services.AddSingleton<ITransactionalEmailSender, FailingEmailSender>();
        }
        else
        {
            services.AddScoped<TransactionProbeEmailSender>();
            services.AddScoped<ITransactionalEmailSender>(provider =>
                provider.GetRequiredService<TransactionProbeEmailSender>());
        }

        services.AddScoped<ProcessInvitationOutbox>();
        return services.BuildServiceProvider();
    }

    private sealed class TransactionProbeEmailSender(CampaignDbContext database) : ITransactionalEmailSender
    {
        public bool SendOccurredWithOpenTransaction { get; private set; }

        public string? LastProviderMessageId { get; private set; }

        public Task<TransactionalEmailReceipt> SendAsync(
            TransactionalEmail email,
            CancellationToken cancellationToken = default)
        {
            SendOccurredWithOpenTransaction = database.Database.CurrentTransaction is not null;
            LastProviderMessageId = "provider-message-id";
            return Task.FromResult(new TransactionalEmailReceipt(LastProviderMessageId));
        }
    }

    private sealed class FailingEmailSender : ITransactionalEmailSender
    {
        public Task<TransactionalEmailReceipt> SendAsync(
            TransactionalEmail email,
            CancellationToken cancellationToken = default) =>
            throw new TransactionalEmailDeliveryException("provider down");
    }

    private sealed class ThrowingMarkSentInvitationStore(IInvitationStore inner) : IInvitationStore
    {
        public Task<bool> HasPendingAsync(
            InvitationKind kind,
            Guid? campaignId,
            string recipientEmail,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            inner.HasPendingAsync(kind, campaignId, recipientEmail, now, cancellationToken);

        public Task<Invitation?> FindByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken) =>
            inner.FindByTokenHashAsync(tokenHash, cancellationToken);

        public Task<Invitation?> FindByIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken) =>
            inner.FindByIdAsync(invitationId, cancellationToken);

        public Task<Invitation?> FindByIdAsync(
            Guid invitationId,
            InvitationKind kind,
            Guid? campaignId,
            CancellationToken cancellationToken) =>
            inner.FindByIdAsync(invitationId, kind, campaignId, cancellationToken);

        public Task<IReadOnlyList<DateTimeOffset>> ListRecentIssueTimesAsync(
            InvitationKind kind,
            Guid? campaignId,
            string recipientEmail,
            DateTimeOffset since,
            CancellationToken cancellationToken) =>
            inner.ListRecentIssueTimesAsync(kind, campaignId, recipientEmail, since, cancellationToken);

        public Task<IReadOnlyList<InvitationListItem>> ListAsync(
            InvitationKind kind,
            Guid? campaignId,
            CancellationToken cancellationToken) =>
            inner.ListAsync(kind, campaignId, cancellationToken);

        public Task AddAsync(Invitation invitation, CancellationToken cancellationToken) =>
            inner.AddAsync(invitation, cancellationToken);

        public Task SaveAsync(Invitation invitation, CancellationToken cancellationToken) =>
            inner.SaveAsync(invitation, cancellationToken);

        public Task SaveAllAsync(
            IReadOnlyCollection<Invitation> invitations,
            CancellationToken cancellationToken) =>
            inner.SaveAllAsync(invitations, cancellationToken);

        public Task MarkSentAsync(Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new IOException("forced mark-sent failure");
    }
}
