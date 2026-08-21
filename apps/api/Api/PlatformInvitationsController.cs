using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Api.Api;

[ApiController]
[Route("api/v1/platform/invitations")]
[Authorize("platform-admin")]
public sealed class PlatformInvitationsController(IPlatformInvitationService platformInvitationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var summaries = await platformInvitationService.ListAsync(new ListPlatformInvitationsCommand(), cancellationToken);
        return Ok(PlatformInvitationHttpMapping.ToResponseList(summaries));
    }

    [HttpPost]
    public async Task<IActionResult> Issue(
        [FromBody] IssueInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var summary = await platformInvitationService.IssueAsync(
            PlatformInvitationHttpMapping.ToCommand(request, User.GetUserId()),
            cancellationToken);
        return Accepted(PlatformInvitationHttpMapping.ToResponse(summary));
    }

    [HttpPost("{invitationId:guid}/resend")]
    public async Task<IActionResult> Resend(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var (status, summary) = await platformInvitationService.ResendAsync(
            new ResendInvitationCommand(invitationId),
            cancellationToken);
        return status switch
        {
            ResendInvitationStatus.Resent => Accepted(PlatformInvitationHttpMapping.ToResponse(summary!)),
            ResendInvitationStatus.NotFound => NotFound(),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var status = await platformInvitationService.RevokeAsync(
            new RevokeInvitationCommand(invitationId),
            cancellationToken);
        return status switch
        {
            RevokeInvitationStatus.Revoked => NoContent(),
            RevokeInvitationStatus.NotFound => NotFound(),
            RevokeInvitationStatus.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "La invitación ya no está pendiente.",
            }),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
