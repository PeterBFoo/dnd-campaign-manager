namespace DndCampaign.Modules.Access.Application.Security;

internal static class PasswordPolicy
{
    public static IReadOnlyDictionary<string, string[]> Validate(string? password)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length is < 12 or > 128)
        {
            errors.Add("La contraseña debe contener entre 12 y 128 caracteres.");
        }
        else
        {
            if (!password.Any(char.IsUpper))
            {
                errors.Add("La contraseña debe incluir una letra mayúscula.");
            }

            if (!password.Any(char.IsLower))
            {
                errors.Add("La contraseña debe incluir una letra minúscula.");
            }

            if (!password.Any(char.IsDigit))
            {
                errors.Add("La contraseña debe incluir un número.");
            }

            if (!password.Any(character => !char.IsLetterOrDigit(character)))
            {
                errors.Add("La contraseña debe incluir un símbolo.");
            }
        }

        return errors.Count == 0
            ? new Dictionary<string, string[]>()
            : new Dictionary<string, string[]> { ["password"] = [.. errors] };
    }
}
