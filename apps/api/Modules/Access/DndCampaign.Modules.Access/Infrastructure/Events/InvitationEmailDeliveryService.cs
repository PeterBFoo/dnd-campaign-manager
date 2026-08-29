using System.Data;
using System.Security.Cryptography;
using DndCampaign.Modules.Access.Application.Ports.Email;
using DndCampaign.Modules.Access.Application.Ports.Events;
using DndCampaign.Modules.Access.Domain.Invitations;
using DndCampaign.Modules.Access.Infrastructure.Email;
using DndCampaign.Modules.Access.Infrastructure.Persistence;
using DndCampaign.Modules.Access.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndCampaign.Modules.Access.Infrastructure.Events;

internal sealed class InvitationEmailDeliveryService(
    AccessDbContext database,
    InvitationTokenProtector protector,
    InvitationEmailComposer composer,
    ITransactionalEmailSender sender,
    TimeProvider timeProvider,
    EventBrokerMetrics metrics,
    ILogger<InvitationEmailDeliveryService> logger) : IInvitationEmailDeliveryService
{
    public async Task<InvitationEmailDeliveryResult> ProcessAsync(
        Guid eventId,
        Guid invitationId,
        string encryptedToken,
        CancellationToken requestCancellationToken = default)
    {
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        var now = timeProvider.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            requestCancellationToken);
        var message = await database.InvitationOutbox
            .SingleOrDefaultAsync(candidate => candidate.Id == eventId, requestCancellationToken);
        if (message is null || message.InvitationId != invitationId)
        {
            await transaction.RollbackAsync(requestCancellationToken);
            return InvitationEmailDeliveryResult.Retryable;
        }

        if (message.ProcessedAt is not null)
        {
            await transaction.CommitAsync(requestCancellationToken);
            metrics.Duplicate();
            metrics.Duration(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return InvitationEmailDeliveryResult.AlreadyProcessed;
        }

        if (!string.Equals(message.EncryptedToken, encryptedToken, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(requestCancellationToken);
            return InvitationEmailDeliveryResult.Invalid;
        }

        if (message.LeaseUntil is not null && message.LeaseUntil > now)
        {
            await transaction.RollbackAsync(requestCancellationToken);
            return InvitationEmailDeliveryResult.Retryable;
        }

        if (message.Attempts > 0 && message.NextAttemptAt > now)
        {
            await transaction.RollbackAsync(requestCancellationToken);
            return InvitationEmailDeliveryResult.Retryable;
        }

        var invitation = await database.Invitations
            .SingleOrDefaultAsync(candidate => candidate.Id == invitationId, requestCancellationToken);
        if (invitation is null)
        {
            await transaction.RollbackAsync(requestCancellationToken);
            return InvitationEmailDeliveryResult.Invalid;
        }

        message.Acquire(now);
        await database.SaveChangesAsync(requestCancellationToken);
        await transaction.CommitAsync(requestCancellationToken);

        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= now)
        {
            invitation.Expire(now);
            message.MarkDiscarded(now);
            await database.SaveChangesAsync(requestCancellationToken);
            metrics.Discarded();
            metrics.Duration(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return InvitationEmailDeliveryResult.Discarded;
        }

        try
        {
            var token = protector.Unprotect(message.EncryptedToken);
            var email = composer.Compose(invitation, token, message.Id.ToString("N"));
            var receipt = await sender.SendAsync(email, requestCancellationToken);
            message.MarkProcessed(receipt.ProviderMessageId, timeProvider.GetUtcNow());
            invitation.MarkSent(timeProvider.GetUtcNow());
            await database.SaveChangesAsync(requestCancellationToken);
            metrics.Processed();
            metrics.Duration(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            logger.LogInformation("Invitation event {EventId} delivered", eventId);
            return InvitationEmailDeliveryResult.Processed;
        }
        catch (Exception exception) when (
            exception is TransactionalEmailDeliveryException
                or CryptographicException
                or FormatException
                or InvalidOperationException)
        {
            message.MarkFailed(
                exception is TransactionalEmailDeliveryException ? "provider_failure" : "payload_failure",
                timeProvider.GetUtcNow());
            await database.SaveChangesAsync(requestCancellationToken);
            metrics.Failed();
            metrics.Duration(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            logger.LogWarning(exception, "Invitation event {EventId} failed", eventId);
            return InvitationEmailDeliveryResult.Retryable;
        }
    }
}

internal sealed class EventBrokerMetrics
{
    public const string MeterName = "DndCampaign.Api.EventBroker";
    private static readonly System.Diagnostics.Metrics.Meter Meter = new(MeterName);
    private readonly System.Diagnostics.Metrics.Counter<long> processed =
        Meter.CreateCounter<long>("event.processed");
    private readonly System.Diagnostics.Metrics.Counter<long> failed =
        Meter.CreateCounter<long>("event.failed");
    private readonly System.Diagnostics.Metrics.Counter<long> duplicate =
        Meter.CreateCounter<long>("event.duplicate");
    private readonly System.Diagnostics.Metrics.Counter<long> discarded =
        Meter.CreateCounter<long>("event.discarded");
    private readonly System.Diagnostics.Metrics.Histogram<double> duration =
        Meter.CreateHistogram<double>("event.delivery.duration", "ms");

    public void Processed() => processed.Add(1);
    public void Failed() => failed.Add(1);
    public void Duplicate() => duplicate.Add(1);
    public void Discarded() => discarded.Add(1);
    public void Duration(double milliseconds) => duration.Record(milliseconds);
}
