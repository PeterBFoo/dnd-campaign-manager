using System.Data;
using System.Security.Claims;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Application.Invitations;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using DndCampaign.Api.Infrastructure.Observability;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Api;

public static class IdentityInvitationEndpoints
{
    public static IEndpointRouteBuilder MapIdentityInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var identity = endpoints.MapGroup("/api/v1/identity").WithTags("Identity");
        identity.MapPost("/login", LoginAsync).RequireRateLimiting("login");
        identity.MapPost("/logout", LogoutAsync).RequireAuthorization();
        identity.MapGet("/me", Me).RequireAuthorization();

        var acceptance = endpoints.MapGroup("/api/v1/invitations").WithTags("Invitations");
        acceptance.MapPost("/preview", PreviewInvitationAsync).RequireRateLimiting("invitation-acceptance");

        var platform = endpoints.MapGroup("/api/v1/platform/invitations")
            .WithTags("Platform invitations")
            .RequireAuthorization("platform-admin");
        platform.MapGet("/", ListPlatformInvitationsAsync);
        platform.MapPost("/", IssuePlatformInvitationAsync);
        platform.MapPost("/{invitationId:guid}/resend", ResendPlatformInvitationAsync);
        platform.MapDelete("/{invitationId:guid}", RevokePlatformInvitationAsync);

        var campaign = endpoints.MapGroup("/api/v1/campaigns/{campaignId:guid}/invitations")
            .WithTags("Campaign invitations")
            .RequireAuthorization();
        campaign.MapGet("/", ListCampaignInvitationsAsync);
        campaign.MapPost("/", IssueCampaignInvitationAsync);
        campaign.MapPost("/{invitationId:guid}/resend", ResendCampaignInvitationAsync);
        campaign.MapDelete("/{invitationId:guid}", RevokeCampaignInvitationAsync);
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        CampaignDbContext database,
        IPasswordHasher<UserAccount> passwordHasher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        IdentityTelemetry.LoginAttempts.Add(1);
        string email;
        try
        {
            email = UserAccount.NormalizeEmail(request.Email ?? string.Empty);
        }
        catch (ArgumentException)
        {
            IdentityTelemetry.LoginFailures.Add(1);
            return InvalidCredentials();
        }

        var user = await database.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);
        if (user is null || string.IsNullOrEmpty(request.Password))
        {
            IdentityTelemetry.LoginFailures.Add(1);
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            IdentityTelemetry.LoginFailures.Add(1);
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        }

        var issued = UserSession.Issue(user.Id, timeProvider.GetUtcNow());
        database.UserSessions.Add(issued.Session);
        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(new SessionResponse(
            issued.Token,
            issued.Session.ExpiresAt,
            ToUserResponse(user)));
    }

    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal principal,
        CampaignDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var sessionId = principal.GetSessionId();
        var session = await database.UserSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == sessionId,
            cancellationToken);
        if (session is not null)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await database.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static IResult Me(ClaimsPrincipal principal) => Results.Ok(new UserResponse(
        principal.GetUserId(),
        principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
        principal.Identity?.Name ?? string.Empty,
        principal.HasClaim("platform_admin", "true")));

    private static async Task<IResult> PreviewInvitationAsync(
        InvitationTokenRequest request,
        CampaignDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(request.Token, database, cancellationToken);
        if (invitation is null)
        {
            return Results.Ok(new InvitationPreviewResponse("invalid", null, null, null, false));
        }

        var now = timeProvider.GetUtcNow();
        invitation.MarkExpired(now);
        if (database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        var requiresAuthentication = invitation.Status == InvitationStatus.Pending
            && await database.Users.AnyAsync(
                user => user.Email == invitation.RecipientEmail,
                cancellationToken);
        return Results.Ok(new InvitationPreviewResponse(
            invitation.Status switch
            {
                InvitationStatus.Pending => "valid",
                InvitationStatus.Expired => "expired",
                InvitationStatus.Accepted => "accepted",
                InvitationStatus.Revoked => "revoked",
                _ => "invalid",
            },
            invitation.Kind.ToString().ToLowerInvariant(),
            MaskEmail(invitation.RecipientEmail),
            invitation.ExpiresAt,
            requiresAuthentication));
    }

    private static async Task<IResult> ListPlatformInvitationsAsync(
        CampaignDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        Results.Ok(await ListInvitationsAsync(
            database,
            InvitationKind.Platform,
            campaignId: null,
            timeProvider.GetUtcNow(),
            cancellationToken));

    private static async Task<IResult> IssuePlatformInvitationAsync(
        IssueInvitationRequest request,
        ClaimsPrincipal principal,
        InvitationService service,
        CancellationToken cancellationToken) =>
        await IssueInvitationResultAsync(
            () => service.IssuePlatformAsync(request.Email ?? string.Empty, principal.GetUserId(), cancellationToken));

    private static async Task<IResult> ResendPlatformInvitationAsync(
        Guid invitationId,
        CampaignDbContext database,
        InvitationService service,
        CancellationToken cancellationToken)
    {
        var invitation = await database.Invitations.SingleOrDefaultAsync(candidate =>
            candidate.Id == invitationId && candidate.Kind == InvitationKind.Platform,
            cancellationToken);
        return invitation is null
            ? Results.NotFound()
            : await ResendInvitationResultAsync(() => service.ResendAsync(invitation, cancellationToken));
    }

    private static async Task<IResult> RevokePlatformInvitationAsync(
        Guid invitationId,
        CampaignDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        await RevokeInvitationAsync(
            database,
            invitationId,
            InvitationKind.Platform,
            campaignId: null,
            timeProvider.GetUtcNow(),
            cancellationToken);

    private static async Task<IResult> ListCampaignInvitationsAsync(
        Guid campaignId,
        ClaimsPrincipal principal,
        CampaignDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await IsCampaignDmAsync(database, campaignId, principal.GetUserId(), cancellationToken))
        {
            return Results.Forbid();
        }

        return Results.Ok(await ListInvitationsAsync(
            database,
            InvitationKind.Campaign,
            campaignId,
            timeProvider.GetUtcNow(),
            cancellationToken));
    }

    private static async Task<IResult> IssueCampaignInvitationAsync(
        Guid campaignId,
        IssueInvitationRequest request,
        ClaimsPrincipal principal,
        CampaignDbContext database,
        InvitationService service,
        CancellationToken cancellationToken)
    {
        if (!await IsCampaignDmAsync(database, campaignId, principal.GetUserId(), cancellationToken))
        {
            return Results.Forbid();
        }

        return await IssueInvitationResultAsync(() => service.IssueCampaignAsync(
            request.Email ?? string.Empty,
            campaignId,
            principal.GetUserId(),
            cancellationToken));
    }

    private static async Task<IResult> ResendCampaignInvitationAsync(
        Guid campaignId,
        Guid invitationId,
        ClaimsPrincipal principal,
        CampaignDbContext database,
        InvitationService service,
        CancellationToken cancellationToken)
    {
        if (!await IsCampaignDmAsync(database, campaignId, principal.GetUserId(), cancellationToken))
        {
            return Results.Forbid();
        }

        var invitation = await database.Invitations.SingleOrDefaultAsync(candidate =>
            candidate.Id == invitationId
            && candidate.Kind == InvitationKind.Campaign
            && candidate.CampaignId == campaignId,
            cancellationToken);
        return invitation is null
            ? Results.NotFound()
            : await ResendInvitationResultAsync(() => service.ResendAsync(invitation, cancellationToken));
    }

    private static async Task<IResult> RevokeCampaignInvitationAsync(
        Guid campaignId,
        Guid invitationId,
        ClaimsPrincipal principal,
        CampaignDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await IsCampaignDmAsync(database, campaignId, principal.GetUserId(), cancellationToken))
        {
            return Results.Forbid();
        }

        return await RevokeInvitationAsync(
            database,
            invitationId,
            InvitationKind.Campaign,
            campaignId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static async Task<IResult> IssueInvitationResultAsync(
        Func<Task<InvitationRecord>> issue)
    {
        try
        {
            var invitation = await issue();
            return Results.Accepted(value: ToInvitationResponse(invitation, "pending"));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = [exception.Message],
            });
        }
        catch (InvitationConflictException)
        {
            return Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Ya existe una invitación pendiente.",
                Detail = "Revócala o utiliza la acción de reenvío cuando esté disponible.",
            });
        }
    }

    private static async Task<IResult> ResendInvitationResultAsync(Func<Task<InvitationRecord>> resend)
    {
        try
        {
            var invitation = await resend();
            return Results.Accepted(value: ToInvitationResponse(invitation, "pending"));
        }
        catch (InvitationStateException exception)
        {
            return Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "La invitación no puede reenviarse.",
                Detail = exception.Message,
            });
        }
        catch (InvitationRateLimitException exception)
        {
            return Results.Json(
                new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "El reenvío está limitado temporalmente.",
                    Detail = $"Podrás volver a intentarlo a partir de {exception.RetryAt:O}.",
                    Extensions = { ["retryAt"] = exception.RetryAt },
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }
    }

    private static async Task<IResult> RevokeInvitationAsync(
        CampaignDbContext database,
        Guid invitationId,
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await database.Invitations.SingleOrDefaultAsync(candidate =>
            candidate.Id == invitationId
            && candidate.Kind == kind
            && candidate.CampaignId == campaignId,
            cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        if (!invitation.Revoke(now))
        {
            return Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "La invitación ya no está pendiente.",
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        IdentityTelemetry.InvitationsRevoked.Add(
            1,
            new KeyValuePair<string, object?>("invitation.kind", kind.ToString().ToLowerInvariant()));
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<InvitationResponse>> ListInvitationsAsync(
        CampaignDbContext database,
        InvitationKind kind,
        Guid? campaignId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitations = await database.Invitations
            .Where(invitation => invitation.Kind == kind && invitation.CampaignId == campaignId)
            .OrderByDescending(invitation => invitation.IssuedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        foreach (var invitation in invitations)
        {
            invitation.MarkExpired(now);
        }

        if (database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        var invitationIds = invitations.Select(invitation => invitation.Id).ToArray();
        var outbox = await database.InvitationOutbox
            .Where(message => invitationIds.Contains(message.InvitationId))
            .ToListAsync(cancellationToken);
        return invitations.Select(invitation =>
        {
            var delivery = outbox
                .Where(message => message.InvitationId == invitation.Id)
                .OrderByDescending(message => message.CreatedAt)
                .FirstOrDefault();
            var deliveryStatus = delivery switch
            {
                { ProcessedAt: not null, ProviderMessageId: not "discarded" } => "sent",
                { ProcessedAt: not null, ProviderMessageId: "discarded" } => "discarded",
                { Attempts: >= 5 } => "failed",
                _ => "pending",
            };
            return ToInvitationResponse(invitation, deliveryStatus);
        }).ToArray();
    }

    private static async Task<InvitationRecord?> FindInvitationAsync(
        string? token,
        CampaignDbContext database,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 32 or > 128)
        {
            return null;
        }

        var tokenHash = Convert.ToHexString(Invitation.HashToken(token));
        return await database.Invitations.SingleOrDefaultAsync(
            invitation => invitation.TokenHash == tokenHash,
            cancellationToken);
    }

    private static async Task<bool> IsCampaignDmAsync(
        CampaignDbContext database,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await database.CampaignMemberships.AnyAsync(membership =>
            membership.CampaignId == campaignId
            && membership.UserId == userId
            && membership.Role == CampaignRole.Dm,
            cancellationToken);

    private static IResult InvalidCredentials() => Results.Json(
        new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "No se han podido validar las credenciales.",
        },
        statusCode: StatusCodes.Status401Unauthorized);

    private static UserResponse ToUserResponse(UserAccount user) =>
        new(user.Id, user.Email, user.DisplayName, user.IsPlatformAdmin);

    private static InvitationResponse ToInvitationResponse(
        InvitationRecord invitation,
        string deliveryStatus) =>
        new(
            invitation.Id,
            invitation.Kind.ToString().ToLowerInvariant(),
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.Status.ToString().ToLowerInvariant(),
            deliveryStatus,
            invitation.IssuedAt,
            invitation.ExpiresAt,
            invitation.LastSentAt);

    private static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return "***";
        }

        var local = email[..separator];
        var visible = local[..Math.Min(2, local.Length)];
        return $"{visible}***{email[separator..]}";
    }
}

public sealed record BootstrapRequest(string? Token, string? Email, string? DisplayName, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record InvitationTokenRequest(string? Token);

public sealed record AcceptInvitationRequest(string? Token, string? DisplayName, string? Password);

public sealed record IssueInvitationRequest(string? Email);

public sealed record UserResponse(Guid Id, string Email, string DisplayName, bool IsPlatformAdmin);

public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

public sealed record InvitationPreviewResponse(
    string State,
    string? Kind,
    string? RecipientEmail,
    DateTimeOffset? ExpiresAt,
    bool RequiresAuthentication);

public sealed record InvitationAcceptanceResponse(
    UserResponse User,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    string Kind);

public sealed record InvitationResponse(
    Guid Id,
    string Kind,
    string RecipientEmail,
    Guid? CampaignId,
    string Status,
    string DeliveryStatus,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastSentAt);
