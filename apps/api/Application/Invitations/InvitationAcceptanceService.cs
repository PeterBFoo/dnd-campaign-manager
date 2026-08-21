using System.Data;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Application.Invitations;

/// <summary>
/// Invitation preview and acceptance. Temporary debt: uses <see cref="CampaignDbContext"/> directly.
/// </summary>
public sealed class InvitationAcceptanceService(
    CampaignDbContext database,
    IPasswordHasher<UserAccount> passwordHasher,
    TimeProvider timeProvider) : IInvitationAcceptanceService
{
    public async Task<InvitationPreviewOutcome> PreviewAsync(
        PreviewInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var invitation = await InvitationInternalOperations.FindInvitationByTokenAsync(
            database,
            command.Token,
            cancellationToken);
        if (invitation is null)
        {
            return new InvitationPreviewOutcome("invalid", null, null, null, false);
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

        var state = invitation.Status switch
        {
            InvitationStatus.Pending => "valid",
            InvitationStatus.Expired => "expired",
            InvitationStatus.Accepted => "accepted",
            InvitationStatus.Revoked => "revoked",
            _ => "invalid",
        };

        return new InvitationPreviewOutcome(
            state,
            invitation.Kind.ToString().ToLowerInvariant(),
            InvitationInternalOperations.MaskEmail(invitation.RecipientEmail),
            invitation.ExpiresAt,
            requiresAuthentication);
    }

    public async Task<AcceptInvitationResult> AcceptAsync(
        AcceptInvitationCommand command,
        AuthenticatedActor actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var invitation = await InvitationInternalOperations.FindInvitationByTokenAsync(
            database,
            command.Token,
            cancellationToken);

        if (invitation is null)
        {
            return AcceptInvitationResult.Failure(AcceptInvitationStatus.NotFound);
        }

        if (invitation.Status == InvitationStatus.Expired)
        {
            return AcceptInvitationResult.Failure(AcceptInvitationStatus.Expired);
        }

        var now = timeProvider.GetUtcNow();
        invitation.MarkExpired(now);

        if (!invitation.IsPending(now))
        {
            await database.SaveChangesAsync(cancellationToken);
            return AcceptInvitationResult.Failure(AcceptInvitationStatus.AlreadyAccepted);
        }

        var user = await database.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == invitation.RecipientEmail,
            cancellationToken);

        IssuedUserSession? issuedSession = null;

        if (user is not null)
        {
            var authorizationStatus = ValidateExistingUser(actor, user);
            if (authorizationStatus.HasValue)
            {
                return AcceptInvitationResult.Failure(authorizationStatus.Value);
            }
        }
        else
        {
            var creationResult = CreateUser(
                invitation.RecipientEmail,
                command.DisplayName,
                command.Password,
                now);

            if (creationResult.Errors.Count > 0)
            {
                return AcceptInvitationResult.InvalidCredentials(creationResult.Errors);
            }

            user = creationResult.User!;
            issuedSession = creationResult.Session;
        }

        if (invitation.Kind == InvitationKind.Campaign && invitation.CampaignId.HasValue)
        {
            var campaignId = invitation.CampaignId.Value;
            var isMember = await database.CampaignMemberships.AnyAsync(
                membership =>
                    membership.CampaignId == campaignId
                    && membership.UserId == user.Id,
                cancellationToken);

            if (!isMember)
            {
                database.CampaignMemberships.Add(
                    CampaignMembership.JoinAsPlayer(campaignId, user.Id, now));
            }
        }

        invitation.MarkAccepted(user.Id, now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var invitationKind = invitation.Kind.ToString().ToLowerInvariant();
        IdentityTelemetry.InvitationsAccepted.Add(
            1,
            new KeyValuePair<string, object?>("invitation.kind", invitationKind));

        return AcceptInvitationResult.Success(
            new InvitationAcceptanceOutcome(
                IdentityService.ToUserProfile(user),
                issuedSession?.Token,
                issuedSession?.Session.ExpiresAt,
                invitationKind));
    }

    private static AcceptInvitationStatus? ValidateExistingUser(AuthenticatedActor actor, UserAccount user)
    {
        if (!actor.IsAuthenticated)
        {
            return AcceptInvitationStatus.Unauthorized;
        }

        if (actor.UserId != user.Id)
        {
            return AcceptInvitationStatus.Forbidden;
        }

        return null;
    }

    private UserCreationResult CreateUser(
        string email,
        string? displayName,
        string? password,
        DateTimeOffset now)
    {
        var validationErrors = IdentityService.ValidateNewAccount(email, displayName, password).ToArray();
        if (validationErrors.Length > 0)
        {
            return new UserCreationResult(null, null, validationErrors);
        }

        var user = UserAccount.Create(email, displayName!, isPlatformAdmin: false, now);
        user.SetPasswordHash(passwordHasher.HashPassword(user, password!));
        database.Users.Add(user);

        var issuedSession = UserSession.Issue(user.Id, now);
        database.UserSessions.Add(issuedSession.Session);

        return new UserCreationResult(user, issuedSession, []);
    }

    private sealed record UserCreationResult(
        UserAccount? User,
        IssuedUserSession? Session,
        IReadOnlyCollection<IdentityAccountValidationErrors> Errors);
}
