using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using Microsoft.AspNetCore.Identity;

namespace DndCampaign.Api.Application.Invitations;

public sealed class InvitationAcceptanceService(
    IInvitationStore invitations,
    IIdentityStore identity,
    ITransactionalBoundary transactions,
    IPasswordHasher<UserAccount> passwordHasher,
    TimeProvider timeProvider) : IInvitationAcceptanceService
{
    public async Task<InvitationPreviewOutcome> PreviewAsync(
        PreviewInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var invitation = await InvitationInternalOperations.FindInvitationByTokenAsync(
            invitations,
            command.Token,
            cancellationToken);
        if (invitation is null)
        {
            return new InvitationPreviewOutcome("invalid", null, null, null, false);
        }

        var now = timeProvider.GetUtcNow();
        if (invitation.Expire(now))
        {
            await invitations.SaveAsync(invitation, cancellationToken);
        }

        var requiresAuthentication = invitation.Status == InvitationStatus.Pending
            && await identity.FindByEmailAsync(invitation.RecipientEmail, cancellationToken) is not null;

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
        try
        {
            var outcome = await transactions.ExecuteSerializableAsync(async ct =>
            {
                var result = await AcceptInsideTransactionAsync(command, actor, ct);
                if (result.Status != AcceptInvitationStatus.Accepted)
                {
                    throw new AcceptTransactionAbortedException(result);
                }

                return result;
            }, cancellationToken);

            IdentityTelemetry.InvitationsAccepted.Add(
                1,
                new KeyValuePair<string, object?>("invitation.kind", outcome.Outcome!.Kind));
            return outcome;
        }
        catch (AcceptTransactionAbortedException aborted)
        {
            return aborted.Result;
        }
    }

    private async Task<AcceptInvitationResult> AcceptInsideTransactionAsync(
        AcceptInvitationCommand command,
        AuthenticatedActor actor,
        CancellationToken cancellationToken)
    {
        var invitation = await InvitationInternalOperations.FindInvitationByTokenAsync(
            invitations,
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
        if (invitation.Expire(now))
        {
            await invitations.SaveAsync(invitation, cancellationToken);
        }

        if (!invitation.IsPending(now))
        {
            return AcceptInvitationResult.Failure(AcceptInvitationStatus.AlreadyAccepted);
        }

        var user = await identity.FindByEmailAsync(invitation.RecipientEmail, cancellationToken);
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
            issuedSession = creationResult.Session!;
            await identity.AddUserAsync(user, cancellationToken);
            await identity.AddSessionAsync(issuedSession.Session, cancellationToken);
        }

        if (invitation.Kind == InvitationKind.Campaign && invitation.CampaignId.HasValue)
        {
            var campaignId = invitation.CampaignId.Value;
            if (!await identity.IsCampaignMemberAsync(campaignId, user.Id, cancellationToken))
            {
                await identity.AddMembershipAsync(
                    CampaignMembership.JoinAsPlayer(campaignId, user.Id, now),
                    cancellationToken);
            }
        }

        var acceptance = invitation.Accept(command.Token!, user.Id, now);
        if (acceptance != InvitationAcceptanceResult.Accepted)
        {
            return AcceptInvitationResult.Failure(AcceptInvitationStatus.NotFound);
        }

        await invitations.SaveAsync(invitation, cancellationToken);

        return AcceptInvitationResult.Success(
            new InvitationAcceptanceOutcome(
                IdentityService.ToUserProfile(user),
                issuedSession?.Token,
                issuedSession?.Session.ExpiresAt,
                invitation.Kind.ToString().ToLowerInvariant()));
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
        var issuedSession = UserSession.Issue(user.Id, now);
        return new UserCreationResult(user, issuedSession, []);
    }

    private sealed record UserCreationResult(
        UserAccount? User,
        IssuedUserSession? Session,
        IReadOnlyCollection<IdentityAccountValidationErrors> Errors);

    private sealed class AcceptTransactionAbortedException(AcceptInvitationResult result) : Exception
    {
        public AcceptInvitationResult Result { get; } = result;
    }
}
