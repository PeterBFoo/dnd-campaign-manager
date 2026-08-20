namespace DndCampaign.Api.Application.Identity;

public static class PasswordPolicy
{
    public static IEnumerable<IdentityAccountValidationErrors> Validate(string? password)
    {
        var errors = new List<IdentityAccountValidationErrors>();
        if (string.IsNullOrEmpty(password) || password.Length is < 12 or > 128)
        {
            errors.Add(IdentityAccountValidationErrors.PasswordTooShortOrTooLong);
            return errors;
        }
        if (!password.Any(char.IsUpper))
            errors.Add(IdentityAccountValidationErrors.PasswordRequiresUpperCase);
        if (!password.Any(char.IsLower))
            errors.Add(IdentityAccountValidationErrors.PasswordRequiresLowerCase);
        if (!password.Any(char.IsDigit))
            errors.Add(IdentityAccountValidationErrors.PasswordRequiresNumber);
        if (password.All(char.IsLetterOrDigit))
            errors.Add(IdentityAccountValidationErrors.PasswordRequiresSymbol);

        return errors;
    }
}
