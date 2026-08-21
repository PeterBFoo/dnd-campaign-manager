using DndCampaign.Modules.Access.Domain.Accounts;

namespace DndCampaign.Modules.Access.Application.Ports.Persistence;

internal interface IUserAccountRepository
{
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    void Add(UserAccount user);
}
