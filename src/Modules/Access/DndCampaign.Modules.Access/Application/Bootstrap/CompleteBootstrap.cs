using DndCampaign.Modules.Access.Application.Abstractions.Messaging;
using DndCampaign.Modules.Access.Application.Abstractions.Results;
using DndCampaign.Modules.Access.Application.Ports.Observability;
using DndCampaign.Modules.Access.Application.Ports.Persistence;
using DndCampaign.Modules.Access.Application.Ports.Security;
using DndCampaign.Modules.Access.Application.Security;
using DndCampaign.Modules.Access.Application.Users;
using DndCampaign.Modules.Access.Domain.Accounts;

namespace DndCampaign.Modules.Access.Application.Bootstrap;

internal sealed record CompleteBootstrapCommand(
    string? Token,
    string? Email,
    string? DisplayName,
    string? Password);

internal sealed class CompleteBootstrapHandler(
    IUserAccountRepository users,
    IAccessUnitOfWork unitOfWork,
    IBootstrapTokenVerifier tokenVerifier,
    IPasswordService passwords,
    IAccessMetrics metrics,
    TimeProvider timeProvider)
    : ICommandHandler<CompleteBootstrapCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> HandleAsync(
        CompleteBootstrapCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!tokenVerifier.Matches(command.Token ?? string.Empty))
        {
            return Result<UserDto>.Failure(new ApplicationError(
                "identity.invalid_credentials",
                ApplicationErrorType.Unauthorized,
                "No se han podido validar las credenciales."));
        }

        var validationErrors = Validate(command);
        if (validationErrors.Count > 0)
        {
            return Result<UserDto>.Failure(new ApplicationError(
                "identity.invalid_account",
                ApplicationErrorType.Validation,
                "Los datos de la cuenta no son válidos.",
                validationErrors));
        }

        Result<UserDto> result;
        try
        {
            result = await unitOfWork.ExecuteSerializableAsync(async transactionCancellationToken =>
            {
                if (await users.AnyAsync(transactionCancellationToken))
                {
                    return Result<UserDto>.Failure(new ApplicationError(
                        "identity.bootstrap_closed",
                        ApplicationErrorType.Conflict,
                        "La primera cuenta de administración ya fue creada."));
                }

                var user = UserAccount.Create(
                    command.Email!,
                    command.DisplayName!,
                    isPlatformAdmin: true,
                    timeProvider.GetUtcNow());
                user.SetPasswordHash(passwords.Hash(user, command.Password!));
                users.Add(user);
                return Result<UserDto>.Success(UserDto.FromDomain(user));
            }, cancellationToken);
        }
        catch (ConcurrentOperationException)
        {
            result = Result<UserDto>.Failure(new ApplicationError(
                "identity.bootstrap_closed",
                ApplicationErrorType.Conflict,
                "La primera cuenta de administración ya fue creada."));
        }
        if (result.IsSuccess)
        {
            metrics.BootstrapCompleted();
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string[]> Validate(CompleteBootstrapCommand command)
    {
        var errors = PasswordPolicy.Validate(command.Password)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        try
        {
            _ = UserAccount.NormalizeEmail(command.Email ?? string.Empty);
        }
        catch (ArgumentException)
        {
            errors["email"] = ["Introduce una dirección de correo válida."];
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName)
            || command.DisplayName.Trim().Length is < 2 or > 80)
        {
            errors["displayName"] = ["El nombre debe contener entre 2 y 80 caracteres."];
        }

        return errors;
    }
}
