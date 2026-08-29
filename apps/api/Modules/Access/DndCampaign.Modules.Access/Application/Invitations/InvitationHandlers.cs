using DndCampaign.Modules.Access.Application.Abstractions.Messaging;
using DndCampaign.Modules.Access.Application.Abstractions.Results;
using DndCampaign.Modules.Access.Application.Identity;
using DndCampaign.Modules.Access.Application.Ports.Observability;
using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Application.Ports.Events;
using DndCampaign.Modules.Access.Application.Ports.Security;
using DndCampaign.Modules.Access.Application.Security;
using DndCampaign.Modules.Access.Application.Users;
using DndCampaign.Modules.Access.Contracts.CampaignAccess;
using DndCampaign.Modules.Access.Domain.Accounts;
using DndCampaign.Modules.Access.Domain.CampaignAccess;
using DndCampaign.Modules.Access.Domain.Invitations;
using DndCampaign.Modules.Access.Domain.Sessions;

namespace DndCampaign.Modules.Access.Application.Invitations;

internal sealed record PreviewInvitationQuery(string? Token);

internal sealed class PreviewInvitationHandler(
    IInvitationReadStore invitations,
    TimeProvider timeProvider)
    : IQueryHandler<PreviewInvitationQuery, InvitationPreviewDto?>
{
    public Task<InvitationPreviewDto?> HandleAsync(
        PreviewInvitationQuery query,
        CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(query.Token) || query.Token.Length is < 32 or > 128
            ? Task.FromResult<InvitationPreviewDto?>(null)
            : invitations.PreviewAsync(
                Invitation.HashToken(query.Token),
                timeProvider.GetUtcNow(),
                cancellationToken);
}

internal sealed record ListInvitationsQuery(
    InvitationKind Kind,
    Guid? CampaignId,
    AccessActor Actor);

internal sealed class ListInvitationsHandler(
    IInvitationReadStore invitations,
    ICampaignInvitationContext campaigns,
    TimeProvider timeProvider)
    : IQueryHandler<ListInvitationsQuery, Result<IReadOnlyList<InvitationListItemDto>>>
{
    public async Task<Result<IReadOnlyList<InvitationListItemDto>>> HandleAsync(
        ListInvitationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Kind == InvitationKind.Platform && !query.Actor.IsPlatformAdmin)
        {
            return Forbidden();
        }

        if (query.Kind == InvitationKind.Campaign
            && (!query.Actor.UserId.HasValue
                || !query.CampaignId.HasValue
                || !(await campaigns.GetAccessAsync(
                    query.CampaignId.Value,
                    query.Actor.UserId.Value,
                    cancellationToken)).IsDm))
        {
            return Forbidden();
        }

        return Result<IReadOnlyList<InvitationListItemDto>>.Success(await invitations.ListAsync(
            query.Kind,
            query.CampaignId,
            timeProvider.GetUtcNow(),
            cancellationToken));
    }

    private static Result<IReadOnlyList<InvitationListItemDto>> Forbidden() =>
        Result<IReadOnlyList<InvitationListItemDto>>.Failure(new ApplicationError(
            "access.forbidden",
            ApplicationErrorType.Forbidden,
            "No tienes acceso a estas invitaciones."));
}

internal sealed record IssueInvitationCommand(
    InvitationKind Kind,
    string? RecipientEmail,
    Guid? RecipientUserId,
    Guid? CampaignId,
    AccessActor Actor);

internal sealed record ResendInvitationCommand(
    InvitationKind Kind,
    Guid InvitationId,
    Guid? CampaignId,
    AccessActor Actor);

internal sealed record RevokeInvitationCommand(
    InvitationKind Kind,
    Guid InvitationId,
    Guid? CampaignId,
    AccessActor Actor);

internal sealed class InvitationCommandHandler(
    IInvitationRepository invitations,
    IInvitationOutboxRepository outbox,
    ICampaignAccessRepository campaignAccess,
    IUserAccountRepository users,
    ICampaignInvitationContext campaigns,
    IInvitationTokenProtector tokenProtector,
    IInvitationEventPublisher eventPublisher,
    IAccessUnitOfWork unitOfWork,
    IAccessMetrics metrics,
    TimeProvider timeProvider)
    : ICommandHandler<IssueInvitationCommand, Result<InvitationListItemDto>>,
      ICommandHandler<ResendInvitationCommand, Result<InvitationListItemDto>>,
      ICommandHandler<RevokeInvitationCommand, Result<bool>>
{
    public async Task<Result<InvitationListItemDto>> HandleAsync(
        IssueInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(
            command.Kind,
            command.CampaignId,
            command.Actor,
            cancellationToken);
        if (authorization is not null)
        {
            return Result<InvitationListItemDto>.Failure(authorization);
        }

        try
        {
            return await unitOfWork.ExecuteSerializableAsync(async transactionCancellationToken =>
            {
                var now = timeProvider.GetUtcNow();
                var recipient = await ResolveRecipientAsync(command, transactionCancellationToken);
                if (!recipient.IsSuccess)
                {
                    return Result<InvitationListItemDto>.Failure(recipient.Error!);
                }

                var email = recipient.Value!;
                if (await invitations.HasPendingAsync(
                    command.Kind,
                    command.CampaignId,
                    email,
                    now,
                    transactionCancellationToken))
                {
                    return Conflict();
                }

                var issued = CreateInvitation(
                    command.Kind,
                    email,
                    command.CampaignId,
                    command.Actor.UserId!.Value,
                    now);
                invitations.Add(issued.Invitation);
                var eventMessage = outbox.Add(issued.Invitation.Id, tokenProtector.Protect(issued.Token), now);
                await eventPublisher.PublishAsync(eventMessage, transactionCancellationToken);
                metrics.InvitationIssued(InvitationOperation.Initial, Kind(command.Kind));
                return Result<InvitationListItemDto>.Success(ToDto(issued.Invitation));
            }, cancellationToken);
        }
        catch (ConcurrentOperationException)
        {
            return Conflict();
        }
        catch (InvitationEventPublishException exception)
        {
            return Result<InvitationListItemDto>.Failure(new ApplicationError(
                "invitation.event_broker_unavailable",
                ApplicationErrorType.Unavailable,
                exception.Message));
        }
    }

    public async Task<Result<InvitationListItemDto>> HandleAsync(
        ResendInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(
            command.Kind,
            command.CampaignId,
            command.Actor,
            cancellationToken);
        if (authorization is not null)
        {
            return Result<InvitationListItemDto>.Failure(authorization);
        }

        try
        {
            return await unitOfWork.ExecuteSerializableAsync(async transactionCancellationToken =>
        {
            var invitation = await invitations.FindByIdAsync(
                command.InvitationId,
                transactionCancellationToken);
            if (!MatchesScope(invitation, command.Kind, command.CampaignId))
            {
                return NotFound();
            }

            var now = timeProvider.GetUtcNow();
            if (!invitation!.IsPending(now))
            {
                return StateConflict();
            }

            var issues = await invitations.ListIssueTimesAsync(
                invitation.Kind,
                invitation.CampaignId,
                invitation.RecipientEmail,
                now.AddHours(-24),
                transactionCancellationToken);
            var mostRecent = issues.Count > 0 ? issues.Max() : invitation.IssuedAt;
            if (now < mostRecent.AddMinutes(15))
            {
                return RateLimited(mostRecent.AddMinutes(15));
            }

            if (issues.Count >= 5)
            {
                return RateLimited(issues.Min().AddHours(24));
            }

            invitation.Revoke(now);
            var replacement = CreateInvitation(
                invitation.Kind,
                invitation.RecipientEmail,
                invitation.CampaignId,
                invitation.IssuedByUserId,
                now);
            invitations.Add(replacement.Invitation);
            var eventMessage = outbox.Add(replacement.Invitation.Id, tokenProtector.Protect(replacement.Token), now);
            await eventPublisher.PublishAsync(eventMessage, transactionCancellationToken);
            metrics.InvitationIssued(InvitationOperation.Resend, Kind(invitation.Kind));
            return Result<InvitationListItemDto>.Success(ToDto(replacement.Invitation));
            }, cancellationToken);
        }
        catch (InvitationEventPublishException exception)
        {
            return Result<InvitationListItemDto>.Failure(new ApplicationError(
                "invitation.event_broker_unavailable",
                ApplicationErrorType.Unavailable,
                exception.Message));
        }
    }

    public async Task<Result<bool>> HandleAsync(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeAsync(
            command.Kind,
            command.CampaignId,
            command.Actor,
            cancellationToken);
        if (authorization is not null)
        {
            return Result<bool>.Failure(authorization);
        }

        var invitation = await invitations.FindByIdAsync(command.InvitationId, cancellationToken);
        if (!MatchesScope(invitation, command.Kind, command.CampaignId))
        {
            return Result<bool>.Failure(NotFoundError());
        }

        if (!invitation!.Revoke(timeProvider.GetUtcNow()))
        {
            return Result<bool>.Failure(new ApplicationError(
                "invitation.not_pending",
                ApplicationErrorType.Conflict,
                "La invitación ya no está pendiente."));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        metrics.InvitationRevoked(Kind(command.Kind));
        return Result<bool>.Success(true);
    }

    private async Task<ApplicationError?> AuthorizeAsync(
        InvitationKind kind,
        Guid? campaignId,
        AccessActor actor,
        CancellationToken cancellationToken)
    {
        if (!actor.UserId.HasValue
            || (kind == InvitationKind.Platform && !actor.IsPlatformAdmin)
            || (kind == InvitationKind.Campaign
                && (!campaignId.HasValue
                    || !(await campaigns.GetAccessAsync(
                        campaignId.Value,
                        actor.UserId.Value,
                        cancellationToken)).IsDm)))
        {
            return new ApplicationError(
                "access.forbidden",
                ApplicationErrorType.Forbidden,
                "No tienes permisos para gestionar estas invitaciones.");
        }

        return null;
    }

    private async Task<Result<string>> ResolveRecipientAsync(
        IssueInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(command.RecipientEmail);
        var hasUserId = command.RecipientUserId.HasValue;
        if (hasEmail == hasUserId
            || (command.Kind != InvitationKind.Campaign && hasUserId))
        {
            return Result<string>.Failure(new ApplicationError(
                "invitation.invalid_recipient",
                ApplicationErrorType.Validation,
                command.Kind == InvitationKind.Campaign
                    ? "Indica un correo o un usuario, pero no ambos."
                    : "Las invitaciones de plataforma requieren un correo."));
        }

        if (hasUserId)
        {
            var recipient = await users.FindByIdAsync(command.RecipientUserId!.Value, cancellationToken);
            if (recipient is null)
            {
                return Result<string>.Failure(new ApplicationError(
                    "invitation.recipient_not_found",
                    ApplicationErrorType.NotFound,
                    "No se ha encontrado el usuario destinatario."));
            }

            if (await IsAlreadyCampaignMemberAsync(command, recipient, cancellationToken))
            {
                return RecipientAlreadyMember();
            }

            return Result<string>.Success(recipient.Email);
        }

        try
        {
            var email = UserAccount.NormalizeEmail(command.RecipientEmail!);
            var recipient = await users.FindByEmailAsync(email, cancellationToken);
            return recipient is not null
                && await IsAlreadyCampaignMemberAsync(command, recipient, cancellationToken)
                    ? RecipientAlreadyMember()
                    : Result<string>.Success(email);
        }
        catch (ArgumentException exception)
        {
            return Result<string>.Failure(Validation(exception.Message).Error!);
        }
    }

    private async Task<bool> IsAlreadyCampaignMemberAsync(
        IssueInvitationCommand command,
        UserAccount recipient,
        CancellationToken cancellationToken) =>
        command.Kind == InvitationKind.Campaign
        && (command.Actor.UserId == recipient.Id
            || (command.CampaignId.HasValue
                && await campaignAccess.IsMemberAsync(
                    command.CampaignId.Value,
                    recipient.Id,
                    cancellationToken)));

    private static Result<string> RecipientAlreadyMember() =>
        Result<string>.Failure(new ApplicationError(
            "invitation.recipient_already_member",
            ApplicationErrorType.Conflict,
            "El usuario ya pertenece a la campaña."));

    private static IssuedInvitation CreateInvitation(
        InvitationKind kind,
        string email,
        Guid? campaignId,
        Guid issuedBy,
        DateTimeOffset now) => kind switch
        {
            InvitationKind.Platform => Invitation.IssuePlatform(email, issuedBy, now),
            InvitationKind.Campaign when campaignId.HasValue =>
                Invitation.IssueCampaign(email, campaignId.Value, issuedBy, now),
            _ => throw new InvalidOperationException("The invitation context is invalid."),
        };

    private static bool MatchesScope(Invitation? invitation, InvitationKind kind, Guid? campaignId) =>
        invitation is not null && invitation.Kind == kind && invitation.CampaignId == campaignId;

    private static Result<InvitationListItemDto> Validation(string message) =>
        Result<InvitationListItemDto>.Failure(new ApplicationError(
            "invitation.invalid_email",
            ApplicationErrorType.Validation,
            "El correo no es válido.",
            new Dictionary<string, string[]> { ["email"] = [message] }));

    private static Result<InvitationListItemDto> Conflict() =>
        Result<InvitationListItemDto>.Failure(new ApplicationError(
            "invitation.pending_exists",
            ApplicationErrorType.Conflict,
            "Ya existe una invitación pendiente."));

    private static Result<InvitationListItemDto> StateConflict() =>
        Result<InvitationListItemDto>.Failure(new ApplicationError(
            "invitation.not_pending",
            ApplicationErrorType.Conflict,
            "Solo se puede reenviar una invitación pendiente."));

    private static Result<InvitationListItemDto> NotFound() =>
        Result<InvitationListItemDto>.Failure(NotFoundError());

    private static ApplicationError NotFoundError() => new(
        "invitation.not_found",
        ApplicationErrorType.NotFound,
        "No se ha encontrado la invitación.");

    private static Result<InvitationListItemDto> RateLimited(DateTimeOffset retryAt) =>
        Result<InvitationListItemDto>.Failure(new ApplicationError(
            "invitation.resend_rate_limited",
            ApplicationErrorType.RateLimited,
            "El reenvío está limitado temporalmente.",
            RetryAt: retryAt));

    private static InvitationListItemDto ToDto(Invitation invitation) => new(
        invitation.Id,
        Kind(invitation.Kind),
        invitation.RecipientEmail,
        invitation.CampaignId,
        invitation.Status.ToString().ToLowerInvariant(),
        "pending",
        invitation.IssuedAt,
        invitation.ExpiresAt,
        invitation.LastSentAt);

    private static string Kind(InvitationKind kind) => kind.ToString().ToLowerInvariant();
}

internal sealed record AcceptInvitationCommand(
    string? Token,
    string? DisplayName,
    string? Password,
    AccessActor Actor);

internal sealed record InvitationAcceptanceDto(
    UserDto User,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    string Kind);

internal sealed class AcceptInvitationHandler(
    IInvitationRepository invitations,
    IUserAccountRepository users,
    IUserSessionRepository sessions,
    ICampaignAccessRepository campaignAccess,
    ICampaignInvitationContext campaigns,
    IPasswordService passwords,
    IAccessUnitOfWork unitOfWork,
    IAccessMetrics metrics,
    TimeProvider timeProvider)
    : ICommandHandler<AcceptInvitationCommand, Result<InvitationAcceptanceDto>>
{
    public async Task<Result<InvitationAcceptanceDto>> HandleAsync(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token) || command.Token.Length is < 32 or > 128)
        {
            return Gone();
        }

        try
        {
            return await unitOfWork.ExecuteSerializableAsync(async transactionCancellationToken =>
            {
                var invitation = await invitations.FindByTokenHashAsync(
                    Invitation.HashToken(command.Token),
                    transactionCancellationToken);
                if (invitation is null)
                {
                    return Gone();
                }

                var now = timeProvider.GetUtcNow();
                invitation.Expire(now);
                if (!invitation.IsPending(now))
                {
                    return Gone();
                }

                if (invitation.Kind == InvitationKind.Campaign
                    && invitation.CampaignId.HasValue
                    && !(await campaigns.GetAccessAsync(
                        invitation.CampaignId.Value,
                        Guid.Empty,
                        transactionCancellationToken)).Exists)
                {
                    return Gone();
                }

                var user = await users.FindByEmailAsync(
                    invitation.RecipientEmail,
                    transactionCancellationToken);
                IssuedUserSession? issuedSession = null;
                if (user is not null)
                {
                    if (!command.Actor.UserId.HasValue)
                    {
                        return Failure(ApplicationErrorType.Unauthorized, "identity.authentication_required");
                    }

                    if (command.Actor.UserId.Value != user.Id)
                    {
                        return Failure(ApplicationErrorType.Forbidden, "identity.wrong_account");
                    }
                }
                else
                {
                    var validation = ValidateNewAccount(command.DisplayName, command.Password);
                    if (validation.Count > 0)
                    {
                        return Result<InvitationAcceptanceDto>.Failure(new ApplicationError(
                            "identity.invalid_account",
                            ApplicationErrorType.Validation,
                            "Los datos de la cuenta no son válidos.",
                            validation));
                    }

                    user = UserAccount.Create(
                        invitation.RecipientEmail,
                        command.DisplayName!,
                        isPlatformAdmin: false,
                        now);
                    user.SetPasswordHash(passwords.Hash(user, command.Password!));
                    users.Add(user);
                    issuedSession = UserSession.Issue(user.Id, now);
                    sessions.Add(issuedSession.Session);
                }

                if (invitation.Kind == InvitationKind.Campaign
                    && invitation.CampaignId.HasValue
                    && !await campaignAccess.IsMemberAsync(
                        invitation.CampaignId.Value,
                        user.Id,
                        transactionCancellationToken))
                {
                    campaignAccess.Add(CampaignMembership.JoinAsPlayer(
                        invitation.CampaignId.Value,
                        user.Id,
                        now));
                }

                if (invitation.Accept(command.Token, user.Id, now) != InvitationAcceptanceResult.Accepted)
                {
                    return Gone();
                }

                metrics.InvitationAccepted(invitation.Kind.ToString().ToLowerInvariant());
                return Result<InvitationAcceptanceDto>.Success(new InvitationAcceptanceDto(
                    UserDto.FromDomain(user),
                    issuedSession?.Token,
                    issuedSession?.Session.ExpiresAt,
                    invitation.Kind.ToString().ToLowerInvariant()));
            }, cancellationToken);
        }
        catch (ConcurrentOperationException)
        {
            return Gone();
        }
    }

    private static IReadOnlyDictionary<string, string[]> ValidateNewAccount(
        string? displayName,
        string? password)
    {
        var errors = PasswordPolicy.Validate(password).ToDictionary(entry => entry.Key, entry => entry.Value);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length is < 2 or > 80)
        {
            errors["displayName"] = ["El nombre debe contener entre 2 y 80 caracteres."];
        }

        return errors;
    }

    private static Result<InvitationAcceptanceDto> Gone() => Failure(
        ApplicationErrorType.Gone,
        "invitation.unavailable");

    private static Result<InvitationAcceptanceDto> Failure(
        ApplicationErrorType type,
        string code) => Result<InvitationAcceptanceDto>.Failure(new ApplicationError(
            code,
            type,
            "La invitación no está disponible."));
}
