using DndCampaign.Modules.Characters.Domain.Characters;

namespace DndCampaign.Modules.Characters.Application.Ports;

internal interface ICharacterRepository
{
    Task<IReadOnlyList<PlayerCharacter>> ListByCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<bool> HasAnyOwnedAsync(
        Guid campaignId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    void Add(PlayerCharacter character);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<PlayerCharacter?> FindForUpdateAsync(
        Guid campaignId,
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task<PlayerCharacter?> FindAsync(
        Guid campaignId,
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task SaveOwnerChangeAsync(
        PlayerCharacter character,
        Guid? previousOwnerUserId,
        bool wasActive,
        CancellationToken cancellationToken = default);

    Task<PlayerCharacter?> ActivateOwnedAsync(
        Guid campaignId,
        Guid ownerUserId,
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        PlayerCharacter character,
        CancellationToken cancellationToken = default);
}

internal sealed class CharacterPersistenceConflictException(Exception innerException)
    : Exception("The requested character state conflicts with persisted data.", innerException);
