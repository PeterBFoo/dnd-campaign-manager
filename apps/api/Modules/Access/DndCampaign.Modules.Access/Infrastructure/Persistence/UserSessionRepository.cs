using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class UserSessionRepository(AccessDbContext database) : IUserSessionRepository
{
    public Task<UserSession?> FindByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        database.UserSessions.SingleOrDefaultAsync(
            session => session.Id == sessionId,
            cancellationToken);

    public void Add(UserSession session) => database.UserSessions.Add(session);
}
