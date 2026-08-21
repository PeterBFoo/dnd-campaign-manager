using DndCampaign.Api.Application;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DndCampaign.Api.Tests;

public sealed class InvitationApplicationFakeTests
{
    [Fact]
    public async Task Accept_persists_new_user_session_and_invitation()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var harness = Harness.Create(now);
        var issued = Invitation.IssuePlatform("player@example.com", harness.Admin.Id, now);
        harness.World.Invitations.Add(Clone(issued.Invitation));

        var result = await harness.Acceptance.AcceptAsync(
            new AcceptInvitationCommand(issued.Token, "Invited Player", "A-valid-player-password-123!"),
            new AuthenticatedActor(false, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(AcceptInvitationStatus.Accepted, result.Status);
        Assert.NotNull(result.Outcome?.AccessToken);
        Assert.Equal(InvitationStatus.Accepted, harness.World.Invitations.Single().Status);
        Assert.Equal(2, harness.World.Users.Count);
        Assert.Single(harness.World.Sessions);
    }

    [Fact]
    public async Task Accept_unauthorized_rolls_back_and_leaves_invitation_pending()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var harness = Harness.Create(now);
        var issued = Invitation.IssuePlatform(harness.Admin.Email, harness.Admin.Id, now);
        harness.World.Invitations.Add(Clone(issued.Invitation));

        var result = await harness.Acceptance.AcceptAsync(
            new AcceptInvitationCommand(issued.Token, null, null),
            new AuthenticatedActor(false, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(AcceptInvitationStatus.Unauthorized, result.Status);
        Assert.Equal(InvitationStatus.Pending, harness.World.Invitations.Single().Status);
        Assert.Single(harness.World.Users);
        Assert.Empty(harness.World.Sessions);
    }

    [Fact]
    public async Task ProcessInvitationOutbox_discards_expired_work()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var harness = Harness.Create(now);
        var issued = Invitation.IssuePlatform("player@example.com", harness.Admin.Id, now);
        harness.World.Invitations.Add(Clone(issued.Invitation, expiresAt: now.AddMinutes(-1)));
        harness.Enqueue(issued, now);

        var processed = await harness.Processor.ProcessNextAsync(TestContext.Current.CancellationToken);

        Assert.True(processed);
        Assert.Equal(InvitationStatus.Expired, harness.World.Invitations.Single().Status);
        Assert.Equal("discarded", harness.World.Outbox.Single().ProviderMessageId);
        Assert.Null(harness.World.LastSentAt.GetValueOrDefault(issued.Invitation.Id));
        Assert.Equal(0, harness.Sender.SendCount);
    }

    [Fact]
    public async Task ProcessInvitationOutbox_provider_failure_does_not_mark_sent()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var harness = Harness.Create(now);
        harness.Sender.Fail = true;
        var issued = Invitation.IssuePlatform("player@example.com", harness.Admin.Id, now);
        harness.World.Invitations.Add(Clone(issued.Invitation));
        harness.Enqueue(issued, now);

        var processed = await harness.Processor.ProcessNextAsync(TestContext.Current.CancellationToken);

        Assert.True(processed);
        Assert.Null(harness.World.LastSentAt.GetValueOrDefault(issued.Invitation.Id));
        Assert.Equal("provider_failure", harness.World.Outbox.Single().LastErrorCode);
        Assert.Equal(1, harness.World.Outbox.Single().Attempts);
        Assert.Null(harness.World.Outbox.Single().ProcessedAt);
    }

    [Fact]
    public async Task ProcessInvitationOutbox_processed_and_mark_sent_are_atomic()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var harness = Harness.Create(now);
        var issued = Invitation.IssuePlatform("player@example.com", harness.Admin.Id, now);
        harness.World.Invitations.Add(Clone(issued.Invitation));
        harness.Enqueue(issued, now);

        var processed = await harness.Processor.ProcessNextAsync(TestContext.Current.CancellationToken);

        Assert.True(processed);
        Assert.Equal(now, harness.World.LastSentAt[issued.Invitation.Id]);
        Assert.Equal("provider-message-id", harness.World.Outbox.Single().ProviderMessageId);
        Assert.NotNull(harness.World.Outbox.Single().ProcessedAt);
    }

    [Fact]
    public async Task ProcessInvitationOutbox_rolls_back_processed_when_mark_sent_fails()
    {
        var now = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        var harness = Harness.Create(now);
        harness.Invitations.ThrowOnMarkSent = true;
        var issued = Invitation.IssuePlatform("player@example.com", harness.Admin.Id, now);
        harness.World.Invitations.Add(Clone(issued.Invitation));
        harness.Enqueue(issued, now);

        await Assert.ThrowsAsync<IOException>(() =>
            harness.Processor.ProcessNextAsync(TestContext.Current.CancellationToken));

        Assert.False(harness.World.LastSentAt.ContainsKey(issued.Invitation.Id));
        Assert.Null(harness.World.Outbox.Single().ProcessedAt);
        Assert.Null(harness.World.Outbox.Single().ProviderMessageId);
    }

    private static Invitation Clone(Invitation invitation, DateTimeOffset? expiresAt = null) =>
        Invitation.Restore(
            invitation.Id,
            invitation.Kind,
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.TokenHash.ToArray(),
            invitation.IssuedByUserId,
            invitation.IssuedAt,
            expiresAt ?? invitation.ExpiresAt,
            invitation.Status,
            invitation.AcceptedByUserId,
            invitation.AcceptedAt,
            invitation.RevokedAt);

    private sealed class Harness
    {
        private Harness(
            InMemoryInvitationWorld world,
            UserAccount admin,
            InvitationAcceptanceService acceptance,
            ProcessInvitationOutbox processor,
            InMemoryInvitationStore invitations,
            RecordingEmailSender sender,
            InvitationTokenProtector protector)
        {
            World = world;
            Admin = admin;
            Acceptance = acceptance;
            Processor = processor;
            Invitations = invitations;
            Sender = sender;
            Protector = protector;
        }

        public InMemoryInvitationWorld World { get; }

        public UserAccount Admin { get; }

        public InvitationAcceptanceService Acceptance { get; }

        public ProcessInvitationOutbox Processor { get; }

        public InMemoryInvitationStore Invitations { get; }

        public RecordingEmailSender Sender { get; }

        public InvitationTokenProtector Protector { get; }

        public void Enqueue(IssuedInvitation issued, DateTimeOffset now) =>
            World.Outbox.Add(new MemoryOutboxMessage
            {
                Id = Guid.NewGuid(),
                InvitationId = issued.Invitation.Id,
                EncryptedToken = Protector.Protect(issued.Token),
                CreatedAt = now,
                NextAttemptAt = now,
            });

        public static Harness Create(DateTimeOffset now)
        {
            var world = new InMemoryInvitationWorld();
            var admin = UserAccount.Create("admin@example.com", "Admin", isPlatformAdmin: true, now);
            admin.SetPasswordHash("hash");
            world.Users.Add(admin);

            var identity = new InMemoryIdentityStore(world);
            var invitations = new InMemoryInvitationStore(world);
            var outbox = new InMemoryInvitationOutboxStore(world);
            var transactions = new InMemoryTransactionalBoundary(world);
            var options = IdentitySecurityTestsHelper.CreateOptions();
            var clock = new FixedTimeProvider(now);
            var protector = new InvitationTokenProtector(options);
            var sender = new RecordingEmailSender();

            var acceptance = new InvitationAcceptanceService(
                invitations,
                identity,
                transactions,
                new PasswordHasher<UserAccount>(),
                clock);
            var processor = new ProcessInvitationOutbox(
                invitations,
                outbox,
                transactions,
                protector,
                new InvitationEmailComposer(options),
                sender,
                clock,
                NullLogger<ProcessInvitationOutbox>.Instance);

            return new Harness(world, admin, acceptance, processor, invitations, sender, protector);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingEmailSender : ITransactionalEmailSender
    {
        public bool Fail { get; set; }

        public int SendCount { get; private set; }

        public Task<TransactionalEmailReceipt> SendAsync(
            TransactionalEmail email,
            CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                throw new TransactionalEmailDeliveryException("provider down");
            }

            SendCount++;
            return Task.FromResult(new TransactionalEmailReceipt("provider-message-id"));
        }
    }
}

internal sealed class InMemoryInvitationWorld
{
    public List<UserAccount> Users { get; } = [];

    public List<UserSession> Sessions { get; } = [];

    public List<CampaignMembership> Memberships { get; } = [];

    public List<Invitation> Invitations { get; } = [];

    public Dictionary<Guid, DateTimeOffset?> LastSentAt { get; private set; } = [];

    public List<MemoryOutboxMessage> Outbox { get; } = [];

    public InMemoryInvitationWorld Snapshot()
    {
        var copy = new InMemoryInvitationWorld();
        copy.Users.AddRange(Users);
        copy.Sessions.AddRange(Sessions);
        copy.Memberships.AddRange(Memberships);
        copy.Invitations.AddRange(Invitations.Select(CloneInvitation));
        copy.LastSentAt = new Dictionary<Guid, DateTimeOffset?>(LastSentAt);
        copy.Outbox.AddRange(Outbox.Select(message => message.Clone()));
        return copy;
    }

    public void Restore(InMemoryInvitationWorld snapshot)
    {
        Users.Clear();
        Users.AddRange(snapshot.Users);
        Sessions.Clear();
        Sessions.AddRange(snapshot.Sessions);
        Memberships.Clear();
        Memberships.AddRange(snapshot.Memberships);
        Invitations.Clear();
        Invitations.AddRange(snapshot.Invitations);
        LastSentAt = new Dictionary<Guid, DateTimeOffset?>(snapshot.LastSentAt);
        Outbox.Clear();
        Outbox.AddRange(snapshot.Outbox);
    }

    private static Invitation CloneInvitation(Invitation invitation) =>
        Invitation.Restore(
            invitation.Id,
            invitation.Kind,
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.TokenHash.ToArray(),
            invitation.IssuedByUserId,
            invitation.IssuedAt,
            invitation.ExpiresAt,
            invitation.Status,
            invitation.AcceptedByUserId,
            invitation.AcceptedAt,
            invitation.RevokedAt);
}

internal sealed class MemoryOutboxMessage
{
    public Guid Id { get; init; }

    public Guid InvitationId { get; init; }

    public string EncryptedToken { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? LeaseUntil { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? LastErrorCode { get; set; }

    public MemoryOutboxMessage Clone() =>
        new()
        {
            Id = Id,
            InvitationId = InvitationId,
            EncryptedToken = EncryptedToken,
            CreatedAt = CreatedAt,
            NextAttemptAt = NextAttemptAt,
            LeaseUntil = LeaseUntil,
            Attempts = Attempts,
            ProcessedAt = ProcessedAt,
            ProviderMessageId = ProviderMessageId,
            LastErrorCode = LastErrorCode,
        };
}

internal sealed class InMemoryTransactionalBoundary(InMemoryInvitationWorld world) : ITransactionalBoundary
{
    public async Task ExecuteSerializableAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var snapshot = world.Snapshot();
        try
        {
            await action(cancellationToken);
        }
        catch
        {
            world.Restore(snapshot);
            throw;
        }
    }

    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var snapshot = world.Snapshot();
        try
        {
            return await action(cancellationToken);
        }
        catch
        {
            world.Restore(snapshot);
            throw;
        }
    }
}

internal sealed class InMemoryIdentityStore(InMemoryInvitationWorld world) : IIdentityStore
{
    public Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken) =>
        Task.FromResult(world.Users.Count > 0);

    public Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(world.Users.SingleOrDefault(user => user.Email == normalizedEmail));

    public Task AddUserAsync(UserAccount user, CancellationToken cancellationToken)
    {
        world.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        world.Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task PersistLoginAsync(
        Guid userId,
        string? rehashedPasswordHash,
        UserSession newSession,
        CancellationToken cancellationToken)
    {
        if (rehashedPasswordHash is not null)
        {
            world.Users.Single(user => user.Id == userId).SetPasswordHash(rehashedPasswordHash);
        }

        world.Sessions.Add(newSession);
        return Task.CompletedTask;
    }

    public Task<UserSession?> FindSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(world.Sessions.SingleOrDefault(session => session.Id == sessionId));

    public Task SaveSessionAsync(UserSession session, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<ActiveUserSession?> FindActiveByTokenHashAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = world.Sessions.SingleOrDefault(candidate =>
            candidate.TokenHash == tokenHash && candidate.IsActive(now));
        if (session is null)
        {
            return Task.FromResult<ActiveUserSession?>(null);
        }

        var user = world.Users.Single(candidate => candidate.Id == session.UserId);
        return Task.FromResult<ActiveUserSession?>(new ActiveUserSession(user, session));
    }

    public Task<bool> IsCampaignDmAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(world.Memberships.Any(membership =>
            membership.CampaignId == campaignId
            && membership.UserId == userId
            && membership.Role == CampaignRole.Dm));

    public Task<bool> IsCampaignMemberAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(world.Memberships.Any(membership =>
            membership.CampaignId == campaignId && membership.UserId == userId));

    public Task AddMembershipAsync(CampaignMembership membership, CancellationToken cancellationToken)
    {
        world.Memberships.Add(membership);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryInvitationStore(InMemoryInvitationWorld world) : IInvitationStore
{
    public bool ThrowOnMarkSent { get; set; }

    public Task<bool> HasPendingAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        Task.FromResult(world.Invitations.Any(invitation =>
            invitation.Kind == kind
            && invitation.CampaignId == campaignId
            && invitation.RecipientEmail == recipientEmail
            && invitation.IsPending(now)));

    public Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var hash = Convert.FromHexString(tokenHash);
        var found = world.Invitations.SingleOrDefault(invitation =>
            invitation.TokenHash.Span.SequenceEqual(hash));
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<Invitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var found = world.Invitations.SingleOrDefault(invitation => invitation.Id == invitationId);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken)
    {
        var found = world.Invitations.SingleOrDefault(invitation =>
            invitation.Id == invitationId
            && invitation.Kind == kind
            && invitation.CampaignId == campaignId);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task<IReadOnlyList<DateTimeOffset>> ListRecentIssueTimesAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DateTimeOffset>>(
            world.Invitations
                .Where(invitation =>
                    invitation.Kind == kind
                    && invitation.CampaignId == campaignId
                    && invitation.RecipientEmail == recipientEmail
                    && invitation.IssuedAt >= since)
                .Select(invitation => invitation.IssuedAt)
                .ToArray());

    public Task<IReadOnlyList<InvitationListItem>> ListAsync(
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InvitationListItem>>(
            world.Invitations
                .Where(invitation => invitation.Kind == kind && invitation.CampaignId == campaignId)
                .OrderByDescending(invitation => invitation.IssuedAt)
                .Select(invitation => new InvitationListItem(
                    Clone(invitation),
                    world.LastSentAt.GetValueOrDefault(invitation.Id)))
                .ToArray());

    public Task AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        world.Invitations.Add(Clone(invitation));
        return Task.CompletedTask;
    }

    public Task SaveAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        var index = world.Invitations.FindIndex(candidate => candidate.Id == invitation.Id);
        world.Invitations[index] = Clone(invitation);
        return Task.CompletedTask;
    }

    public Task SaveAllAsync(IReadOnlyCollection<Invitation> invitations, CancellationToken cancellationToken)
    {
        foreach (var invitation in invitations)
        {
            var index = world.Invitations.FindIndex(candidate => candidate.Id == invitation.Id);
            world.Invitations[index] = Clone(invitation);
        }

        return Task.CompletedTask;
    }

    public Task MarkSentAsync(Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (ThrowOnMarkSent)
        {
            throw new IOException("forced mark-sent failure");
        }

        world.LastSentAt[invitationId] = now;
        return Task.CompletedTask;
    }

    private static Invitation Clone(Invitation invitation) =>
        Invitation.Restore(
            invitation.Id,
            invitation.Kind,
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.TokenHash.ToArray(),
            invitation.IssuedByUserId,
            invitation.IssuedAt,
            invitation.ExpiresAt,
            invitation.Status,
            invitation.AcceptedByUserId,
            invitation.AcceptedAt,
            invitation.RevokedAt);
}

internal sealed class InMemoryInvitationOutboxStore(InMemoryInvitationWorld world) : IInvitationOutboxStore
{
    public Task EnqueueAsync(
        Guid invitationId,
        string encryptedToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        world.Outbox.Add(new MemoryOutboxMessage
        {
            Id = Guid.NewGuid(),
            InvitationId = invitationId,
            EncryptedToken = encryptedToken,
            CreatedAt = now,
            NextAttemptAt = now,
        });
        return Task.CompletedTask;
    }

    public Task<ClaimedOutboxWork?> TryClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var message = world.Outbox
            .Where(candidate =>
                candidate.ProcessedAt is null
                && candidate.Attempts < 5
                && candidate.NextAttemptAt <= now
                && (candidate.LeaseUntil is null || candidate.LeaseUntil < now))
            .OrderBy(candidate => candidate.NextAttemptAt)
            .FirstOrDefault();
        if (message is null)
        {
            return Task.FromResult<ClaimedOutboxWork?>(null);
        }

        message.LeaseUntil = now.AddMinutes(1);
        return Task.FromResult<ClaimedOutboxWork?>(
            new ClaimedOutboxWork(message.Id, message.InvitationId, message.EncryptedToken));
    }

    public Task MarkProcessedAsync(
        Guid outboxId,
        string providerMessageId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = world.Outbox.Single(candidate => candidate.Id == outboxId);
        message.ProviderMessageId = providerMessageId;
        message.ProcessedAt = now;
        message.LeaseUntil = null;
        message.LastErrorCode = null;
        message.EncryptedToken = string.Empty;
        return Task.CompletedTask;
    }

    public Task MarkDiscardedAsync(Guid outboxId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var message = world.Outbox.Single(candidate => candidate.Id == outboxId);
        message.ProviderMessageId = "discarded";
        message.ProcessedAt = now;
        message.LeaseUntil = null;
        message.LastErrorCode = null;
        message.EncryptedToken = string.Empty;
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        Guid outboxId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = world.Outbox.Single(candidate => candidate.Id == outboxId);
        message.Attempts++;
        message.LastErrorCode = errorCode;
        message.LeaseUntil = null;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, InvitationDeliveryStatus>> GetDeliveryStatusesAsync(
        IReadOnlyCollection<Guid> invitationIds,
        CancellationToken cancellationToken)
    {
        var statuses = invitationIds.ToDictionary(
            invitationId => invitationId,
            invitationId => ToDeliveryStatus(
                world.Outbox
                    .Where(message => message.InvitationId == invitationId)
                    .OrderByDescending(message => message.CreatedAt)
                    .FirstOrDefault()));
        return Task.FromResult<IReadOnlyDictionary<Guid, InvitationDeliveryStatus>>(statuses);
    }

    private static InvitationDeliveryStatus ToDeliveryStatus(MemoryOutboxMessage? delivery) =>
        delivery switch
        {
            { ProcessedAt: not null, ProviderMessageId: not "discarded" } => InvitationDeliveryStatus.Sent,
            { ProcessedAt: not null, ProviderMessageId: "discarded" } => InvitationDeliveryStatus.Discarded,
            { Attempts: >= 5 } => InvitationDeliveryStatus.Failed,
            _ => InvitationDeliveryStatus.Pending,
        };
}
