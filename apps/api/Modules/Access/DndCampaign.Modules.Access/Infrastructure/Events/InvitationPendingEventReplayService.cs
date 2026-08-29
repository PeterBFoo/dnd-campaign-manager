using DndCampaign.Modules.Access.Application.Ports.Events;
using DndCampaign.Modules.Access.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Events;

internal sealed class InvitationPendingEventReplayService(
    AccessDbContext database,
    IInvitationEventPublisher publisher) : IInvitationPendingEventReplayer
{
    public async Task<int> ReplayAsync(CancellationToken cancellationToken = default)
    {
        var pending = await database.InvitationOutbox
            .AsNoTracking()
            .Where(message => message.ProcessedAt == null && message.Attempts < 5)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var message in pending)
        {
            await publisher.PublishAsync(new InvitationEmailRequested(
                message.Id,
                message.InvitationId,
                message.EncryptedToken,
                "v1",
                message.CreatedAt), cancellationToken);
        }

        return pending.Count;
    }
}
