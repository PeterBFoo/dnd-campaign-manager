using System.Diagnostics;
using System.Security.Claims;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using DndCampaign.Modules.AdventureCatalog.Contracts.Campaigns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.AdventureCatalog.Api;

[ApiController]
[Authorize]
[Route("api/v1/adventure-modules")]
internal sealed class AdventureModuleCampaignController(
    IAdventureModuleCampaignReader modules,
    GetAdventureModuleCoverHandler getCover,
    IAdventureCatalogMetrics metrics) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<AdventureModuleCampaignSummary>>> OptionsAsync(
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var options = await modules.ListOptionsAsync(cancellationToken);
            metrics.OperationCompleted("campaign_options", "success", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Ok(options);
        }
        catch
        {
            metrics.OperationCompleted("campaign_options", "failure", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    [HttpGet("{moduleId:guid}/cover")]
    public async Task<IActionResult> CoverAsync(Guid moduleId, CancellationToken cancellationToken)
    {
        var actor = new AdventureCatalogActor(
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : Guid.Empty,
            User.HasClaim("platform_admin", "true"));
        var result = await getCover.HandleAsync(
            new GetAdventureModuleCoverQuery(actor, moduleId), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error!.Type == Application.Abstractions.AdventureCatalogErrorType.Forbidden
                ? Forbid()
                : NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Value!.Content, result.Value.ContentType, enableRangeProcessing: false);
    }
}
