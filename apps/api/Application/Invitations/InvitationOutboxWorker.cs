using System.Security.Cryptography;
using System.Data.Common;
using DndCampaign.Api.Application.Email;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Application.Invitations;

public sealed class InvitationOutboxWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<InvitationOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), timeProvider, stoppingToken);
                }
            }
            catch (Exception exception) when (IsDatabaseUnavailable(exception))
            {
                logger.LogWarning("Invitation outbox is waiting for the database to become available");
                await Task.Delay(TimeSpan.FromSeconds(30), timeProvider, stoppingToken);
            }
        }
    }

    private static bool IsDatabaseUnavailable(Exception exception) =>
        exception is DbException or TimeoutException
        || (exception.InnerException is not null && IsDatabaseUnavailable(exception.InnerException));

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
        var now = timeProvider.GetUtcNow();
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
            return false;
        }

        message.Acquire(now);
        await database.SaveChangesAsync(cancellationToken);
        var invitation = await database.Invitations.SingleAsync(
            candidate => candidate.Id == message.InvitationId,
            cancellationToken);
        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= now)
        {
            invitation.MarkExpired(now);
            message.MarkDiscarded(now);
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        try
        {
            var protector = scope.ServiceProvider.GetRequiredService<InvitationTokenProtector>();
            var composer = scope.ServiceProvider.GetRequiredService<InvitationEmailComposer>();
            var sender = scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
            var token = protector.Unprotect(message.EncryptedToken);
            var email = composer.Compose(invitation, token, message.Id.ToString("N"));
            var receipt = await sender.SendAsync(email, cancellationToken);
            message.MarkProcessed(receipt.ProviderMessageId, now);
            invitation.MarkSent(now);
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Invitation outbox message {OutboxMessageId} was delivered for kind {InvitationKind}",
                message.Id,
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
            message.MarkFailed(errorCode, now);
            await database.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Invitation outbox message {OutboxMessageId} failed with {ErrorCode}",
                message.Id,
                errorCode);
        }

        return true;
    }
}
