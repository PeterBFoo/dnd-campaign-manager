using System.Diagnostics;
using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Campaigns.Contracts.CampaignAccess;
using DndCampaign.Modules.Characters.Application.Abstractions;
using DndCampaign.Modules.Characters.Application.Ports;
using DndCampaign.Modules.Characters.Domain.Characters;

namespace DndCampaign.Modules.Characters.Application.Characters;

internal sealed record ListCharactersQuery(Guid UserId, Guid CampaignId);
internal sealed record ListCharacterOwnersQuery(Guid UserId, Guid CampaignId);
internal sealed record CreateCharacterCommand(Guid UserId, Guid CampaignId, string? Name, int? ArmorClass,
    int? Initiative, Guid? OwnerUserId, CharacterImageUpload? Image);
internal sealed record UpdateCharacterCommand(Guid UserId, Guid CampaignId, Guid CharacterId, string? Name,
    int? ArmorClass, int? Initiative, Guid? OwnerUserId, bool RemoveImage, CharacterImageUpload? Image);
internal sealed record ActivateCharacterCommand(Guid UserId, Guid CampaignId, Guid CharacterId);
internal sealed record DeleteCharacterCommand(Guid UserId, Guid CampaignId, Guid CharacterId);
internal sealed record GetCharacterImageQuery(Guid UserId, Guid CampaignId, Guid CharacterId);

internal sealed record CharacterDto(Guid Id, Guid CampaignId, Guid? OwnerUserId, string? OwnerDisplayName,
    string Name, int ArmorClass, int Initiative, string ImageUrl, bool IsActive, DateTimeOffset CreatedAt);
internal sealed record CharacterOwnerDto(Guid UserId, string DisplayName);

internal sealed class ListCharactersHandler(
    ICampaignAccessReader campaignAccess,
    ICampaignPlayerReader players,
    ICharacterRepository characters,
    ICharacterMetrics metrics)
{
    public async Task<CharacterResult<IReadOnlyList<CharacterDto>>> HandleAsync(
        ListCharactersQuery query, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await CharacterAuthorization.AuthorizeAsync(
                campaignAccess, query.CampaignId, query.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = CharacterAuthorization.Outcome(access.Error);
                return CharacterResult<IReadOnlyList<CharacterDto>>.Failure(access.Error);
            }

            var ownerNames = (await players.ListPlayersAsync(query.CampaignId, cancellationToken))
                .ToDictionary(player => player.UserId, player => player.DisplayName);
            var items = await characters.ListByCampaignAsync(query.CampaignId, cancellationToken);
            outcome = "success";
            return CharacterResult<IReadOnlyList<CharacterDto>>.Success(items
                .Select(character => ToDto(character, OwnerName(character.OwnerUserId, ownerNames))).ToArray());
        }
        finally
        {
            metrics.OperationCompleted("list", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    internal static CharacterDto ToDto(PlayerCharacter character, string? ownerDisplayName) => new(
        character.Id, character.CampaignId, character.OwnerUserId, ownerDisplayName, character.Name,
        character.ArmorClass, character.Initiative,
        character.ImageObjectKey is null
            ? PlayerCharacter.DefaultImageUrl
            : $"/api/v1/campaigns/{character.CampaignId}/characters/{character.Id}/image",
        character.IsActive, character.CreatedAt);

    internal static string? OwnerName(Guid? ownerUserId, IReadOnlyDictionary<Guid, string> owners) =>
        ownerUserId is Guid id && owners.TryGetValue(id, out var name) ? name : null;
}

internal sealed class ListCharacterOwnersHandler(
    ICampaignAccessReader campaignAccess,
    ICampaignPlayerReader players,
    ICharacterMetrics metrics)
{
    public async Task<CharacterResult<IReadOnlyList<CharacterOwnerDto>>> HandleAsync(
        ListCharacterOwnersQuery query, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await CharacterAuthorization.AuthorizeAsync(
                campaignAccess, query.CampaignId, query.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = CharacterAuthorization.Outcome(access.Error);
                return CharacterResult<IReadOnlyList<CharacterOwnerDto>>.Failure(access.Error);
            }

            if (access.Role != CampaignRole.Dm)
            {
                outcome = "forbidden";
                return CharacterResult<IReadOnlyList<CharacterOwnerDto>>.Failure(CharacterAuthorization.Forbidden());
            }

            var result = await players.ListPlayersAsync(query.CampaignId, cancellationToken);
            outcome = "success";
            return CharacterResult<IReadOnlyList<CharacterOwnerDto>>.Success(result
                .Select(player => new CharacterOwnerDto(player.UserId, player.DisplayName)).ToArray());
        }
        finally
        {
            metrics.OperationCompleted("owners", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class CreateCharacterHandler(
    ICampaignAccessReader campaignAccess,
    ICampaignPlayerReader players,
    ICharacterRepository characters,
    ICharacterImageStore images,
    ICharacterMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<CharacterResult<CharacterDto>> HandleAsync(
        CreateCharacterCommand command, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        StoredCharacterImage? storedImage = null;
        try
        {
            var access = await CharacterAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = CharacterAuthorization.Outcome(access.Error);
                return CharacterResult<CharacterDto>.Failure(access.Error);
            }

            var validation = CharacterValidation.Required(command.Name, command.ArmorClass, command.Initiative);
            if (validation is not null)
            {
                outcome = "validation";
                return CharacterResult<CharacterDto>.Failure(validation);
            }

            var campaignPlayers = await players.ListPlayersAsync(command.CampaignId, cancellationToken);
            var owner = access.Role == CampaignRole.Player ? command.UserId : command.OwnerUserId;
            if (access.Role == CampaignRole.Player && command.OwnerUserId is not null && command.OwnerUserId != command.UserId)
            {
                outcome = "forbidden";
                return CharacterResult<CharacterDto>.Failure(CharacterAuthorization.Forbidden());
            }

            if (owner is Guid ownerId && campaignPlayers.All(player => player.UserId != ownerId))
            {
                outcome = "validation";
                return CharacterResult<CharacterDto>.Failure(CharacterValidation.InvalidOwner());
            }

            var hasCharacters = owner is Guid activeOwner
                && await characters.HasAnyOwnedAsync(command.CampaignId, activeOwner, cancellationToken);
            var character = PlayerCharacter.Create(command.CampaignId, owner, command.Name!,
                command.ArmorClass!.Value, command.Initiative!.Value, null, null, null,
                owner is not null && !hasCharacters, timeProvider.GetUtcNow());

            if (command.Image is not null)
            {
                var imageResult = await TryStoreImageAsync(
                    images, command.CampaignId, character.Id, command.Image, cancellationToken);
                if (imageResult.Error is not null)
                {
                    outcome = "validation";
                    return CharacterResult<CharacterDto>.Failure(imageResult.Error);
                }

                storedImage = imageResult.Image;
                character.Update(character.Name, character.ArmorClass, character.Initiative, character.OwnerUserId,
                    storedImage!.ObjectKey, storedImage.ContentType, storedImage.SizeBytes);
            }

            characters.Add(character);
            try
            {
                await characters.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                if (storedImage is not null)
                {
                    await images.DeleteIfExistsAsync(storedImage.ObjectKey, cancellationToken);
                }

                throw;
            }

            outcome = "success";
            var ownerName = owner is Guid id
                ? campaignPlayers.Single(player => player.UserId == id).DisplayName : null;
            return CharacterResult<CharacterDto>.Success(ListCharactersHandler.ToDto(character, ownerName));
        }
        catch (ArgumentException exception)
        {
            outcome = "validation";
            return CharacterResult<CharacterDto>.Failure(CharacterValidation.FromException(exception));
        }
        catch (CharacterPersistenceConflictException)
        {
            outcome = "conflict";
            return CharacterResult<CharacterDto>.Failure(CharacterValidation.ActiveConflict());
        }
        finally
        {
            metrics.OperationCompleted("create", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    internal static async Task<(StoredCharacterImage? Image, CharacterError? Error)> TryStoreImageAsync(
        ICharacterImageStore images, Guid campaignId, Guid characterId, CharacterImageUpload upload,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await images.StoreAsync(campaignId, characterId, upload, cancellationToken), null);
        }
        catch (CharacterImageValidationException exception)
        {
            return (null, CharacterValidation.InvalidImage(exception.Message));
        }
    }
}

internal sealed class UpdateCharacterHandler(
    ICampaignAccessReader campaignAccess,
    ICampaignPlayerReader players,
    ICharacterRepository characters,
    ICharacterImageStore images,
    ICharacterMetrics metrics)
{
    public async Task<CharacterResult<CharacterDto>> HandleAsync(
        UpdateCharacterCommand command, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        StoredCharacterImage? newImage = null;
        try
        {
            var access = await CharacterAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = CharacterAuthorization.Outcome(access.Error);
                return CharacterResult<CharacterDto>.Failure(access.Error);
            }

            var character = await characters.FindForUpdateAsync(
                command.CampaignId, command.CharacterId, cancellationToken);
            if (character is null)
            {
                outcome = "not_found";
                return CharacterResult<CharacterDto>.Failure(CharacterValidation.NotFound());
            }

            if (access.Role == CampaignRole.Player && character.OwnerUserId != command.UserId)
            {
                outcome = "forbidden";
                return CharacterResult<CharacterDto>.Failure(CharacterAuthorization.Forbidden());
            }

            var validation = CharacterValidation.Required(command.Name, command.ArmorClass, command.Initiative);
            if (validation is not null || (command.RemoveImage && command.Image is not null))
            {
                outcome = "validation";
                return CharacterResult<CharacterDto>.Failure(validation ?? CharacterValidation.InvalidImage(
                    "No se puede subir y retirar la imagen en la misma operación."));
            }

            var campaignPlayers = await players.ListPlayersAsync(command.CampaignId, cancellationToken);
            var owner = access.Role == CampaignRole.Dm ? command.OwnerUserId : character.OwnerUserId;
            if (owner is Guid ownerId && campaignPlayers.All(player => player.UserId != ownerId))
            {
                outcome = "validation";
                return CharacterResult<CharacterDto>.Failure(CharacterValidation.InvalidOwner());
            }

            var oldObjectKey = character.ImageObjectKey;
            if (command.Image is not null)
            {
                var imageResult = await CreateCharacterHandler.TryStoreImageAsync(
                    images, command.CampaignId, character.Id, command.Image, cancellationToken);
                if (imageResult.Error is not null)
                {
                    outcome = "validation";
                    return CharacterResult<CharacterDto>.Failure(imageResult.Error);
                }

                newImage = imageResult.Image;
            }

            var previousOwner = character.OwnerUserId;
            var wasActive = character.IsActive;
            character.Update(command.Name!, command.ArmorClass!.Value, command.Initiative!.Value, owner,
                command.RemoveImage ? null : newImage?.ObjectKey ?? character.ImageObjectKey,
                command.RemoveImage ? null : newImage?.ContentType ?? character.ImageContentType,
                command.RemoveImage ? null : newImage?.SizeBytes ?? character.ImageSizeBytes);
            try
            {
                await characters.SaveOwnerChangeAsync(character, previousOwner, wasActive, cancellationToken);
            }
            catch
            {
                if (newImage is not null)
                {
                    await images.DeleteIfExistsAsync(newImage.ObjectKey, cancellationToken);
                }

                throw;
            }

            if (oldObjectKey is not null && oldObjectKey != character.ImageObjectKey)
            {
                await images.DeleteIfExistsAsync(oldObjectKey, cancellationToken);
            }

            outcome = "success";
            var ownerName = owner is Guid id
                ? campaignPlayers.Single(player => player.UserId == id).DisplayName : null;
            return CharacterResult<CharacterDto>.Success(ListCharactersHandler.ToDto(character, ownerName));
        }
        catch (ArgumentException exception)
        {
            outcome = "validation";
            return CharacterResult<CharacterDto>.Failure(CharacterValidation.FromException(exception));
        }
        catch (CharacterPersistenceConflictException)
        {
            outcome = "conflict";
            return CharacterResult<CharacterDto>.Failure(CharacterValidation.ActiveConflict());
        }
        finally
        {
            metrics.OperationCompleted("update", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class ActivateCharacterHandler(
    ICampaignAccessReader campaignAccess,
    ICampaignPlayerReader players,
    ICharacterRepository characters,
    ICharacterMetrics metrics)
{
    public async Task<CharacterResult<CharacterDto>> HandleAsync(
        ActivateCharacterCommand command, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await CharacterAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = CharacterAuthorization.Outcome(access.Error);
                return CharacterResult<CharacterDto>.Failure(access.Error);
            }

            if (access.Role != CampaignRole.Player)
            {
                outcome = "forbidden";
                return CharacterResult<CharacterDto>.Failure(CharacterAuthorization.Forbidden());
            }

            var character = await characters.ActivateOwnedAsync(
                command.CampaignId, command.UserId, command.CharacterId, cancellationToken);
            if (character is null)
            {
                outcome = "not_found";
                return CharacterResult<CharacterDto>.Failure(CharacterValidation.NotFound());
            }

            outcome = "success";
            var ownerName = (await players.ListPlayersAsync(command.CampaignId, cancellationToken))
                .SingleOrDefault(player => player.UserId == command.UserId)?.DisplayName;
            return CharacterResult<CharacterDto>.Success(ListCharactersHandler.ToDto(character, ownerName));
        }
        catch (CharacterPersistenceConflictException)
        {
            outcome = "conflict";
            return CharacterResult<CharacterDto>.Failure(CharacterValidation.ActiveConflict());
        }
        finally
        {
            metrics.OperationCompleted("activate", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class DeleteCharacterHandler(
    ICampaignAccessReader campaignAccess,
    ICharacterRepository characters,
    ICharacterImageStore images,
    ICharacterMetrics metrics)
{
    public async Task<CharacterResult<bool>> HandleAsync(
        DeleteCharacterCommand command, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await CharacterAuthorization.AuthorizeAsync(
                campaignAccess, command.CampaignId, command.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = CharacterAuthorization.Outcome(access.Error);
                return CharacterResult<bool>.Failure(access.Error);
            }

            var character = await characters.FindForUpdateAsync(
                command.CampaignId, command.CharacterId, cancellationToken);
            if (character is null)
            {
                outcome = "not_found";
                return CharacterResult<bool>.Failure(CharacterValidation.NotFound());
            }

            if (access.Role == CampaignRole.Player && character.OwnerUserId != command.UserId)
            {
                outcome = "forbidden";
                return CharacterResult<bool>.Failure(CharacterAuthorization.Forbidden());
            }

            var objectKey = character.ImageObjectKey;
            await characters.DeleteAsync(character, cancellationToken);
            if (objectKey is not null)
            {
                await images.DeleteIfExistsAsync(objectKey, cancellationToken);
            }

            outcome = "success";
            return CharacterResult<bool>.Success(true);
        }
        finally
        {
            metrics.OperationCompleted("delete", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed class GetCharacterImageHandler(
    ICampaignAccessReader campaignAccess,
    ICharacterRepository characters,
    ICharacterImageStore images,
    ICharacterMetrics metrics)
{
    public async Task<CharacterResult<CharacterImageContent>> HandleAsync(
        GetCharacterImageQuery query, CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            var access = await CharacterAuthorization.AuthorizeAsync(
                campaignAccess, query.CampaignId, query.UserId, cancellationToken);
            if (access.Error is not null)
            {
                outcome = CharacterAuthorization.Outcome(access.Error);
                return CharacterResult<CharacterImageContent>.Failure(access.Error);
            }

            var character = await characters.FindAsync(query.CampaignId, query.CharacterId, cancellationToken);
            if (character?.ImageObjectKey is null)
            {
                outcome = "not_found";
                return CharacterResult<CharacterImageContent>.Failure(CharacterValidation.NotFound());
            }

            var image = await images.OpenReadAsync(character.ImageObjectKey, cancellationToken);
            if (image is null)
            {
                outcome = "not_found";
                return CharacterResult<CharacterImageContent>.Failure(CharacterValidation.NotFound());
            }

            outcome = "success";
            return CharacterResult<CharacterImageContent>.Success(image);
        }
        finally
        {
            metrics.OperationCompleted("image", outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

internal sealed record CharacterAccess(CampaignRole? Role, CharacterError? Error);

internal static class CharacterAuthorization
{
    public static async Task<CharacterAccess> AuthorizeAsync(
        ICampaignAccessReader campaignAccess, Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return new CharacterAccess(null, Forbidden());
        }

        var access = await campaignAccess.GetAccessAsync(campaignId, userId, cancellationToken);
        if (!access.Exists)
        {
            return new CharacterAccess(null, new CharacterError(
                "campaign.not_found", CharacterErrorType.NotFound, "No se ha encontrado la campaña."));
        }

        return access.Role is null
            ? new CharacterAccess(null, Forbidden())
            : new CharacterAccess(access.Role, null);
    }

    public static CharacterError Forbidden() => new(
        "character.forbidden", CharacterErrorType.Forbidden, "No tienes permiso para realizar esta operación.");

    public static string Outcome(CharacterError error) => error.Type switch
    {
        CharacterErrorType.Forbidden => "forbidden",
        CharacterErrorType.NotFound => "not_found",
        CharacterErrorType.Validation => "validation",
        CharacterErrorType.Conflict => "conflict",
        _ => "failure",
    };
}

internal static class CharacterValidation
{
    public static CharacterError? Required(string? name, int? armorClass, int? initiative)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["El nombre es obligatorio."];
        if (armorClass is null) errors["armorClass"] = ["La CA es obligatoria."];
        if (initiative is null) errors["initiative"] = ["La iniciativa es obligatoria."];
        if (errors.Count > 0) return Invalid(errors);

        try
        {
            _ = PlayerCharacter.Create(Guid.NewGuid(), null, name!, armorClass!.Value, initiative!.Value,
                null, null, null, false, DateTimeOffset.UtcNow);
            return null;
        }
        catch (ArgumentException exception)
        {
            return FromException(exception);
        }
    }

    public static CharacterError FromException(ArgumentException exception)
    {
        var field = exception.ParamName switch
        {
            "armorClass" => "armorClass",
            "initiative" => "initiative",
            "objectKey" => "image",
            "ownerUserId" => "ownerUserId",
            _ => "name",
        };
        return Invalid(new Dictionary<string, string[]> { [field] = [exception.Message] });
    }

    public static CharacterError InvalidImage(string message) =>
        Invalid(new Dictionary<string, string[]> { ["image"] = [message] });
    public static CharacterError InvalidOwner() =>
        Invalid(new Dictionary<string, string[]> { ["ownerUserId"] = ["El propietario debe ser un jugador aceptado de la campaña."] });
    public static CharacterError NotFound() => new(
        "character.not_found", CharacterErrorType.NotFound, "No se ha encontrado el personaje.");
    public static CharacterError ActiveConflict() => new(
        "character.active_conflict", CharacterErrorType.Conflict,
        "No se ha podido conservar un único personaje activo. Vuelve a intentarlo.");
    private static CharacterError Invalid(IReadOnlyDictionary<string, string[]> errors) => new(
        "character.invalid", CharacterErrorType.Validation, "El personaje no es válido.", errors);
}
