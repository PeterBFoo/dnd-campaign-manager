using DndCampaign.Api.Api.Contracts;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Api.Api;

[ApiController]
[Route("api/v1/campaigns/{campaignId:guid}/invitations")]
[Authorize]
public sealed class CampaignInvitationsController(ICampaignInvitationService campaignInvitationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var (access, items) = await campaignInvitationService.ListAsync(
            CampaignInvitationHttpMapping.ToListCommand(campaignId, User.GetUserId()),
            cancellationToken);
        if (access == CampaignAccessStatus.Forbidden)
        {
            return Forbid();
        }

        return Ok(CampaignInvitationHttpMapping.ToResponseList(items!));
    }

    [HttpPost]
    public async Task<IActionResult> Issue(
        Guid campaignId,
        [FromBody] IssueInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var (access, summary) = await campaignInvitationService.IssueAsync(
            CampaignInvitationHttpMapping.ToIssueCommand(campaignId, request, User.GetUserId()),
            cancellationToken);
        if (access == CampaignAccessStatus.Forbidden)
        {
            return Forbid();
        }

        return Accepted(CampaignInvitationHttpMapping.ToResponse(summary!));
    }

    [HttpPost("{invitationId:guid}/resend")]
    public async Task<IActionResult> Resend(
        Guid campaignId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var (access, status, summary) = await campaignInvitationService.ResendAsync(
            CampaignInvitationHttpMapping.ToResendCommand(campaignId, User.GetUserId(), invitationId),
            cancellationToken);
        if (access == CampaignAccessStatus.Forbidden)
        {
            return Forbid();
        }

        return status switch
        {
            ResendInvitationStatus.Resent => Accepted(CampaignInvitationHttpMapping.ToResponse(summary!)),
            ResendInvitationStatus.NotFound => NotFound(),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid campaignId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var (access, status) = await campaignInvitationService.RevokeAsync(
            campaignId,
            User.GetUserId(),
            new RevokeInvitationCommand(invitationId),
            cancellationToken);
        if (access == CampaignAccessStatus.Forbidden)
        {
            return Forbid();
        }

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
