using DndCampaign.Modules.Access.Application.Abstractions.Messaging;
using DndCampaign.Modules.Access.Application.Ports.Persistence;

namespace DndCampaign.Modules.Access.Application.Bootstrap;

internal sealed record GetBootstrapStatusQuery;

internal sealed record BootstrapStatus(bool IsRequired);

internal sealed class GetBootstrapStatusHandler(IUserAccountRepository users)
    : IQueryHandler<GetBootstrapStatusQuery, BootstrapStatus>
{
    public async Task<BootstrapStatus> HandleAsync(
        GetBootstrapStatusQuery query,
        CancellationToken cancellationToken = default) =>
        new(!await users.AnyAsync(cancellationToken));
}
