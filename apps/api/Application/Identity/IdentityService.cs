using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DndCampaign.Api.Application.Identity;

public sealed class IdentityService(CampaignDbContext database)
{
    public async Task<BootstrapStatus> GetBootstrapStatus(CancellationToken cancellationToken)
    {
        var hasUsers = await database.Users.AnyAsync(cancellationToken);
        return hasUsers ? BootstrapStatus.Completed : BootstrapStatus.Required;
    }
}