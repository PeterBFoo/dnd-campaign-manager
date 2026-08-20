using System.Data;
using DndCampaign.Api.Api;
using DndCampaign.Api.Domain.Identity;
using DndCampaign.Api.Infrastructure.Observability;
using DndCampaign.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace DndCampaign.Api.Application.Identity;

public sealed class IdentityService(CampaignDbContext database, IdentitySecurityOptions options, TimeProvider timeProvider, IPasswordHasher<UserAccount> passwordHasher)
{
    public async Task<BootstrapStatus> GetBootstrapStatus(CancellationToken cancellationToken)
    {
        var hasUsers = await database.Users.AnyAsync(cancellationToken);
        return hasUsers ? BootstrapStatus.Completed : BootstrapStatus.Required;
    }
    
    public async Task<(BootstrapCreationStatus status, IEnumerable<IdentityAccountValidationErrors> errors, UserResponse? userResponse)> 
        BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
        if (!SecretComparer.Equals(options.BootstrapToken, request.Token ?? string.Empty))
        {
            return (BootstrapCreationStatus.InvalidBootstrapToken, [], null);
        }

        var validationErrors = ValidateNewAccount(request.Email, request.DisplayName, request.Password);
        if (validationErrors.Any())
        {
            return (BootstrapCreationStatus.InvalidCredentials, validationErrors, null);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await database.Users.AnyAsync(cancellationToken))
        {
            return (BootstrapCreationStatus.InitialRegistrationClosed, [], null);
        }

        var user = UserAccount.Create(
            request.Email!,
            request.DisplayName!,
            isPlatformAdmin: true,
            timeProvider.GetUtcNow());
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password!));
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        
        IdentityTelemetry.BootstrapCompletions.Add(1);
        return (BootstrapCreationStatus.Created, [], ToUserResponse(user));
    }
    
    public static IEnumerable<IdentityAccountValidationErrors> ValidateNewAccount(string? email, string? displayName, string? password)
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
            errors.Add(IdentityAccountValidationErrors.DisplayName);
        
        return errors;
    }
    
    public static UserResponse ToUserResponse(UserAccount user) =>
        new(user.Id, user.Email, user.DisplayName, user.IsPlatformAdmin);
}