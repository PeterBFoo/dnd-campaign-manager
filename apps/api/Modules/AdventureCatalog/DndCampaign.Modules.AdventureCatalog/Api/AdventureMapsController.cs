using System.Security.Claims;
using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Maps;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.AdventureCatalog.Api;

[ApiController, Authorize, Route("api/v1/admin/adventure-modules/{moduleId:guid}/maps")]
internal sealed class AdventureMapsController(AdventureMapService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(Guid moduleId, CancellationToken ct) => Map(await service.ListAdminAsync(Actor(), moduleId, ct));
    [HttpGet("chapters")] public async Task<IActionResult> Chapters(Guid moduleId, CancellationToken ct) => Map(await service.ChaptersAsync(Actor(), moduleId, ct));
    [HttpGet("{mapId:guid}")] public async Task<IActionResult> Get(Guid moduleId, Guid mapId, CancellationToken ct) => Map(await service.GetAdminAsync(Actor(), moduleId, mapId, ct));
    [HttpPost] public async Task<IActionResult> Create(Guid moduleId, [FromBody] MapTextRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(Actor(), moduleId, request.Name, request.Description, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { moduleId, mapId = result.Value!.Id }, result.Value) : Error(result.Error!);
    }
    [HttpPut("{mapId:guid}")] public async Task<IActionResult> Update(Guid moduleId, Guid mapId, [FromBody] MapTextRequest request, CancellationToken ct) => Map(await service.UpdateAsync(Actor(), moduleId, mapId, request.Name, request.Description, request.ExpectedVersion, ct));
    [HttpDelete("{mapId:guid}")] public async Task<IActionResult> Delete(Guid moduleId, Guid mapId, [FromQuery] long expectedVersion, CancellationToken ct)
    { var result = await service.DeleteAsync(Actor(), moduleId, mapId, expectedVersion, ct); return result.IsSuccess ? NoContent() : Error(result.Error!); }
    [HttpPut("{mapId:guid}/image"), RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> PutImage(Guid moduleId, Guid mapId, [FromForm] MapImageForm form, CancellationToken ct)
    {
        if (form.Image is null) return BadRequest(new ProblemDetails { Title = "La imagen es obligatoria." });
        await using var content = form.Image.OpenReadStream();
        return Map(await service.PutImageAsync(Actor(), moduleId, mapId, new(new(content, form.Image.Length, form.Image.ContentType), new(form.OriginKind, form.SourceReference, form.RightsBasis, form.Attribution), form.ExpectedVersion), ct));
    }
    [HttpDelete("{mapId:guid}/image")] public async Task<IActionResult> RemoveImage(Guid moduleId, Guid mapId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.RemoveImageAsync(Actor(), moduleId, mapId, expectedVersion, ct));
    [HttpGet("{mapId:guid}/image")] public async Task<IActionResult> Image(Guid moduleId, Guid mapId, CancellationToken ct) => FileResult(await service.OpenImageAdminAsync(Actor(), moduleId, mapId, ct));
    [HttpPut("{mapId:guid}/chapters/{chapterId:guid}")] public async Task<IActionResult> AddChapter(Guid moduleId, Guid mapId, Guid chapterId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.SetChapterAsync(Actor(), moduleId, mapId, chapterId, expectedVersion, true, ct));
    [HttpDelete("{mapId:guid}/chapters/{chapterId:guid}")] public async Task<IActionResult> RemoveChapter(Guid moduleId, Guid mapId, Guid chapterId, [FromQuery] long expectedVersion, CancellationToken ct) => Map(await service.SetChapterAsync(Actor(), moduleId, mapId, chapterId, expectedVersion, false, ct));

    private AdventureCatalogActor Actor() => new(Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty, User.HasClaim("platform_admin", "true"));
    private IActionResult FileResult(AdventureCatalogResult<AdventureMapImageContent> result)
    { if (!result.IsSuccess) return Error(result.Error!); Response.Headers.XContentTypeOptions = "nosniff"; Response.Headers.CacheControl = "private, no-store"; return File(result.Value!.Content, result.Value.ContentType); }
    private IActionResult Map<T>(AdventureCatalogResult<T> result) => result.IsSuccess ? Ok(result.Value) : Error(result.Error!);
    private IActionResult Error(AdventureCatalogError error) => error.Type switch
    {
        AdventureCatalogErrorType.Forbidden => Forbid(), AdventureCatalogErrorType.NotFound => NotFound(),
        AdventureCatalogErrorType.Conflict => Conflict(new ProblemDetails { Title = "Conflicto de versión.", Detail = error.Description, Extensions = { ["code"] = error.Code } }),
        AdventureCatalogErrorType.Validation => BadRequest(new ValidationProblemDetails(error.ValidationErrors?.ToDictionary(x => x.Key, x => x.Value) ?? []) { Title = "El mapa no es válido." }),
        _ => throw new ArgumentOutOfRangeException()
    };
}

[ApiController, Authorize, Route("api/v1/campaigns/{campaignId:guid}/adventure/maps")]
internal sealed class CampaignAdventureMapsController(AdventureMapService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(Guid campaignId, CancellationToken ct) => Map(await service.ListCampaignAsync(UserId(), campaignId, ct));
    [HttpGet("{mapId:guid}")] public async Task<IActionResult> Get(Guid campaignId, Guid mapId, CancellationToken ct) => Map(await service.GetCampaignAsync(UserId(), campaignId, mapId, ct));
    [HttpGet("{mapId:guid}/image")] public async Task<IActionResult> Image(Guid campaignId, Guid mapId, CancellationToken ct)
    { var result = await service.OpenImageCampaignAsync(UserId(), campaignId, mapId, ct); if (!result.IsSuccess) return result.Error!.Type == AdventureCatalogErrorType.Forbidden ? Forbid() : NotFound(); Response.Headers.XContentTypeOptions = "nosniff"; Response.Headers.CacheControl = "private, no-store"; return File(result.Value!.Content, result.Value.ContentType); }
    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private IActionResult Map<T>(AdventureCatalogResult<T> result) => result.IsSuccess ? Ok(result.Value) : result.Error!.Type == AdventureCatalogErrorType.Forbidden ? Forbid() : NotFound();
}

internal sealed record MapTextRequest(string? Name, string? Description, long ExpectedVersion = 0);
internal sealed class MapImageForm
{
    public IFormFile? Image { get; init; }
    public string? OriginKind { get; init; }
    public string? SourceReference { get; init; }
    public string? RightsBasis { get; init; }
    public string? Attribution { get; init; }
    public long ExpectedVersion { get; init; }
}
