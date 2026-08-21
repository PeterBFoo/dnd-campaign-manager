using DndCampaign.Modules.Access.Domain.Sessions;

namespace DndCampaign.Modules.Access.Application.Ports.Persistence;

internal interface IUserSessionRepository
{
    Task<UserSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    void Add(UserSession session);
}
