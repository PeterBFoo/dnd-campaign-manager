using DndCampaign.Modules.Characters.Application.Ports;
using DndCampaign.Modules.Characters.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Modules.Characters.Infrastructure.Persistence;

internal sealed class CharacterRepository(CharactersDbContext database) : ICharacterRepository
{
    public async Task<IReadOnlyList<PlayerCharacter>> ListByCampaignAsync(
        Guid campaignId, CancellationToken cancellationToken = default) =>
        await database.Characters.AsNoTracking()
            .Where(character => character.CampaignId == campaignId)
            .OrderByDescending(character => character.IsActive)
            .ThenBy(character => character.Name)
            .ThenBy(character => character.Id)
            .ToArrayAsync(cancellationToken);

    public Task<bool> HasAnyOwnedAsync(
        Guid campaignId, Guid ownerUserId, CancellationToken cancellationToken = default) =>
        database.Characters.AsNoTracking().AnyAsync(character =>
            character.CampaignId == campaignId && character.OwnerUserId == ownerUserId,
            cancellationToken);

    public void Add(PlayerCharacter character) => database.Characters.Add(character);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new CharacterPersistenceConflictException(exception);
        }
    }

    public Task<PlayerCharacter?> FindForUpdateAsync(
        Guid campaignId, Guid characterId, CancellationToken cancellationToken = default) =>
        database.Characters.SingleOrDefaultAsync(character =>
            character.CampaignId == campaignId && character.Id == characterId,
            cancellationToken);

    public Task<PlayerCharacter?> FindAsync(
        Guid campaignId, Guid characterId, CancellationToken cancellationToken = default) =>
        database.Characters.AsNoTracking().SingleOrDefaultAsync(character =>
            character.CampaignId == campaignId && character.Id == characterId,
            cancellationToken);

    public async Task SaveOwnerChangeAsync(
        PlayerCharacter character,
        Guid? previousOwnerUserId,
        bool wasActive,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (previousOwnerUserId != character.OwnerUserId)
            {
                character.Deactivate();
                await SaveChangesAsync(cancellationToken);
                if (wasActive && previousOwnerUserId is Guid previousOwner)
                {
                    var replacement = await OldestOwnedAsync(
                        character.CampaignId, previousOwner, character.Id, cancellationToken);
                    replacement?.Activate();
                }

                if (character.OwnerUserId is Guid newOwner
                    && !await database.Characters.AnyAsync(item =>
                        item.CampaignId == character.CampaignId
                        && item.OwnerUserId == newOwner
                        && item.Id != character.Id
                        && item.IsActive,
                        cancellationToken))
                {
                    character.Activate();
                }
            }

            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PlayerCharacter?> ActivateOwnedAsync(
        Guid campaignId,
        Guid ownerUserId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var target = await database.Characters.SingleOrDefaultAsync(character =>
                character.CampaignId == campaignId
                && character.OwnerUserId == ownerUserId
                && character.Id == characterId,
                cancellationToken);
            if (target is null)
            {
                return null;
            }

            if (!target.IsActive)
            {
                await database.Characters
                    .Where(character => character.CampaignId == campaignId
                        && character.OwnerUserId == ownerUserId
                        && character.IsActive)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(character => character.IsActive, false),
                        cancellationToken);
                target.Activate();
                await SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return target;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteAsync(PlayerCharacter character, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            PlayerCharacter? replacement = null;
            if (character.IsActive && character.OwnerUserId is Guid owner)
            {
                replacement = await OldestOwnedAsync(character.CampaignId, owner, character.Id, cancellationToken);
                character.Deactivate();
                await SaveChangesAsync(cancellationToken);
            }

            database.Characters.Remove(character);
            replacement?.Activate();
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Task<PlayerCharacter?> OldestOwnedAsync(
        Guid campaignId, Guid ownerUserId, Guid excludedCharacterId, CancellationToken cancellationToken) =>
        database.Characters
            .Where(character => character.CampaignId == campaignId
                && character.OwnerUserId == ownerUserId
                && character.Id != excludedCharacterId)
            .OrderBy(character => character.CreatedAt)
            .ThenBy(character => character.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
