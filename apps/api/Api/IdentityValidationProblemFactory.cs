using DndCampaign.Api.Application.Identity;
using Microsoft.AspNetCore.Mvc;

public static class IdentityValidationProblemFactory
{
    public static ValidationProblemDetails Create(
        IEnumerable<IdentityAccountValidationErrors> errors)
    {
        var errorsArray = errors.ToArray();

        if (errorsArray.Length == 0)
        {
            throw new InvalidOperationException(
                "No validation errors occurred.");
        }

        var formattedErrors = new Dictionary<string, List<string>>();

        foreach (var error in errorsArray)
        {
            var field = error switch
            {
                IdentityAccountValidationErrors.Email => "email",

                IdentityAccountValidationErrors.DisplayName => "displayName",

                IdentityAccountValidationErrors.PasswordTooShortOrTooLong
                    or IdentityAccountValidationErrors.PasswordRequiresLowerCase
                    or IdentityAccountValidationErrors.PasswordRequiresUpperCase
                    or IdentityAccountValidationErrors.PasswordRequiresNumber
                    or IdentityAccountValidationErrors.PasswordRequiresSymbol
                    => "password",

                _ => throw new ArgumentOutOfRangeException(
                    nameof(error),
                    error,
                    null)
            };

            if (!formattedErrors.TryGetValue(field, out var fieldErrors))
            {
                fieldErrors = [];
                formattedErrors[field] = fieldErrors;
            }

            fieldErrors.Add(
                IdentityErrors.GetIdentityAccountValidationErrorText(error));
        }

        return new ValidationProblemDetails(
            formattedErrors.ToDictionary(
                x => x.Key,
                x => x.Value.ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
    }
}