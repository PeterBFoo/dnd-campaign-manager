using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class UserAccountRepository(AccessDbContext database) : IUserAccountRepository
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        database.Users.AnyAsync(cancellationToken);

    public Task<UserAccount?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default) =>
        database.Users.SingleOrDefaultAsync(
            user => user.Email == normalizedEmail,
            cancellationToken);

    public void Add(UserAccount user) => database.Users.Add(user);
}
