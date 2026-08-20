using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DndCampaign.Api.Api;

[ApiController]
[Route("api/v1/invitations")]
public sealed class InvitationController(
    InvitationService invitationService) : ControllerBase
{
    private readonly InvitationService _invitationService = invitationService;

    [HttpPost("accept")]
    [EnableRateLimiting("invitation-acceptance")]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _invitationService.AcceptInvitationAsync(
            request,
            User,
            cancellationToken);

        return result.Status switch
        {
            AcceptInvitationStatus.Accepted =>
                Ok(result.Response),

            AcceptInvitationStatus.InvalidCredentials =>
                InvalidCredentials(result.Errors),

            AcceptInvitationStatus.Unauthorized =>
                Unauthorized(),

            AcceptInvitationStatus.Forbidden =>
                Forbid(),

            AcceptInvitationStatus.NotFound
                or AcceptInvitationStatus.Expired
                or AcceptInvitationStatus.AlreadyAccepted =>
                InvalidInvitation(),

            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private IActionResult InvalidCredentials(
        IEnumerable<IdentityAccountValidationErrors> errors)
    {
        return BadRequest(
            IdentityValidationProblemFactory.Create(errors));
    }

    private IActionResult InvalidInvitation()
    {
        return StatusCode(
            StatusCodes.Status410Gone,
            new ProblemDetails
            {
                Status = StatusCodes.Status410Gone,
                Title = "La invitación no está disponible.",
                Detail = "Puede haber caducado, haber sido revocada o haberse utilizado anteriormente."
            });
    }
}