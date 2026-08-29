namespace DndCampaign.Modules.Access.Application.Ports.Events;

internal sealed record InvitationEmailRequested(
    Guid EventId,
    Guid InvitationId,
    string EncryptedToken,
    string KeyVersion,
    DateTimeOffset OccurredAt,
    string SchemaVersion = "1.0");

internal interface IInvitationEventPublisher
{
    Task PublishAsync(InvitationEmailRequested message, CancellationToken cancellationToken = default);
}

internal enum InvitationEmailDeliveryResult
{
    Processed,
    AlreadyProcessed,
    Discarded,
    Retryable,
    Invalid,
}

internal interface IInvitationEmailDeliveryService
{
    Task<InvitationEmailDeliveryResult> ProcessAsync(
        Guid eventId,
        Guid invitationId,
        string encryptedToken,
        CancellationToken cancellationToken = default);
}

internal interface IInvitationPendingEventReplayer
{
    Task<int> ReplayAsync(CancellationToken cancellationToken = default);
}

internal sealed class InvitationEventPublishException(string message, Exception? innerException = null)
    : Exception(message, innerException);
