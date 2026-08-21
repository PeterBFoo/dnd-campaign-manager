using System.Security.Cryptography;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;
using Microsoft.Extensions.Logging;

namespace DndCampaign.Api.Application.Invitations;

public sealed class ProcessInvitationOutbox(
    IInvitationStore invitations,
    IInvitationOutboxStore outbox,
    ITransactionalBoundary transactions,
    InvitationTokenProtector protector,
    InvitationEmailComposer composer,
    ITransactionalEmailSender sender,
    TimeProvider timeProvider,
    ILogger<ProcessInvitationOutbox> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var work = await outbox.TryClaimNextAsync(now, cancellationToken);
        if (work is null)
        {
            return false;
        }

        var invitation = await invitations.FindByIdAsync(work.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("The claimed outbox message has no invitation.");

        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= now)
        {
            await transactions.ExecuteSerializableAsync(async ct =>
            {
                invitation.Expire(now);
                await invitations.SaveAsync(invitation, ct);
                await outbox.MarkDiscardedAsync(work.OutboxId, now, ct);
            }, cancellationToken);
            return true;
        }

        try
        {
            var token = protector.Unprotect(work.EncryptedToken);
            var email = composer.Compose(invitation, token, work.OutboxId.ToString("N"));
            var receipt = await sender.SendAsync(email, cancellationToken);
            await transactions.ExecuteSerializableAsync(async ct =>
            {
                await outbox.MarkProcessedAsync(work.OutboxId, receipt.ProviderMessageId, now, ct);
                await invitations.MarkSentAsync(work.InvitationId, now, ct);
            }, cancellationToken);
            logger.LogInformation(
                "Invitation outbox message {OutboxMessageId} was delivered for kind {InvitationKind}",
                work.OutboxId,
                invitation.Kind);
        }
        catch (Exception exception) when (
            exception is TransactionalEmailDeliveryException
                or CryptographicException
                or FormatException
                or InvalidOperationException)
        {
            var errorCode = exception switch
            {
                TransactionalEmailDeliveryException => "provider_failure",
                CryptographicException or FormatException => "payload_failure",
                _ => "configuration_failure",
            };
            await outbox.MarkFailedAsync(work.OutboxId, errorCode, now, cancellationToken);
            logger.LogWarning(
                "Invitation outbox message {OutboxMessageId} failed with {ErrorCode}",
                work.OutboxId,
                errorCode);
        }

        return true;
    }
}
