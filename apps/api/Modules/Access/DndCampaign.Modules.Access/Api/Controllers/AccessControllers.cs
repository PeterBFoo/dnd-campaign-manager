using System.Security.Claims;
using DndCampaign.Modules.Access.Application.Abstractions.Results;
using DndCampaign.Modules.Access.Application.Bootstrap;
using DndCampaign.Modules.Access.Application.Identity;
using DndCampaign.Modules.Access.Application.Invitations;
using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Application.Users;
using DndCampaign.Modules.Access.Domain.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DndCampaign.Modules.Access.Api;

[ApiController]
[Route("api/v1/identity")]
internal sealed class IdentityController(
    GetBootstrapStatusHandler getBootstrapStatus,
    CompleteBootstrapHandler completeBootstrap,
    LoginHandler login,
    LogoutHandler logout,
    GetCurrentUserHandler getCurrentUser) : ControllerBase
{
    [HttpGet("bootstrap")]
    [ProducesResponseType<BootstrapStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBootstrapStatusAsync(CancellationToken cancellationToken)
    {
        var status = await getBootstrapStatus.HandleAsync(
            new GetBootstrapStatusQuery(),
            cancellationToken);
        return Ok(new BootstrapStatusResponse(status.IsRequired ? "required" : "completed"));
    }

    [HttpPost("bootstrap")]
    [EnableRateLimiting("bootstrap")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteBootstrapAsync(
        BootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var result = await completeBootstrap.HandleAsync(new CompleteBootstrapCommand(
            request.Token,
            request.Email,
            request.DisplayName,
            request.Password), cancellationToken);
        if (result.IsSuccess)
        {
            return Created(
                "/api/v1/identity/me",
                AccessControllerMappings.ToUserResponse(result.Value!));
        }

        if (result.Error!.Type == ApplicationErrorType.Conflict)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "El alta inicial ya está cerrada.",
                Detail = result.Error.Description,
            });
        }

        return AccessControllerMappings.MapError(result.Error);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await login.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return AccessControllerMappings.MapError(result.Error!);
        }

        return Ok(new SessionResponse(
            result.Value!.AccessToken,
            result.Value.ExpiresAt,
            AccessControllerMappings.ToUserResponse(result.Value.User)));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await logout.HandleAsync(new LogoutCommand(User.GetSessionId()), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await getCurrentUser.HandleAsync(
            new GetCurrentUserQuery(AccessControllerMappings.ToActor(User)),
            cancellationToken);
        return Ok(AccessControllerMappings.ToUserResponse(user));
    }
}

[ApiController]
[Route("api/v1/invitations")]
internal sealed class InvitationsController(
    PreviewInvitationHandler previewInvitation,
    AcceptInvitationHandler acceptInvitation) : ControllerBase
{
    [HttpPost("preview")]
    [EnableRateLimiting("invitation-acceptance")]
    [ProducesResponseType<InvitationPreviewResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewAsync(
        InvitationTokenRequest request,
        CancellationToken cancellationToken)
    {
        var preview = await previewInvitation.HandleAsync(
            new PreviewInvitationQuery(request.Token),
            cancellationToken);
        return Ok(preview is null
            ? new InvitationPreviewResponse("invalid", null, null, null, false)
            : new InvitationPreviewResponse(
                preview.State,
                preview.Kind,
                preview.MaskedRecipientEmail,
                preview.ExpiresAt,
                preview.RequiresAuthentication));
    }

    [HttpPost("accept")]
    [EnableRateLimiting("invitation-acceptance")]
    [ProducesResponseType<InvitationAcceptanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public async Task<IActionResult> AcceptAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await acceptInvitation.HandleAsync(new AcceptInvitationCommand(
            request.Token,
            request.DisplayName,
            request.Password,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        if (!result.IsSuccess)
        {
            return AccessControllerMappings.MapError(result.Error!);
        }

        return Ok(new InvitationAcceptanceResponse(
            AccessControllerMappings.ToUserResponse(result.Value!.User),
            result.Value.AccessToken,
            result.Value.ExpiresAt,
            result.Value.Kind));
    }
}

[ApiController]
[Authorize(Policy = "platform-admin")]
[Route("api/v1/platform/invitations")]
internal sealed class PlatformInvitationsController(
    ListInvitationsHandler listInvitations,
    InvitationCommandHandler invitationCommands) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IEnumerable<InvitationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var result = await listInvitations.HandleAsync(new ListInvitationsQuery(
            InvitationKind.Platform,
            CampaignId: null,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.Select(AccessControllerMappings.ToInvitationResponse))
            : AccessControllerMappings.MapError(result.Error!);
    }

    [HttpPost]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IssueAsync(
        IssueInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await invitationCommands.HandleAsync(new IssueInvitationCommand(
            InvitationKind.Platform,
            request.Email,
            request.RecipientUserId,
            CampaignId: null,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess
            ? Accepted(AccessControllerMappings.ToInvitationResponse(result.Value!))
            : AccessControllerMappings.MapError(result.Error!);
    }

    [HttpPost("{invitationId:guid}/resend")]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await invitationCommands.HandleAsync(new ResendInvitationCommand(
            InvitationKind.Platform,
            invitationId,
            CampaignId: null,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess
            ? Accepted(AccessControllerMappings.ToInvitationResponse(result.Value!))
            : AccessControllerMappings.MapError(result.Error!);
    }

    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await invitationCommands.HandleAsync(new RevokeInvitationCommand(
            InvitationKind.Platform,
            invitationId,
            CampaignId: null,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess ? NoContent() : AccessControllerMappings.MapError(result.Error!);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/campaigns/{campaignId:guid}/invitations")]
internal sealed class CampaignInvitationsController(
    ListInvitationsHandler listInvitations,
    InvitationCommandHandler invitationCommands) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IEnumerable<InvitationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var result = await listInvitations.HandleAsync(new ListInvitationsQuery(
            InvitationKind.Campaign,
            campaignId,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value!.Select(AccessControllerMappings.ToInvitationResponse))
            : AccessControllerMappings.MapError(result.Error!);
    }

    [HttpPost]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IssueAsync(
        Guid campaignId,
        IssueInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await invitationCommands.HandleAsync(new IssueInvitationCommand(
            InvitationKind.Campaign,
            request.Email,
            request.RecipientUserId,
            campaignId,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess
            ? Accepted(AccessControllerMappings.ToInvitationResponse(result.Value!))
            : AccessControllerMappings.MapError(result.Error!);
    }

    [HttpPost("{invitationId:guid}/resend")]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendAsync(
        Guid campaignId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await invitationCommands.HandleAsync(new ResendInvitationCommand(
            InvitationKind.Campaign,
            invitationId,
            campaignId,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess
            ? Accepted(AccessControllerMappings.ToInvitationResponse(result.Value!))
            : AccessControllerMappings.MapError(result.Error!);
    }

    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAsync(
        Guid campaignId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await invitationCommands.HandleAsync(new RevokeInvitationCommand(
            InvitationKind.Campaign,
            invitationId,
            campaignId,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess ? NoContent() : AccessControllerMappings.MapError(result.Error!);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/campaigns/{campaignId:guid}/eligible-users")]
internal sealed class EligibleCampaignUsersController(
    SearchEligibleUsersHandler searchEligibleUsers) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("eligible-users")]
    [ProducesResponseType<EligibleUsersPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        Guid campaignId,
        [FromQuery] string? query,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await searchEligibleUsers.HandleAsync(new SearchEligibleUsersQuery(
            campaignId,
            query,
            cursor,
            limit,
            AccessControllerMappings.ToActor(User)), cancellationToken);
        return result.IsSuccess
            ? Ok(new EligibleUsersPageResponse(
                result.Value!.Items.Select(item => new EligibleUserResponse(
                    item.UserId,
                    item.DisplayName,
                    item.MaskedEmail)),
                result.Value.NextCursor))
            : AccessControllerMappings.MapError(result.Error!);
    }
}

internal static class AccessControllerMappings
{
    internal static IActionResult MapError(ApplicationError error) => error.Type switch
    {
        ApplicationErrorType.Validation => new BadRequestObjectResult(new ValidationProblemDetails(
            error.ValidationErrors?.ToDictionary(entry => entry.Key, entry => entry.Value)
                ?? new Dictionary<string, string[]>())
        {
            Status = StatusCodes.Status400BadRequest,
        }),
        ApplicationErrorType.Unauthorized => new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "No se han podido validar las credenciales.",
        }) { StatusCode = StatusCodes.Status401Unauthorized },
        ApplicationErrorType.Forbidden => new ForbidResult(),
        ApplicationErrorType.NotFound => new NotFoundResult(),
        ApplicationErrorType.Gone => new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status410Gone,
            Title = "La invitación no está disponible.",
            Detail = "Puede haber caducado, haber sido revocada o haberse utilizado anteriormente.",
        }) { StatusCode = StatusCodes.Status410Gone },
        ApplicationErrorType.RateLimited => new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "El reenvío está limitado temporalmente.",
            Detail = $"Podrás volver a intentarlo a partir de {error.RetryAt:O}.",
            Extensions = { ["retryAt"] = error.RetryAt },
        }) { StatusCode = StatusCodes.Status429TooManyRequests },
        ApplicationErrorType.Unavailable => new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "El servicio de correo no está disponible temporalmente.",
            Detail = error.Description,
        }) { StatusCode = StatusCodes.Status503ServiceUnavailable },
        _ => new ConflictObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = error.Code == "invitation.pending_exists"
                ? "Ya existe una invitación pendiente."
                : "La invitación no puede modificarse.",
            Detail = error.Description,
        }),
    };

    internal static AccessActor ToActor(ClaimsPrincipal principal) => new(
        principal.Identity?.IsAuthenticated == true ? principal.GetUserId() : null,
        principal.Identity?.IsAuthenticated == true ? principal.GetSessionId() : null,
        principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
        principal.Identity?.Name ?? string.Empty,
        principal.HasClaim("platform_admin", "true"));

    internal static UserResponse ToUserResponse(UserDto user) =>
        new(user.Id, user.Email, user.DisplayName, user.IsPlatformAdmin);

    internal static InvitationResponse ToInvitationResponse(InvitationListItemDto invitation) => new(
        invitation.Id,
        invitation.Kind,
        invitation.RecipientEmail,
        invitation.CampaignId,
        invitation.Status,
        invitation.DeliveryStatus,
        invitation.IssuedAt,
        invitation.ExpiresAt,
        invitation.LastSentAt);
}

internal sealed record BootstrapRequest(string? Token, string? Email, string? DisplayName, string? Password);

internal sealed record BootstrapStatusResponse(string State);

internal sealed record LoginRequest(string? Email, string? Password);

internal sealed record InvitationTokenRequest(string? Token);

internal sealed record AcceptInvitationRequest(string? Token, string? DisplayName, string? Password);

internal sealed record IssueInvitationRequest(string? Email, Guid? RecipientUserId);

internal sealed record EligibleUserResponse(Guid UserId, string DisplayName, string MaskedEmail);

internal sealed record EligibleUsersPageResponse(
    IEnumerable<EligibleUserResponse> Items,
    string? NextCursor);

internal sealed record UserResponse(Guid Id, string Email, string DisplayName, bool IsPlatformAdmin);

internal sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

internal sealed record InvitationPreviewResponse(
    string State,
    string? Kind,
    string? RecipientEmail,
    DateTimeOffset? ExpiresAt,
    bool RequiresAuthentication);

internal sealed record InvitationAcceptanceResponse(
    UserResponse User,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    string Kind);

internal sealed record InvitationResponse(
    Guid Id,
    string Kind,
    string RecipientEmail,
    Guid? CampaignId,
    string Status,
    string DeliveryStatus,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastSentAt);
