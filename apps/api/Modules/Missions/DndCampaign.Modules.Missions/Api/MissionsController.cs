using System.Security.Claims;
using DndCampaign.Modules.Missions.Application.Abstractions;
using DndCampaign.Modules.Missions.Application.Missions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.Missions.Api;

[ApiController]
[Authorize]
[Route("api/v1/campaigns/{campaignId:guid}/missions")]
internal sealed class MissionsController(
    ListMissionsHandler listMissions,
    CreateMissionHandler createMission,
    UpdateMissionHandler updateMission,
    SetMainMissionHandler setMainMission,
    ClearMainMissionHandler clearMainMission,
    DeleteMissionHandler deleteMission) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await listMissions.HandleAsync(
            new ListMissionsQuery(GetUserId(), campaignId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        Guid campaignId,
        [FromBody] CreateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createMission.HandleAsync(
            new CreateMissionCommand(
                GetUserId(), campaignId, request.Title, request.Description, request.IsMain),
            cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/campaigns/{campaignId}/missions/{result.Value!.Id}", result.Value)
            : MapError(result.Error!);
    }

    [HttpPut("{missionId:guid}")]
    public async Task<IActionResult> UpdateAsync(
        Guid campaignId,
        Guid missionId,
        [FromBody] UpdateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await updateMission.HandleAsync(
            new UpdateMissionCommand(
                GetUserId(), campaignId, missionId, request.Title, request.Description, request.Status),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPut("{missionId:guid}/main")]
    public async Task<IActionResult> SetMainAsync(
        Guid campaignId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var result = await setMainMission.HandleAsync(
            new SetMainMissionCommand(GetUserId(), campaignId, missionId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpDelete("{missionId:guid}/main")]
    public async Task<IActionResult> ClearMainAsync(
        Guid campaignId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var result = await clearMainMission.HandleAsync(
            new ClearMainMissionCommand(GetUserId(), campaignId, missionId), cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    [HttpDelete("{missionId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid campaignId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var result = await deleteMission.HandleAsync(
            new DeleteMissionCommand(GetUserId(), campaignId, missionId), cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    private Guid GetUserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

    private static IActionResult MapError(MissionError error) => error.Type switch
    {
        MissionErrorType.Validation => new BadRequestObjectResult(new ValidationProblemDetails(
            error.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "La misión no es válida.",
        }),
        MissionErrorType.Forbidden => new ForbidResult(),
        MissionErrorType.NotFound => new NotFoundObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "No se ha encontrado el recurso solicitado.",
        }),
        MissionErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "No se ha podido completar la operación sobre la misión.",
            Detail = error.Description,
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}

internal sealed class CreateMissionRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public bool IsMain { get; set; }
}

internal sealed class UpdateMissionRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }
}
