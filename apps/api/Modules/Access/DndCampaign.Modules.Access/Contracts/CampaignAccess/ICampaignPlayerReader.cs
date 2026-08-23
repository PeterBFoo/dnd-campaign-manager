namespace DndCampaign.Modules.Access.Contracts.CampaignAccess;

public interface ICampaignPlayerReader
{
    Task<IReadOnlyList<CampaignPlayer>> ListPlayersAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);
}

public sealed record CampaignPlayer(Guid UserId, string DisplayName);
