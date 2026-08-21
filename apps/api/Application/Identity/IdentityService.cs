using DndCampaign.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace DndCampaign.Api.Application.Identity;

public sealed class IdentityService(
    IIdentityStore identity,
    ITransactionalBoundary transactions,
    IdentitySecurityOptions options,
    TimeProvider timeProvider,
    IPasswordHasher<UserAccount> passwordHasher) : IIdentityService
{
    public async Task<BootstrapStatus> GetBootstrapStatus(CancellationToken cancellationToken)
    {
        var hasUsers = await identity.HasAnyUsersAsync(cancellationToken);
        return hasUsers ? BootstrapStatus.Completed : BootstrapStatus.Required;
    }

    public async Task<(BootstrapCreationStatus status, IEnumerable<IdentityAccountValidationErrors> errors, UserProfile? user)>
        BootstrapAsync(BootstrapAccountCommand command, CancellationToken cancellationToken)
    {
        if (!SecretComparer.Equals(options.BootstrapToken, command.Token ?? string.Empty))
        {
            return (BootstrapCreationStatus.InvalidBootstrapToken, [], null);
        }

        var validationErrors = ValidateNewAccount(command.Email, command.DisplayName, command.Password);
        if (validationErrors.Any())
        {
            return (BootstrapCreationStatus.InvalidCredentials, validationErrors, null);
        }

        var outcome = await transactions.ExecuteSerializableAsync<(
            BootstrapCreationStatus Status,
            IEnumerable<IdentityAccountValidationErrors> Errors,
            UserProfile? User)>(async ct =>
        {
            if (await identity.HasAnyUsersAsync(ct))
            {
                return (
                    BootstrapCreationStatus.InitialRegistrationClosed,
                    Enumerable.Empty<IdentityAccountValidationErrors>(),
                    null);
            }

            var user = UserAccount.Create(
                command.Email!,
                command.DisplayName!,
                isPlatformAdmin: true,
                timeProvider.GetUtcNow());
            user.SetPasswordHash(passwordHasher.HashPassword(user, command.Password!));
            await identity.AddUserAsync(user, ct);

            return (
                BootstrapCreationStatus.Created,
                Enumerable.Empty<IdentityAccountValidationErrors>(),
                ToUserProfile(user));
        }, cancellationToken);

        if (outcome.Status == BootstrapCreationStatus.Created)
        {
            IdentityTelemetry.BootstrapCompletions.Add(1);
        }

        return outcome;
    }

    public async Task<LoginOutcome?> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        IdentityTelemetry.LoginAttempts.Add(1);
        string email;
        try
        {
            email = UserAccount.NormalizeEmail(command.Email ?? string.Empty);
        }
        catch (ArgumentException)
        {
            IdentityTelemetry.LoginFailures.Add(1);
            return null;
        }

        var user = await identity.FindByEmailAsync(email, cancellationToken);
        if (user is null || string.IsNullOrEmpty(command.Password))
        {
            IdentityTelemetry.LoginFailures.Add(1);
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            IdentityTelemetry.LoginFailures.Add(1);
            return null;
        }

        string? rehashedPasswordHash = null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            rehashedPasswordHash = passwordHasher.HashPassword(user, command.Password);
        }

        var issued = UserSession.Issue(user.Id, timeProvider.GetUtcNow());
        await identity.PersistLoginAsync(user.Id, rehashedPasswordHash, issued.Session, cancellationToken);
        return new LoginOutcome(issued.Token, issued.Session.ExpiresAt, ToUserProfile(user));
    }

    public async Task LogoutAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        var session = await identity.FindSessionByIdAsync(command.SessionId, cancellationToken);
        if (session is not null)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await identity.SaveSessionAsync(session, cancellationToken);
        }
    }

    public static IEnumerable<IdentityAccountValidationErrors> ValidateNewAccount(
        string? email,
        string? displayName,
        string? password)
    {
        var errors = PasswordPolicy.Validate(password).ToList();
        try
        {
            _ = UserAccount.NormalizeEmail(email ?? string.Empty);
        }
        catch (ArgumentException)
        {
            errors.Add(IdentityAccountValidationErrors.Email);
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length is < 2 or > 80)
        {
            errors.Add(IdentityAccountValidationErrors.DisplayName);
        }

        return errors;
    }

    public static UserProfile ToUserProfile(UserAccount user) =>
        new(user.Id, user.Email, user.DisplayName, user.IsPlatformAdmin);
}
