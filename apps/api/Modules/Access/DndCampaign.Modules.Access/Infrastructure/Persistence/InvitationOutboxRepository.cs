using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Application.Ports.Events;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class InvitationOutboxRepository(AccessDbContext database)
    : IInvitationOutboxRepository
{
    public InvitationEmailRequested Add(Guid invitationId, string protectedToken, DateTimeOffset createdAt)
    {
        var message = InvitationOutboxMessage.Create(
            invitationId,
            protectedToken,
            createdAt);
        database.InvitationOutbox.Add(message);
        return new InvitationEmailRequested(
            message.Id,
            invitationId,
            protectedToken,
            "v1",
            createdAt);
    }
}
