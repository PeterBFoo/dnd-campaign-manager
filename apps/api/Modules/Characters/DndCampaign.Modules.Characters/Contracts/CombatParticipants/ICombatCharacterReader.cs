namespace DndCampaign.Modules.Characters.Contracts.CombatParticipants;

public interface ICombatCharacterReader
{
    Task<CombatCharacterSnapshot?> GetAsync(
        Guid campaignId,
        Guid characterId,
        CancellationToken cancellationToken = default);
}

public sealed record CombatCharacterSnapshot(
    Guid CharacterId,
    string Name,
    int ArmorClass);
