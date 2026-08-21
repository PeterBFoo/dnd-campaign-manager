using System.Data.Common;
using DndCampaign.Api.Application.Invitations;

namespace DndCampaign.Api.Infrastructure.Background;

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
        var processor = scope.ServiceProvider.GetRequiredService<ProcessInvitationOutbox>();
        return await processor.ProcessNextAsync(cancellationToken);
    }
}
