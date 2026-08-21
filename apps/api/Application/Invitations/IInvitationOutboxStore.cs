namespace DndCampaign.Api.Application.Invitations;

public sealed record ClaimedOutboxWork(
    Guid OutboxId,
    Guid InvitationId,
    string EncryptedToken);

public interface IInvitationOutboxStore
{
    Task EnqueueAsync(
        Guid invitationId,
        string encryptedToken,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ClaimedOutboxWork?> TryClaimNextAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        Guid outboxId,
        string providerMessageId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkDiscardedAsync(
        Guid outboxId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid outboxId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, InvitationDeliveryStatus>> GetDeliveryStatusesAsync(
        IReadOnlyCollection<Guid> invitationIds,
        CancellationToken cancellationToken);
}
