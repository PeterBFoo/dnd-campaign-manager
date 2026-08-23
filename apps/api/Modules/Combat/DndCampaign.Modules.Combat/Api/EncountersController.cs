using System.Security.Claims;
using DndCampaign.Modules.Combat.Application.Abstractions;
using DndCampaign.Modules.Combat.Application.Encounters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.Combat.Api;

[ApiController]
[Authorize]
[Route("api/v1/campaigns/{campaignId:guid}/encounters")]
internal sealed class EncountersController(EncounterApplication application) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await application.ListAsync(
            new ListEncountersQuery(GetUserId(), campaignId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await application.GetActiveAsync(
            new GetActiveEncounterQuery(GetUserId(), campaignId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpGet("{encounterId:guid}")]
    public async Task<IActionResult> GetAsync(
        Guid campaignId,
        Guid encounterId,
        CancellationToken cancellationToken)
    {
        var result = await application.GetAsync(
            new GetEncounterQuery(GetUserId(), campaignId, encounterId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        Guid campaignId,
        [FromBody] CreateEncounterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await application.CreateAsync(
            new CreateEncounterCommand(GetUserId(), campaignId, request.Name), cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/campaigns/{campaignId}/encounters/{result.Value!.Id}", result.Value)
            : MapError(result.Error!);
    }

    [HttpPut("{encounterId:guid}")]
    public Task<IActionResult> RenameAsync(
        Guid campaignId,
        Guid encounterId,
        [FromBody] RenameEncounterRequest request,
        CancellationToken cancellationToken) => MapAsync(application.RenameAsync(
            new RenameEncounterCommand(
                GetUserId(), campaignId, encounterId, request.Name, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("{encounterId:guid}/characters")]
    public Task<IActionResult> AddCharacterAsync(
        Guid campaignId,
        Guid encounterId,
        [FromBody] AddCharacterRequest request,
        CancellationToken cancellationToken) => MapAsync(application.AddCharacterAsync(
            new AddCharacterCommand(
                GetUserId(), campaignId, encounterId, request.CharacterId,
                request.Initiative, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("{encounterId:guid}/enemies")]
    public Task<IActionResult> AddEnemyAsync(
        Guid campaignId,
        Guid encounterId,
        [FromBody] AddEnemyRequest request,
        CancellationToken cancellationToken) => MapAsync(application.AddEnemyAsync(
            new AddEnemyCommand(
                GetUserId(), campaignId, encounterId, request.Name, request.Initiative,
                request.ArmorClass, request.MaximumHitPoints, request.Quantity, request.ExpectedVersion),
            cancellationToken));

    [HttpPut("{encounterId:guid}/participants/{participantId:guid}/initiative")]
    public Task<IActionResult> ChangeInitiativeAsync(
        Guid campaignId,
        Guid encounterId,
        Guid participantId,
        [FromBody] ChangeInitiativeRequest request,
        CancellationToken cancellationToken) => MapAsync(application.ChangeInitiativeAsync(
            new ChangeInitiativeCommand(
                GetUserId(), campaignId, encounterId, participantId,
                request.Initiative, request.ExpectedVersion),
            cancellationToken));

    [HttpDelete("{encounterId:guid}/participants/{participantId:guid}")]
    public Task<IActionResult> RemoveParticipantAsync(
        Guid campaignId,
        Guid encounterId,
        Guid participantId,
        [FromQuery] long? expectedVersion,
        CancellationToken cancellationToken) => MapAsync(application.RemoveParticipantAsync(
            new RemoveParticipantCommand(
                GetUserId(), campaignId, encounterId, participantId, expectedVersion),
            cancellationToken));

    [HttpPut("{encounterId:guid}/initiative-order")]
    public Task<IActionResult> ConfirmInitiativeOrderAsync(
        Guid campaignId,
        Guid encounterId,
        [FromBody] ConfirmInitiativeOrderRequest request,
        CancellationToken cancellationToken) => MapAsync(application.ConfirmInitiativeOrderAsync(
            new ConfirmInitiativeOrderCommand(
                GetUserId(), campaignId, encounterId, request.ParticipantIds, request.ExpectedVersion),
            cancellationToken));

    [HttpPut("{encounterId:guid}/active")]
    public Task<IActionResult> ActivateAsync(
        Guid campaignId,
        Guid encounterId,
        [FromBody] VersionedRequest request,
        CancellationToken cancellationToken) => MapAsync(application.ActivateAsync(
            new ActivateEncounterCommand(
                GetUserId(), campaignId, encounterId, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("{encounterId:guid}/turns/advance")]
    public Task<IActionResult> AdvanceAsync(
        Guid campaignId,
        Guid encounterId,
        [FromBody] VersionedRequest request,
        CancellationToken cancellationToken) => MapAsync(application.AdvanceAsync(
            new AdvanceTurnCommand(
                GetUserId(), campaignId, encounterId, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("{encounterId:guid}/enemies/{participantId:guid}/members/{memberId:guid}/hit-points")]
    public Task<IActionResult> AdjustHitPointsAsync(
        Guid campaignId,
        Guid encounterId,
        Guid participantId,
        Guid memberId,
        [FromBody] AdjustHitPointsRequest request,
        CancellationToken cancellationToken) => MapAsync(application.AdjustHitPointsAsync(
            new AdjustEnemyHitPointsCommand(
                GetUserId(), campaignId, encounterId, participantId, memberId,
                request.Kind, request.Amount, request.ExpectedVersion),
            cancellationToken));

    [HttpPut("{encounterId:guid}/finished")]
    public Task<IActionResult> FinishAsync(
        Guid campaignId,
        Guid encounterId,
        [FromBody] VersionedRequest request,
        CancellationToken cancellationToken) => MapAsync(application.FinishAsync(
            new FinishEncounterCommand(
                GetUserId(), campaignId, encounterId, request.ExpectedVersion),
            cancellationToken));

    [HttpDelete("{encounterId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid campaignId,
        Guid encounterId,
        [FromQuery] long? expectedVersion,
        CancellationToken cancellationToken)
    {
        var result = await application.DeleteAsync(new DeleteEncounterCommand(
            GetUserId(), campaignId, encounterId, expectedVersion), cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    private static async Task<IActionResult> MapAsync(Task<CombatResult<DmEncounterDto>> operation)
    {
        var result = await operation;
        return result.IsSuccess ? new OkObjectResult(result.Value) : MapError(result.Error!);
    }

    private Guid GetUserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

    private static IActionResult MapError(CombatError error) => error.Type switch
    {
        CombatErrorType.Validation => new BadRequestObjectResult(new ValidationProblemDetails(
            error.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "El encuentro no es válido.",
        }),
        CombatErrorType.Forbidden => new ForbidResult(),
        CombatErrorType.NotFound => new NotFoundObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "No se ha encontrado el recurso solicitado.",
        }),
        CombatErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "No se ha podido completar la operación sobre el encuentro.",
            Detail = error.Description,
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}

internal sealed class CreateEncounterRequest
{
    public string? Name { get; set; }
}

internal sealed class RenameEncounterRequest
{
    public string? Name { get; set; }

    public long? ExpectedVersion { get; set; }
}

internal sealed class AddCharacterRequest
{
    public Guid? CharacterId { get; set; }

    public int? Initiative { get; set; }

    public long? ExpectedVersion { get; set; }
}

internal sealed class AddEnemyRequest
{
    public string? Name { get; set; }

    public int? Initiative { get; set; }

    public int? ArmorClass { get; set; }

    public int? MaximumHitPoints { get; set; }

    public int? Quantity { get; set; }

    public long? ExpectedVersion { get; set; }
}

internal sealed class ChangeInitiativeRequest
{
    public int? Initiative { get; set; }

    public long? ExpectedVersion { get; set; }
}

internal sealed class ConfirmInitiativeOrderRequest
{
    public Guid[]? ParticipantIds { get; set; }

    public long? ExpectedVersion { get; set; }
}

internal sealed class VersionedRequest
{
    public long? ExpectedVersion { get; set; }
}

internal sealed class AdjustHitPointsRequest
{
    public string? Kind { get; set; }

    public int? Amount { get; set; }

    public long? ExpectedVersion { get; set; }
}
