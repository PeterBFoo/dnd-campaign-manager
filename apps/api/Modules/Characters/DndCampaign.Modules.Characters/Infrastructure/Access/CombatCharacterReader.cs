using DndCampaign.Modules.Characters.Contracts.CombatParticipants;
using DndCampaign.Modules.Characters.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Characters.Infrastructure.Access;

internal sealed class CombatCharacterReader(CharactersDbContext database) : ICombatCharacterReader
{
    public Task<CombatCharacterSnapshot?> GetAsync(
        Guid campaignId,
        Guid characterId,
        CancellationToken cancellationToken = default) =>
        database.Characters.AsNoTracking()
            .Where(character => character.CampaignId == campaignId && character.Id == characterId)
            .Select(character => new CombatCharacterSnapshot(
                character.Id,
                character.Name,
                character.ArmorClass))
            .SingleOrDefaultAsync(cancellationToken);
}
