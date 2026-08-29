namespace DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;

public sealed record AdventureModuleCampaignSummary(
    Guid Id,
    string Name,
    string? CoverUrl);

public interface IAdventureModuleCampaignReader
{
    Task<IReadOnlyList<AdventureModuleCampaignSummary>> ListOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<AdventureModuleCampaignSummary?> FindAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdventureModuleCampaignSummary>> ListAsync(
        IReadOnlyCollection<Guid> moduleIds,
        CancellationToken cancellationToken = default);
}
