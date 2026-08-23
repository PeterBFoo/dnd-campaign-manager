using System.Security.Claims;
using DndCampaign.Modules.Characters.Application.Abstractions;
using DndCampaign.Modules.Characters.Application.Characters;
using DndCampaign.Modules.Characters.Application.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.Characters.Api;

[ApiController]
[Authorize]
[Route("api/v1/campaigns/{campaignId:guid}/characters")]
internal sealed class CharactersController(
    ListCharactersHandler listCharacters,
    ListCharacterOwnersHandler listOwners,
    CreateCharacterHandler createCharacter,
    UpdateCharacterHandler updateCharacter,
    ActivateCharacterHandler activateCharacter,
    DeleteCharacterHandler deleteCharacter,
    GetCharacterImageHandler getImage) : ControllerBase
{
    private const long MaximumRequestSize = 6 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await listCharacters.HandleAsync(
            new ListCharactersQuery(GetUserId(), campaignId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpGet("owners")]
    public async Task<IActionResult> ListOwnersAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await listOwners.HandleAsync(
            new ListCharacterOwnersQuery(GetUserId(), campaignId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumRequestSize)]
    public async Task<IActionResult> CreateAsync(
        Guid campaignId,
        [FromForm] CharacterForm request,
        CancellationToken cancellationToken)
    {
        await using var imageStream = request.Image?.OpenReadStream();
        var result = await createCharacter.HandleAsync(new CreateCharacterCommand(
            GetUserId(), campaignId, request.Name, request.ArmorClass, request.Initiative,
            request.OwnerUserId, ToUpload(request.Image, imageStream)), cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/campaigns/{campaignId}/characters/{result.Value!.Id}", result.Value)
            : MapError(result.Error!);
    }

    [HttpPut("{characterId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumRequestSize)]
    public async Task<IActionResult> UpdateAsync(
        Guid campaignId,
        Guid characterId,
        [FromForm] UpdateCharacterForm request,
        CancellationToken cancellationToken)
    {
        await using var imageStream = request.Image?.OpenReadStream();
        var result = await updateCharacter.HandleAsync(new UpdateCharacterCommand(
            GetUserId(), campaignId, characterId, request.Name, request.ArmorClass, request.Initiative,
            request.OwnerUserId, request.RemoveImage, ToUpload(request.Image, imageStream)), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPut("{characterId:guid}/active")]
    public async Task<IActionResult> ActivateAsync(
        Guid campaignId, Guid characterId, CancellationToken cancellationToken)
    {
        var result = await activateCharacter.HandleAsync(
            new ActivateCharacterCommand(GetUserId(), campaignId, characterId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpDelete("{characterId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid campaignId, Guid characterId, CancellationToken cancellationToken)
    {
        var result = await deleteCharacter.HandleAsync(
            new DeleteCharacterCommand(GetUserId(), campaignId, characterId), cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    [HttpGet("{characterId:guid}/image")]
    public async Task<IActionResult> ImageAsync(
        Guid campaignId, Guid characterId, CancellationToken cancellationToken)
    {
        var result = await getImage.HandleAsync(
            new GetCharacterImageQuery(GetUserId(), campaignId, characterId), cancellationToken);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Value!.Content, result.Value.ContentType, enableRangeProcessing: false);
    }

    private Guid GetUserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

    private static CharacterImageUpload? ToUpload(IFormFile? file, Stream? stream) =>
        file is null || stream is null ? null : new CharacterImageUpload(stream, file.Length, file.ContentType);

    private static IActionResult MapError(CharacterError error) => error.Type switch
    {
        CharacterErrorType.Validation => new BadRequestObjectResult(new ValidationProblemDetails(
            error.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "El personaje no es válido.",
        }),
        CharacterErrorType.Forbidden => new ForbidResult(),
        CharacterErrorType.NotFound => new NotFoundResult(),
        CharacterErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflicto al guardar el personaje.",
            Detail = error.Description,
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}

internal class CharacterForm
{
    public string? Name { get; set; }
    public int? ArmorClass { get; set; }
    public int? Initiative { get; set; }
    public Guid? OwnerUserId { get; set; }
    public IFormFile? Image { get; set; }
}

internal sealed class UpdateCharacterForm : CharacterForm
{
    public bool RemoveImage { get; set; }
}
