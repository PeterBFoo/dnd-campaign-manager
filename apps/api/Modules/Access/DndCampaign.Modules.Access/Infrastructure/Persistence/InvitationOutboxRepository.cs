using DndCampaign.Modules.Access.Application.Ports.Persistence;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class InvitationOutboxRepository(AccessDbContext database)
    : IInvitationOutboxRepository
{
    public void Add(Guid invitationId, string protectedToken, DateTimeOffset createdAt) =>
        database.InvitationOutbox.Add(InvitationOutboxMessage.Create(
            invitationId,
            protectedToken,
            createdAt));
}
