using System.Security.Claims;
using DndCampaign.Modules.Journal.Application.Abstractions;
using DndCampaign.Modules.Journal.Application.Entries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Modules.Journal.Api;

[ApiController]
[Authorize]
[Route("api/v1/campaigns/{campaignId:guid}/journal/entries")]
internal sealed class JournalEntriesController(
    ListJournalEntriesHandler listEntries,
    CreateJournalEntryHandler createEntry,
    UpdateJournalEntryHandler updateEntry,
    DeleteJournalEntryHandler deleteEntry) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        Guid campaignId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await listEntries.HandleAsync(
            new ListJournalEntriesQuery(GetUserId(), campaignId, cursor, limit), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        Guid campaignId,
        [FromBody] JournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await createEntry.HandleAsync(
            new CreateJournalEntryCommand(GetUserId(), campaignId, request.Content), cancellationToken);
        return result.IsSuccess
            ? Created(
                $"/api/v1/campaigns/{campaignId}/journal/entries/{result.Value!.Id}",
                result.Value)
            : MapError(result.Error!);
    }

    [HttpPut("{entryId:guid}")]
    public async Task<IActionResult> UpdateAsync(
        Guid campaignId,
        Guid entryId,
        [FromBody] JournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await updateEntry.HandleAsync(
            new UpdateJournalEntryCommand(GetUserId(), campaignId, entryId, request.Content), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    [HttpDelete("{entryId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid campaignId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var result = await deleteEntry.HandleAsync(
            new DeleteJournalEntryCommand(GetUserId(), campaignId, entryId), cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }

    private Guid GetUserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

    private static IActionResult MapError(JournalError error) => error.Type switch
    {
        JournalErrorType.Validation => new BadRequestObjectResult(new ValidationProblemDetails(
            error.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "La entrada de bitácora no es válida.",
        }),
        JournalErrorType.Forbidden => new ForbidResult(),
        JournalErrorType.NotFound => new NotFoundObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "No se ha encontrado el recurso solicitado.",
        }),
        JournalErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Se requiere un personaje activo.",
            Detail = error.Description,
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };
}

internal sealed class JournalEntryRequest
{
    public string? Content { get; set; }
}
