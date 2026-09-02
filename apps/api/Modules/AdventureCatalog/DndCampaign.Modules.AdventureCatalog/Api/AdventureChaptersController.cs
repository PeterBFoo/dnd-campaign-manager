using System.Security.Claims;
using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Chapters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.AdventureCatalog.Api;

[ApiController, Authorize(Policy = "platform-admin")]
[Route("api/v1/admin/adventure-modules/{moduleId:guid}/chapters")]
internal sealed class AdventureChaptersController(AdventureChapterService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(Guid moduleId, CancellationToken ct) => Map(await service.ListAdminAsync(UserId, IsAdmin, moduleId, ct));
    [HttpGet("{chapterId:guid}")] public async Task<IActionResult> Get(Guid moduleId, Guid chapterId, CancellationToken ct) => Map(await service.GetAdminAsync(UserId, IsAdmin, moduleId, chapterId, ct));
    [HttpPost] public async Task<IActionResult> Create(Guid moduleId, ChapterRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(new(UserId, IsAdmin, moduleId, null, request.Name, request.Description, request.Provenance?.ToInput() ?? EmptyProvenance, null), ct);
        return result.IsSuccess ? Created($"/api/v1/admin/adventure-modules/{moduleId}/chapters/{result.Value!.Id}", result.Value) : Map(result);
    }
    [HttpPut("{chapterId:guid}")] public async Task<IActionResult> Update(Guid moduleId, Guid chapterId, ChapterRequest request, CancellationToken ct) =>
        Map(await service.UpdateAsync(new(UserId, IsAdmin, moduleId, chapterId, request.Name, request.Description, request.Provenance?.ToInput() ?? EmptyProvenance, request.ExpectedVersion), ct));
    [HttpDelete("{chapterId:guid}")] public async Task<IActionResult> Delete(Guid moduleId, Guid chapterId, [FromQuery] long expectedVersion, CancellationToken ct)
    { var result = await service.DeleteAsync(UserId, IsAdmin, moduleId, chapterId, expectedVersion, ct); return result.IsSuccess ? NoContent() : Map(result); }
    [HttpPut("order")] public async Task<IActionResult> Order(Guid moduleId, ChapterOrderRequest request, CancellationToken ct) =>
        Map(await service.ReorderAsync(UserId, IsAdmin, moduleId, request.ExpectedIndexVersion, request.ChapterIds, ct));

    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool IsAdmin => User.HasClaim("platform_admin", "true");
    private static EditorialProvenanceInput EmptyProvenance => new(null, null, null, null);
    private IActionResult Map<T>(AdventureCatalogResult<T> result) => result.IsSuccess ? Ok(result.Value) : result.Error!.Type switch
    { AdventureCatalogErrorType.Validation => BadRequest(new ValidationProblemDetails(result.Error.ValidationErrors!.ToDictionary(entry => entry.Key, entry => entry.Value))), AdventureCatalogErrorType.Forbidden => Forbid(), AdventureCatalogErrorType.NotFound => NotFound(), AdventureCatalogErrorType.Conflict => Conflict(new ProblemDetails { Status = 409, Title = "Conflicto de capítulos", Detail = result.Error.Description, Extensions = { ["code"] = result.Error.Code } }), _ => throw new ArgumentOutOfRangeException() };
}

internal sealed record ChapterRequest(string? Name, string? Description, ChapterProvenanceRequest? Provenance, long? ExpectedVersion);
internal sealed record ChapterProvenanceRequest(string? OriginKind, string? SourceReference, string? RightsBasis, string? Attribution)
{ public EditorialProvenanceInput ToInput() => new(OriginKind, SourceReference, RightsBasis, Attribution); }
internal sealed record ChapterOrderRequest(long ExpectedIndexVersion, IReadOnlyList<Guid>? ChapterIds);

[ApiController, Authorize]
[Route("api/v1/campaigns/{campaignId:guid}/adventure/chapters")]
internal sealed class CampaignAdventureChaptersController(AdventureChapterService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(Guid campaignId, CancellationToken ct) => Map(await service.ListCampaignAsync(UserId, campaignId, ct));
    [HttpGet("{chapterId:guid}")] public async Task<IActionResult> Get(Guid campaignId, Guid chapterId, CancellationToken ct) => Map(await service.GetCampaignAsync(UserId, campaignId, chapterId, ct));
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private IActionResult Map<T>(AdventureCatalogResult<T> result) => result.IsSuccess ? Ok(result.Value) : result.Error!.Type == AdventureCatalogErrorType.Forbidden ? Forbid() : NotFound();
}
