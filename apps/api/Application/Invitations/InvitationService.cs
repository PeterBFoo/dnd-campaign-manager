using System.Data;
using System.Security.Claims;
using DndCampaign.Api.Api;
using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Domain.Invitations;
using DndCampaign.Api.Infrastructure.Persistence;
using DndCampaign.Api.Infrastructure.Observability;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DndCampaign.Api.Application.Invitations;

public sealed class InvitationService(
    CampaignDbContext database,
    InvitationTokenProtector protector,
    IPasswordHasher<UserAccount> passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<InvitationRecord> IssuePlatformAsync(
        string recipientEmail,
        Guid issuedByUserId,
        CancellationToken cancellationToken) =>
        await IssueAsync(
            InvitationKind.Platform,
            recipientEmail,
            campaignId: null,
            issuedByUserId,
            cancellationToken);

    public async Task<InvitationRecord> IssueCampaignAsync(
        string recipientEmail,
        Guid campaignId,
        Guid issuedByUserId,
        CancellationToken cancellationToken) =>
        await IssueAsync(
            InvitationKind.Campaign,
            recipientEmail,
            campaignId,
            issuedByUserId,
            cancellationToken);

    public async Task<InvitationRecord> ResendAsync(
        InvitationRecord invitation,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!invitation.IsPending(now))
        {
            throw new InvitationStateException("Solo se puede reenviar una invitación pendiente.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var recentIssues = await database.Invitations
            .Where(candidate =>
                candidate.Kind == invitation.Kind
                && candidate.CampaignId == invitation.CampaignId
                && candidate.RecipientEmail == invitation.RecipientEmail
                && candidate.IssuedAt >= now.AddHours(-24))
            .Select(candidate => candidate.IssuedAt)
            .ToListAsync(cancellationToken);
        var mostRecentIssue = recentIssues.Count > 0 ? recentIssues.Max() : invitation.IssuedAt;
        var nextAllowedAt = mostRecentIssue.AddMinutes(15);
        if (now < nextAllowedAt)
        {
            throw new InvitationRateLimitException(nextAllowedAt);
        }

        if (recentIssues.Count >= 5)
        {
            throw new InvitationRateLimitException(recentIssues.Min().AddHours(24));
        }

        invitation.Revoke(now);
        var replacement = CreateInvitation(
            invitation.Kind,
            invitation.RecipientEmail,
            invitation.CampaignId,
            invitation.IssuedByUserId,
            now);
        database.Invitations.Add(replacement.Record);
        database.InvitationOutbox.Add(replacement.Outbox);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        IdentityTelemetry.InvitationsIssued.Add(
            1,
            new KeyValuePair<string, object?>("invitation.kind", replacement.Record.Kind.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("invitation.operation", "resend"));
        return replacement.Record;
    }

    private async Task<InvitationRecord> IssueAsync(
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        Guid issuedByUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = UserAccount.NormalizeEmail(recipientEmail);
        var now = timeProvider.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var alreadyPending = await database.Invitations.AnyAsync(invitation =>
            invitation.Kind == kind
            && invitation.CampaignId == campaignId
            && invitation.RecipientEmail == normalizedEmail
            && invitation.Status == InvitationStatus.Pending
            && invitation.ExpiresAt > now,
            cancellationToken);
        if (alreadyPending)
        {
            throw new InvitationConflictException();
        }

        var created = CreateInvitation(kind, normalizedEmail, campaignId, issuedByUserId, now);
        database.Invitations.Add(created.Record);
        database.InvitationOutbox.Add(created.Outbox);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        IdentityTelemetry.InvitationsIssued.Add(
            1,
            new KeyValuePair<string, object?>("invitation.kind", kind.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("invitation.operation", "initial"));
        return created.Record;
    }

    private (InvitationRecord Record, InvitationOutboxMessage Outbox) CreateInvitation(
        InvitationKind kind,
        string recipientEmail,
        Guid? campaignId,
        Guid issuedByUserId,
        DateTimeOffset now)
    {
        var issued = kind switch
        {
            InvitationKind.Platform => Invitation.IssuePlatform(recipientEmail, now),
            InvitationKind.Campaign when campaignId.HasValue =>
                Invitation.IssueCampaign(recipientEmail, campaignId.Value, now),
            _ => throw new InvalidOperationException("The invitation kind is not supported."),
        };
        var record = InvitationRecord.FromIssued(issued, issuedByUserId);
        var outbox = InvitationOutboxMessage.Create(
            record.Id,
            protector.Protect(issued.Token),
            now);
        return (record, outbox);
    }
    
    public async Task<AcceptInvitationResult> AcceptInvitationAsync(
    AcceptInvitationRequest request,
    ClaimsPrincipal principal,
    CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
    
        var invitation = await FindInvitationAsync(
            request.Token,
            cancellationToken);
    
        if (invitation is null)
        {
            return AcceptInvitationResult.Failure(
                AcceptInvitationStatus.NotFound);
        }
    
        if (invitation.Status == InvitationStatus.Expired)
        {
            return AcceptInvitationResult.Failure(
                AcceptInvitationStatus.Expired);
        }
    
        var now = timeProvider.GetUtcNow();
    
        invitation.MarkExpired(now);
    
        if (!invitation.IsPending(now))
        {
            await database.SaveChangesAsync(cancellationToken);
    
            return AcceptInvitationResult.Failure(
                AcceptInvitationStatus.AlreadyAccepted);
        }
    
        var user = await FindUserByEmailAsync(
            invitation.RecipientEmail,
            cancellationToken);
    
        IssuedUserSession? issuedSession = null;
    
        if (user is not null)
        {
            var authorizationStatus = ValidateExistingUser(
                principal,
                user);
    
            if (authorizationStatus.HasValue)
            {
                return AcceptInvitationResult.Failure(
                    authorizationStatus.Value);
            }
        }
        else
        {
            var creationResult = CreateUser(
                invitation.RecipientEmail,
                request.DisplayName,
                request.Password,
                now);
    
            if (creationResult.Errors.Count > 0)
            {
                return AcceptInvitationResult.InvalidCredentials(
                    creationResult.Errors);
            }
    
            user = creationResult.User!;
            issuedSession = creationResult.Session;
        }
    
        if (invitation.Kind == InvitationKind.Campaign && invitation.CampaignId.HasValue)
        {
            var campaignId = invitation.CampaignId.Value;
    
            var isMember = await database.CampaignMemberships.AnyAsync(
                membership =>
                    membership.CampaignId == campaignId &&
                    membership.UserId == user.Id,
                cancellationToken);
    
            if (!isMember)
            {
                database.CampaignMemberships.Add(
                    CampaignMembership.JoinAsPlayer(
                        campaignId,
                        user.Id,
                        now));
            }
        }

        invitation.MarkAccepted(
            user.Id,
            now);
    
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    
        var invitationKind = invitation.Kind
            .ToString()
            .ToLowerInvariant();
    
        TrackInvitationAccepted(invitationKind);
    
        return AcceptInvitationResult.Success(
            new InvitationAcceptanceResponse(
                IdentityService.ToUserResponse(user),
                issuedSession?.Token,
                issuedSession?.Session.ExpiresAt,
                invitationKind)); 
    }
    
    private async Task<InvitationRecord?> FindInvitationAsync(
        string? token,
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
    
    private Task<UserAccount?> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        return database.Users.SingleOrDefaultAsync(
            user => user.Email == email,
            cancellationToken);
    }

    private static AcceptInvitationStatus? ValidateExistingUser(
        ClaimsPrincipal principal,
        UserAccount user)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return AcceptInvitationStatus.Unauthorized;
        }

        if (principal.GetUserId() != user.Id)
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
        var validationErrors = IdentityService.ValidateNewAccount(
                email,
                displayName,
                password)
            .ToArray();

        if (validationErrors.Length > 0)
        {
            return new UserCreationResult(
                null,
                null,
                validationErrors);
        }

        var user = UserAccount.Create(
            email,
            displayName!,
            isPlatformAdmin: false,
            now);

        var passwordHash = passwordHasher.HashPassword(
            user,
            password!);

        user.SetPasswordHash(passwordHash);

        database.Users.Add(user);

        var issuedSession = UserSession.Issue(
            user.Id,
            now);

        database.UserSessions.Add(
            issuedSession.Session);

        return new UserCreationResult(
            user,
            issuedSession,
            []);
    }

    private static void TrackInvitationAccepted(
        string invitationKind)
    {
        IdentityTelemetry.InvitationsAccepted.Add(
            1,
            new KeyValuePair<string, object?>(
                "invitation.kind",
                invitationKind));
    }

    private sealed record UserCreationResult(
        UserAccount? User,
        IssuedUserSession? Session,
        IReadOnlyCollection<IdentityAccountValidationErrors> Errors);
}

public sealed class InvitationConflictException : Exception;

public sealed class InvitationStateException(string message) : Exception(message);

public sealed class InvitationRateLimitException(DateTimeOffset retryAt) : Exception
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}
