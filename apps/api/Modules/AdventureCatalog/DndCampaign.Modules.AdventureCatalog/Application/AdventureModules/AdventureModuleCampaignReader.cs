using DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Domain.AdventureModules;

namespace DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;

internal sealed class AdventureModuleCampaignReader(IAdventureModuleRepository modules)
    : IAdventureModuleCampaignReader
{
    public async Task<IReadOnlyList<AdventureModuleCampaignSummary>> ListOptionsAsync(
        CancellationToken cancellationToken = default) =>
        (await modules.ListAsync(cancellationToken)).Select(ToSummary).ToArray();

    public async Task<AdventureModuleCampaignSummary?> FindAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default)
    {
        if (moduleId == Guid.Empty)
        {
            return null;
        }

        var module = await modules.FindAsync(moduleId, cancellationToken);
        return module is null ? null : ToSummary(module);
    }

    public async Task<IReadOnlyList<AdventureModuleCampaignSummary>> ListAsync(
        IReadOnlyCollection<Guid> moduleIds,
        CancellationToken cancellationToken = default)
    {
        if (moduleIds.Count == 0)
        {
            return [];
        }

        var requested = moduleIds.Where(id => id != Guid.Empty).ToHashSet();
        var modulesList = await modules.ListByIdsAsync(requested, cancellationToken);
        return modulesList.Select(ToSummary).ToArray();
    }

    private static AdventureModuleCampaignSummary ToSummary(
        AdventureModule module) => new(
            module.Id,
            module.Name,
            module.Cover is null ? null : $"/api/v1/adventure-modules/{module.Id}/cover");
}
