namespace DndCampaign.Api.Application.Identity;

public static class IdentityErrors
{
    public static string GetIdentityAccountValidationErrorText(IdentityAccountValidationErrors error)
    {
        switch (error)
        {
            case IdentityAccountValidationErrors.Email:
                return "Introduce una dirección de correo válida.";
            case IdentityAccountValidationErrors.DisplayName:
                return "El nombre debe contener entre 2 y 80 caracteres.";
            case IdentityAccountValidationErrors.PasswordTooShortOrTooLong:
                return "La contraseña debe contener entre 12 y 128 caracteres.";
            case IdentityAccountValidationErrors.PasswordRequiresLowerCase:
                return "La contraseña debe incluir una letra minúscula.";
            case IdentityAccountValidationErrors.PasswordRequiresUpperCase:
                return "La contraseña debe incluir una letra mayúscula.";
            case IdentityAccountValidationErrors.PasswordRequiresNumber:
                return "La contraseña debe incluir un número.";
            case IdentityAccountValidationErrors.PasswordRequiresSymbol:
                return "La contraseña debe incluir un símbolo.";
            default:
                throw new ArgumentOutOfRangeException(nameof(error), error, null);
        }
    }
}