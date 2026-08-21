using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DndCampaign.Api.Api;

[ApiController]
[Route("api/v1/invitations")]
public sealed class InvitationAcceptanceController(IInvitationAcceptanceService acceptanceService) : ControllerBase
{
    [HttpPost("preview")]
    [EnableRateLimiting("invitation-acceptance")]
    public async Task<IActionResult> Preview(
        [FromBody] InvitationTokenRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await acceptanceService.PreviewAsync(
            InvitationAcceptanceHttpMapping.ToCommand(request),
            cancellationToken);
        return Ok(InvitationAcceptanceHttpMapping.ToResponse(outcome));
    }

    [HttpPost("accept")]
    [EnableRateLimiting("invitation-acceptance")]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await acceptanceService.AcceptAsync(
            InvitationAcceptanceHttpMapping.ToCommand(request),
            InvitationAcceptanceHttpMapping.ToActor(User),
            cancellationToken);

        return result.Status switch
        {
            AcceptInvitationStatus.Accepted =>
                Ok(InvitationAcceptanceHttpMapping.ToResponse(result.Outcome!)),
            AcceptInvitationStatus.InvalidCredentials =>
                BadRequest(IdentityValidationProblemFactory.Create(result.Errors)),
            AcceptInvitationStatus.Unauthorized => Unauthorized(),
            AcceptInvitationStatus.Forbidden => Forbid(),
            AcceptInvitationStatus.NotFound
                or AcceptInvitationStatus.Expired
                or AcceptInvitationStatus.AlreadyAccepted =>
                InvalidInvitation(),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private IActionResult InvalidInvitation() =>
        StatusCode(
            StatusCodes.Status410Gone,
            new ProblemDetails
            {
                Status = StatusCodes.Status410Gone,
                Title = "La invitación no está disponible.",
                Detail = "Puede haber caducado, haber sido revocada o haberse utilizado anteriormente.",
            });
}
