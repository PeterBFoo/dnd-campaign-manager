using DndCampaign.Api.Application.Identity;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DndCampaign.Api.Application.Identity;

/// <summary>
/// Identity use cases. Temporary debt: uses <see cref="CampaignDbContext"/> directly until persistence is abstracted.
/// </summary>
public sealed class IdentityService(
    CampaignDbContext database,
    IdentitySecurityOptions options,
    TimeProvider timeProvider,
    IPasswordHasher<UserAccount> passwordHasher) : IIdentityService
{
    public async Task<BootstrapStatus> GetBootstrapStatus(CancellationToken cancellationToken)
    {
        var hasUsers = await database.Users.AnyAsync(cancellationToken);
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

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (await database.Users.AnyAsync(cancellationToken))
        {
            return (BootstrapCreationStatus.InitialRegistrationClosed, [], null);
        }

        var user = UserAccount.Create(
            command.Email!,
            command.DisplayName!,
            isPlatformAdmin: true,
            timeProvider.GetUtcNow());
        user.SetPasswordHash(passwordHasher.HashPassword(user, command.Password!));
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        IdentityTelemetry.BootstrapCompletions.Add(1);
        return (BootstrapCreationStatus.Created, [], ToUserProfile(user));
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

        var user = await database.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);
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

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, command.Password));
        }

        var issued = UserSession.Issue(user.Id, timeProvider.GetUtcNow());
        database.UserSessions.Add(issued.Session);
        await database.SaveChangesAsync(cancellationToken);
        return new LoginOutcome(issued.Token, issued.Session.ExpiresAt, ToUserProfile(user));
    }

    public async Task LogoutAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        var session = await database.UserSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == command.SessionId,
            cancellationToken);
        if (session is not null)
        {
            session.Revoke(timeProvider.GetUtcNow());
            await database.SaveChangesAsync(cancellationToken);
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
