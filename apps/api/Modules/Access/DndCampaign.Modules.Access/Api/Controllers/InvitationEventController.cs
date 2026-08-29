using System.Text.Json.Serialization;
using System.Text.Json;
using DndCampaign.Modules.Access.Application.Ports.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;

namespace DndCampaign.Modules.Access.Api.Controllers;

[ApiController]
[Route("internal/events")]
[Authorize(AuthenticationSchemes = "EventGrid", Policy = "event-grid-delivery")]
internal sealed class InvitationEventController(
    IInvitationEmailDeliveryService deliveryService,
    IInvitationPendingEventReplayer replayService) : ControllerBase
{
    [HttpOptions("invitation-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ValidateCloudEventsWebhook()
    {
        if (!Request.Headers.TryGetValue("WebHook-Request-Origin", out var requestOrigin)
            || string.IsNullOrWhiteSpace(requestOrigin.ToString()))
        {
            return BadRequest();
        }

        Response.Headers["WebHook-Allowed-Origin"] = requestOrigin.ToString();
        Response.Headers["WebHook-Allowed-Rate"] = "*";
        Response.Headers.Allow = "POST";
        return Ok();
    }

    [HttpPost("invitation-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Receive(
        [FromBody] IReadOnlyList<CloudEventRequest>? events,
        CancellationToken cancellationToken)
    {
        if (events is null || events.Count != 1)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Se esperaba exactamente un evento CloudEvent.",
            });
        }

        var cloudEvent = events[0];
        if (string.Equals(cloudEvent.Type, "Microsoft.EventGrid.SubscriptionValidationEvent", StringComparison.Ordinal)
            && cloudEvent.Data is { } validationData
            && validationData.TryGetProperty("validationCode", out var validationCode))
        {
            return Ok(new { validationResponse = validationCode.GetString() });
        }

        var data = cloudEvent.Data?.Deserialize<CloudEventData>();
        if (!string.Equals(cloudEvent.Type, "access.invitation-email.requested.v1", StringComparison.Ordinal)
            || data is null
            || !Guid.TryParse(cloudEvent.Id, out var eventId)
            || data.InvitationId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "El evento de invitación no es válido.",
            });
        }

        InvitationEmailDeliveryResult result;
        try
        {
            result = await deliveryService.ProcessAsync(
                eventId,
                data.InvitationId,
                data.EncryptedToken,
                cancellationToken);
        }
        catch (DbException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (TimeoutException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        return result switch
        {
            InvitationEmailDeliveryResult.Retryable => StatusCode(StatusCodes.Status503ServiceUnavailable),
            InvitationEmailDeliveryResult.Invalid => BadRequest(),
            _ => NoContent(),
        };
    }

    [HttpPost("invitation-email/replay-pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplayPending(CancellationToken cancellationToken)
    {
        var count = await replayService.ReplayAsync(cancellationToken);
        return Ok(new { replayed = count });
    }

    internal sealed record CloudEventRequest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("data")] JsonElement? Data);

    internal sealed record CloudEventData(
        [property: JsonPropertyName("invitationId")] Guid InvitationId,
        [property: JsonPropertyName("encryptedToken")] string EncryptedToken,
        [property: JsonPropertyName("keyVersion")] string? KeyVersion,
        [property: JsonPropertyName("schemaVersion")] string? SchemaVersion);
}
