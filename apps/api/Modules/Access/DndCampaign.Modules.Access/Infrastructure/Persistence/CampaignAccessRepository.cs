using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Domain.CampaignAccess;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class CampaignAccessRepository(AccessDbContext database) : ICampaignAccessRepository
{
    public Task<bool> IsDmAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        database.CampaignMemberships.AnyAsync(membership =>
            membership.CampaignId == campaignId
            && membership.UserId == userId
            && membership.Role == CampaignRole.Dm,
            cancellationToken);

    public Task<bool> IsMemberAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        database.CampaignMemberships.AnyAsync(membership =>
            membership.CampaignId == campaignId && membership.UserId == userId,
            cancellationToken);

    public void Add(CampaignMembership membership) => database.CampaignMemberships.Add(membership);
}
