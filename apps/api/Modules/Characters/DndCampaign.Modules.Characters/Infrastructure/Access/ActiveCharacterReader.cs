using DndCampaign.Modules.Characters.Contracts.ActiveCharacters;
using DndCampaign.Modules.Characters.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Characters.Infrastructure.Access;

internal sealed class ActiveCharacterReader(CharactersDbContext database) : IActiveCharacterReader
{
    public Task<ActiveCharacterSnapshot?> GetActiveAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        database.Characters.AsNoTracking()
            .Where(character => character.CampaignId == campaignId
                && character.OwnerUserId == userId
                && character.IsActive)
            .Select(character => new ActiveCharacterSnapshot(character.Id, character.Name))
            .SingleOrDefaultAsync(cancellationToken);
}
