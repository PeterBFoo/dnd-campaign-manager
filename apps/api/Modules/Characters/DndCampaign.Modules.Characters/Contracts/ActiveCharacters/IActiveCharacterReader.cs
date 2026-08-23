namespace DndCampaign.Modules.Characters.Contracts.ActiveCharacters;

public interface IActiveCharacterReader
{
    Task<ActiveCharacterSnapshot?> GetActiveAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record ActiveCharacterSnapshot(Guid CharacterId, string Name);
