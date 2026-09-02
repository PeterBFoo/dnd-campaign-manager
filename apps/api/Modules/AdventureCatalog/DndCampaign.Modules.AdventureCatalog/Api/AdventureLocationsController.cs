using System.Security.Claims;
using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.AdventureCatalog.Api;

[ApiController, Authorize(Policy = "platform-admin"), Route("api/v1/admin/adventure-modules/{moduleId:guid}/locations")]
internal sealed class AdventureLocationsController(AdventureLocationService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(Guid moduleId, CancellationToken ct) => Map(await service.ListAdminAsync(Actor(), moduleId, ct));
    [HttpGet("{locationId:guid}")] public async Task<IActionResult> Get(Guid moduleId, Guid locationId, CancellationToken ct) => Map(await service.GetAdminAsync(Actor(), moduleId, locationId, ct));
    [HttpPost] public async Task<IActionResult> Create(Guid moduleId, [FromBody] LocationTextRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(new(UserId(), IsAdmin(), moduleId, null, request.Name, request.Description, null), ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { moduleId, locationId = result.Value!.Id }, result.Value) : Error(result.Error!);
    }
    [HttpPut("{locationId:guid}")] public async Task<IActionResult> Update(Guid moduleId, Guid locationId, [FromBody] LocationTextRequest request, CancellationToken ct) => Map(await service.UpdateAsync(new(UserId(), IsAdmin(), moduleId, locationId, request.Name, request.Description, request.ExpectedVersion), ct));
    [HttpDelete("{locationId:guid}")] public async Task<IActionResult> Delete(Guid moduleId, Guid locationId, [FromQuery] long expectedVersion, CancellationToken ct)
    { var result = await service.DeleteAsync(Actor(), moduleId, locationId, expectedVersion, ct); return result.IsSuccess ? NoContent() : Error(result.Error!); }
    [HttpPut("{locationId:guid}/detail-map")] public async Task<IActionResult> SetDetailMap(Guid moduleId, Guid locationId, [FromBody] DetailMapRequest request, CancellationToken ct) => Map(await service.SetDetailMapAsync(Actor(), moduleId, locationId, request.MapId, request.ExpectedVersion, ct));
    [HttpDelete("{locationId:guid}/detail-map")] public async Task<IActionResult> RemoveDetailMap(Guid moduleId, Guid locationId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.SetDetailMapAsync(Actor(), moduleId, locationId, null, expectedVersion, ct));
    [HttpPost("{locationId:guid}/points-of-interest")] public async Task<IActionResult> CreatePoint(Guid moduleId, Guid locationId, [FromBody] PointRequest request, CancellationToken ct) => Map(await service.CreatePointAsync(new(UserId(), IsAdmin(), moduleId, locationId, null, request.Name, request.Description, request.X, request.Y, request.ExpectedVersion), ct));
    [HttpPut("{locationId:guid}/points-of-interest/{pointId:guid}")] public async Task<IActionResult> UpdatePoint(Guid moduleId, Guid locationId, Guid pointId, [FromBody] PointRequest request, CancellationToken ct) => Map(await service.UpdatePointAsync(new(UserId(), IsAdmin(), moduleId, locationId, pointId, request.Name, request.Description, request.X, request.Y, request.ExpectedVersion), ct));
    [HttpDelete("{locationId:guid}/points-of-interest/{pointId:guid}")] public async Task<IActionResult> DeletePoint(Guid moduleId, Guid locationId, Guid pointId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.DeletePointAsync(new(UserId(), IsAdmin(), moduleId, locationId, pointId, null, null, null, null, expectedVersion), ct));
    [HttpPut("{locationId:guid}/chapters/{chapterId:guid}")] public async Task<IActionResult> AddChapter(Guid moduleId, Guid locationId, Guid chapterId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.SetChapterAsync(Actor(), moduleId, locationId, chapterId, expectedVersion, true, ct));
    [HttpDelete("{locationId:guid}/chapters/{chapterId:guid}")] public async Task<IActionResult> RemoveChapter(Guid moduleId, Guid locationId, Guid chapterId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.SetChapterAsync(Actor(), moduleId, locationId, chapterId, expectedVersion, false, ct));
    [HttpPut("{locationId:guid}/placements/{mapId:guid}")] public async Task<IActionResult> SetPlacement(Guid moduleId, Guid locationId, Guid mapId, [FromBody] PlacementRequest request, CancellationToken ct) => Map(await service.SetPlacementAsync(Actor(), moduleId, locationId, mapId, request.X, request.Y, request.ExpectedVersion, ct));
    [HttpDelete("{locationId:guid}/placements/{mapId:guid}")] public async Task<IActionResult> RemovePlacement(Guid moduleId, Guid locationId, Guid mapId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.RemovePlacementAsync(Actor(), moduleId, locationId, mapId, expectedVersion, ct));

    private AdventureCatalogActor Actor() => new(UserId(), IsAdmin());
    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool IsAdmin() => User.HasClaim("platform_admin", "true");
    private IActionResult Map<T>(AdventureCatalogResult<T> result) => result.IsSuccess ? Ok(result.Value) : Error(result.Error!);
    private IActionResult Error(AdventureCatalogError error) => error.Type switch
    {
        AdventureCatalogErrorType.Forbidden => Forbid(),
        AdventureCatalogErrorType.NotFound => NotFound(),
        AdventureCatalogErrorType.Conflict => Conflict(new ProblemDetails { Title = "Conflicto de versión.", Detail = error.Description, Extensions = { ["code"] = error.Code } }),
        AdventureCatalogErrorType.Validation => BadRequest(new ValidationProblemDetails(error.ValidationErrors?.ToDictionary(x => x.Key, x => x.Value) ?? []) { Title = "La localización no es válida." }),
        _ => throw new ArgumentOutOfRangeException(),
    };
}

[ApiController, Authorize, Route("api/v1/campaigns/{campaignId:guid}/adventure/locations")]
internal sealed class CampaignAdventureLocationsController(AdventureLocationService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(Guid campaignId, CancellationToken ct) => Map(await service.ListCampaignAsync(UserId(), campaignId, ct));
    [HttpGet("{locationId:guid}")] public async Task<IActionResult> Get(Guid campaignId, Guid locationId, CancellationToken ct) => Map(await service.GetCampaignAsync(UserId(), campaignId, locationId, ct));
    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private IActionResult Map<T>(AdventureCatalogResult<T> result) => result.IsSuccess ? Ok(result.Value) : result.Error!.Type == AdventureCatalogErrorType.Forbidden ? Forbid() : NotFound();
}

internal sealed record LocationTextRequest(string? Name, string? Description, long ExpectedVersion = 0);
internal sealed record DetailMapRequest(Guid? MapId, long ExpectedVersion);
internal sealed record PointRequest(string? Name, string? Description, decimal? X, decimal? Y, long ExpectedVersion);
internal sealed record PlacementRequest(decimal X, decimal Y, long ExpectedVersion);
