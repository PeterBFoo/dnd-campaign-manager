using System.Security.Claims;
using DndCampaign.Modules.Campaigns.Application.Abstractions;
using DndCampaign.Modules.Campaigns.Application.Campaigns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.Campaigns.Api;

[ApiController]
[Authorize]
[Route("api/v1/campaigns")]
internal sealed class CampaignsController(
    CreateCampaignHandler createCampaign,
    ListCampaignsHandler listCampaigns,
    GetCampaignHandler getCampaign) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IEnumerable<CampaignResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var campaigns = await listCampaigns.HandleAsync(
            new ListCampaignsQuery(GetUserId()),
            cancellationToken);
        return Ok(campaigns.Select(ToResponse));
    }

    [HttpPost]
    [ProducesResponseType<CampaignResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        CreateCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createCampaign.HandleAsync(
            new CreateCampaignCommand(GetUserId(), request.Name),
            cancellationToken);
        return result.IsSuccess
            ? Created(
                $"/api/v1/campaigns/{result.Value!.Id}",
                ToResponse(result.Value))
            : MapError(result.Error!);
    }

    [HttpGet("{campaignId:guid}")]
    [ProducesResponseType<CampaignResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var result = await getCampaign.HandleAsync(
            new GetCampaignQuery(GetUserId(), campaignId),
            cancellationToken);
        return result.IsSuccess ? Ok(ToResponse(result.Value!)) : MapError(result.Error!);
    }

    private Guid GetUserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var userId)
        ? userId
        : Guid.Empty;

    private static CampaignResponse ToResponse(CampaignDto campaign) => new(
        campaign.Id,
        campaign.Name,
        campaign.Role,
        campaign.AdventureModuleId,
        campaign.CreatedAt);

    private static IActionResult MapError(CampaignError error) => error.Type switch
    {
        CampaignErrorType.Validation => new BadRequestObjectResult(new ValidationProblemDetails(
            error.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "La campaña no es válida.",
        }),
        CampaignErrorType.Forbidden => new ForbidResult(),
        CampaignErrorType.NotFound => new NotFoundResult(),
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}

internal sealed record CreateCampaignRequest(string? Name);

internal sealed record CampaignResponse(
    Guid Id,
    string Name,
    string Role,
    Guid? AdventureModuleId,
    DateTimeOffset CreatedAt);
