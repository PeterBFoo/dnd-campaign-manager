using System.Security.Claims;
using DndCampaign.Modules.AdventureCatalog.Application.Abstractions;
using DndCampaign.Modules.AdventureCatalog.Application.AdventureModules;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.AdventureCatalog.Api;

[ApiController]
[Authorize(Policy = "platform-admin")]
[Route("api/v1/admin/adventure-modules")]
internal sealed class AdventureModulesController(
    ListAdventureModulesHandler listModules,
    GetAdventureModuleHandler getModule,
    CreateAdventureModuleHandler createModule,
    UpdateAdventureModuleHandler updateModule,
    DeleteAdventureModuleHandler deleteModule,
    GetAdventureModuleCoverHandler getCover) : ControllerBase
{
    private const long MaximumRequestSize = 11 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var result = await listModules.HandleAsync(
            new ListAdventureModulesQuery(GetActor()), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpGet("{moduleId:guid}")]
    public async Task<IActionResult> GetAsync(Guid moduleId, CancellationToken cancellationToken)
    {
        var result = await getModule.HandleAsync(
            new GetAdventureModuleQuery(GetActor(), moduleId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumRequestSize)]
    public async Task<IActionResult> CreateAsync(
        [FromForm] CreateAdventureModuleForm request,
        CancellationToken cancellationToken)
    {
        await using var coverStream = request.Cover?.OpenReadStream();
        var result = await createModule.HandleAsync(new CreateAdventureModuleCommand(
            GetActor(),
            request.Name,
            request.Description,
            request.GetTextProvenance(),
            ToUpload(request.Cover, coverStream),
            request.Cover is null ? null : request.GetCoverProvenance()), cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/admin/adventure-modules/{result.Value!.Id}", result.Value)
            : MapError(result.Error!);
    }

    [HttpPut("{moduleId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumRequestSize)]
    public async Task<IActionResult> UpdateAsync(
        Guid moduleId,
        [FromForm] UpdateAdventureModuleForm request,
        CancellationToken cancellationToken)
    {
        await using var coverStream = request.Cover?.OpenReadStream();
        var coverProvenance = request.HasCoverProvenance()
            ? request.GetCoverProvenance()
            : null;
        var result = await updateModule.HandleAsync(new UpdateAdventureModuleCommand(
            GetActor(),
            moduleId,
            request.Name,
            request.Description,
            request.GetTextProvenance(),
            ToUpload(request.Cover, coverStream),
            coverProvenance,
            request.RemoveCover,
            request.ExpectedVersion), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpDelete("{moduleId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid moduleId,
        [FromQuery] long expectedVersion,
        CancellationToken cancellationToken)
    {
        var result = await deleteModule.HandleAsync(
            new DeleteAdventureModuleCommand(GetActor(), moduleId, expectedVersion), cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    [HttpGet("{moduleId:guid}/cover")]
    public async Task<IActionResult> CoverAsync(Guid moduleId, CancellationToken cancellationToken)
    {
        var result = await getCover.HandleAsync(
            new GetAdventureModuleCoverQuery(GetActor(), moduleId), cancellationToken);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Value!.Content, result.Value.ContentType, enableRangeProcessing: false);
    }

    private AdventureCatalogActor GetActor() => new(
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : Guid.Empty,
        User.HasClaim("platform_admin", "true"));

    private static AdventureModuleCoverUpload? ToUpload(IFormFile? file, Stream? content) =>
        file is null || content is null
            ? null
            : new AdventureModuleCoverUpload(content, file.Length, file.ContentType);

    private static IActionResult MapError(AdventureCatalogError error) => error.Type switch
    {
        AdventureCatalogErrorType.Validation => new BadRequestObjectResult(new ValidationProblemDetails(
            error.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "El módulo de aventura no es válido.",
        }),
        AdventureCatalogErrorType.Forbidden => new ForbidResult(),
        AdventureCatalogErrorType.NotFound => new NotFoundResult(),
        AdventureCatalogErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "No se ha podido guardar el módulo de aventura.",
            Detail = error.Description,
            Extensions = { ["code"] = error.Code },
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}

internal class AdventureModuleFormBase
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? TextOriginKind { get; init; }

    public string? TextSourceReference { get; init; }

    public string? TextRightsBasis { get; init; }

    public string? TextAttribution { get; init; }

    public EditorialProvenanceForm? TextProvenance { get; init; }

    public IFormFile? Cover { get; init; }

    public string? CoverOriginKind { get; init; }

    public string? CoverSourceReference { get; init; }

    public string? CoverRightsBasis { get; init; }

    public string? CoverAttribution { get; init; }

    public EditorialProvenanceForm? CoverProvenance { get; init; }

    public EditorialProvenanceInput GetTextProvenance() => new(
        TextProvenance?.OriginKind ?? TextOriginKind,
        TextProvenance?.SourceReference ?? TextSourceReference,
        TextProvenance?.RightsBasis ?? TextRightsBasis,
        TextProvenance?.Attribution ?? TextAttribution);

    public EditorialProvenanceInput GetCoverProvenance() => new(
        CoverProvenance?.OriginKind ?? CoverOriginKind,
        CoverProvenance?.SourceReference ?? CoverSourceReference,
        CoverProvenance?.RightsBasis ?? CoverRightsBasis,
        CoverProvenance?.Attribution ?? CoverAttribution);

    public bool HasCoverProvenance() =>
        CoverProvenance is not null
        || !string.IsNullOrWhiteSpace(CoverOriginKind)
        || !string.IsNullOrWhiteSpace(CoverSourceReference)
        || !string.IsNullOrWhiteSpace(CoverRightsBasis)
        || !string.IsNullOrWhiteSpace(CoverAttribution);
}

internal sealed class EditorialProvenanceForm
{
    public string? OriginKind { get; init; }
    public string? SourceReference { get; init; }
    public string? RightsBasis { get; init; }
    public string? Attribution { get; init; }
}

internal sealed class CreateAdventureModuleForm : AdventureModuleFormBase
{
}

internal sealed class UpdateAdventureModuleForm : AdventureModuleFormBase
{
    public bool RemoveCover { get; init; }

    public long ExpectedVersion { get; init; }
}
