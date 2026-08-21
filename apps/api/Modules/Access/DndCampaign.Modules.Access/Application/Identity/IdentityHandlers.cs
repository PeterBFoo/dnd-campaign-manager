using DndCampaign.Modules.Access.Application.Abstractions.Messaging;
using DndCampaign.Modules.Access.Application.Abstractions.Results;
using DndCampaign.Modules.Access.Application.Ports.Observability;
using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Application.Ports.Security;
using DndCampaign.Modules.Access.Application.Users;
using DndCampaign.Modules.Access.Domain.Accounts;
using DndCampaign.Modules.Access.Domain.Sessions;

namespace DndCampaign.Modules.Access.Application.Identity;

internal sealed record AccessActor(
    Guid? UserId,
    Guid? SessionId,
    string Email,
    string DisplayName,
    bool IsPlatformAdmin)
{
    public bool IsAuthenticated => UserId.HasValue;
}

internal sealed record LoginCommand(string? Email, string? Password);

internal sealed record SessionDto(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

internal sealed class LoginHandler(
    IUserAccountRepository users,
    IUserSessionRepository sessions,
    IPasswordService passwords,
    IAccessUnitOfWork unitOfWork,
    IAccessMetrics metrics,
    TimeProvider timeProvider)
    : ICommandHandler<LoginCommand, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        metrics.LoginAttempted();
        string email;
        try
        {
            email = UserAccount.NormalizeEmail(command.Email ?? string.Empty);
        }
        catch (ArgumentException)
        {
            return InvalidCredentials();
        }

        var user = await users.FindByEmailAsync(email, cancellationToken);
        if (user is null || string.IsNullOrEmpty(command.Password))
        {
            return InvalidCredentials();
        }

        var verification = passwords.Verify(user, user.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwords.Hash(user, command.Password));
        }

        var issued = UserSession.Issue(user.Id, timeProvider.GetUtcNow());
        sessions.Add(issued.Session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SessionDto>.Success(new SessionDto(
            issued.Token,
            issued.Session.ExpiresAt,
            UserDto.FromDomain(user)));
    }

    private Result<SessionDto> InvalidCredentials()
    {
        metrics.LoginFailed();
        return Result<SessionDto>.Failure(new ApplicationError(
            "identity.invalid_credentials",
            ApplicationErrorType.Unauthorized,
            "No se han podido validar las credenciales."));
    }
}

internal sealed record LogoutCommand(Guid SessionId);

internal sealed class LogoutHandler(
    IUserSessionRepository sessions,
    IAccessUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<LogoutCommand, bool>
{
    public async Task<bool> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var session = await sessions.FindByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return false;
        }

        session.Revoke(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal sealed record GetCurrentUserQuery(AccessActor Actor);

internal sealed class GetCurrentUserHandler : IQueryHandler<GetCurrentUserQuery, UserDto>
{
    public Task<UserDto> HandleAsync(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserDto(
            query.Actor.UserId!.Value,
            query.Actor.Email,
            query.Actor.DisplayName,
            query.Actor.IsPlatformAdmin));
}
