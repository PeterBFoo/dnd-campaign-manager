using DndCampaign.Api.Application.Invitations;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Infrastructure.Persistence;

/// <summary>
/// Outbox persistence for a single replica (ADR-0003). Two workers can both observe the
/// same unlocked row before either writes <c>LeaseUntil</c>; this store does not add
/// <c>SKIP LOCKED</c> or distributed locks.
/// </summary>
public sealed class InvitationOutboxStore(CampaignDbContext database) : IInvitationOutboxStore
{
    public async Task EnqueueAsync(
        Guid invitationId,
        string encryptedToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = InvitationOutboxMessage.Create(invitationId, encryptedToken, now);
        database.InvitationOutbox.Add(message);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, message);
    }

    public async Task<ClaimedOutboxWork?> TryClaimNextAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await database.InvitationOutbox
            .Where(candidate =>
                candidate.ProcessedAt == null
                && candidate.Attempts < 5
                && candidate.NextAttemptAt <= now
                && (candidate.LeaseUntil == null || candidate.LeaseUntil < now))
            .OrderBy(candidate => candidate.NextAttemptAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            return null;
        }

        message.Acquire(now);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, message);
        return new ClaimedOutboxWork(message.Id, message.InvitationId, message.EncryptedToken);
    }

    public async Task MarkProcessedAsync(
        Guid outboxId,
        string providerMessageId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await LoadMessageAsync(outboxId, cancellationToken);
        message.MarkProcessed(providerMessageId, now);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, message);
    }

    public async Task MarkDiscardedAsync(
        Guid outboxId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await LoadMessageAsync(outboxId, cancellationToken);
        message.MarkDiscarded(now);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, message);
    }

    public async Task MarkFailedAsync(
        Guid outboxId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await LoadMessageAsync(outboxId, cancellationToken);
        message.MarkFailed(errorCode, now);
        await CampaignDbContextPersistence.SaveEntitiesAsync(database, cancellationToken, message);
    }

    public async Task<IReadOnlyDictionary<Guid, InvitationDeliveryStatus>> GetDeliveryStatusesAsync(
        IReadOnlyCollection<Guid> invitationIds,
        CancellationToken cancellationToken)
    {
        if (invitationIds.Count == 0)
        {
            return new Dictionary<Guid, InvitationDeliveryStatus>();
        }

        var messages = await database.InvitationOutbox
            .AsNoTracking()
            .Where(message => invitationIds.Contains(message.InvitationId))
            .ToListAsync(cancellationToken);

        return invitationIds.ToDictionary(
            invitationId => invitationId,
            invitationId => ToDeliveryStatus(
                messages
                    .Where(message => message.InvitationId == invitationId)
                    .OrderByDescending(message => message.CreatedAt)
                    .FirstOrDefault()));
    }

    private Task<InvitationOutboxMessage> LoadMessageAsync(Guid outboxId, CancellationToken cancellationToken) =>
        database.InvitationOutbox.SingleAsync(
            candidate => candidate.Id == outboxId,
            cancellationToken);

    private static InvitationDeliveryStatus ToDeliveryStatus(InvitationOutboxMessage? delivery) =>
        delivery switch
        {
            { ProcessedAt: not null, ProviderMessageId: not "discarded" } => InvitationDeliveryStatus.Sent,
            { ProcessedAt: not null, ProviderMessageId: "discarded" } => InvitationDeliveryStatus.Discarded,
            { Attempts: >= 5 } => InvitationDeliveryStatus.Failed,
            _ => InvitationDeliveryStatus.Pending,
        };
}
