namespace TicketingMundial.Application.Validation;

public static class PasswordPolicyValidator
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static PasswordValidationResult Validate(string password, string confirmation)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("La contraseña es obligatoria.");
        }
        else
        {
            if (password.Length < MinLength)
            {
                errors.Add("La contraseña debe tener al menos 8 caracteres.");
            }

            if (password.Length > MaxLength)
            {
                errors.Add("La contraseña no puede superar 128 caracteres.");
            }

            if (!password.Any(char.IsLetter))
            {
                errors.Add("La contraseña debe contener al menos una letra.");
            }

            if (!password.Any(char.IsDigit))
            {
                errors.Add("La contraseña debe contener al menos un número.");
            }
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            errors.Add("La confirmación de contraseña no coincide.");
        }

        return errors.Count == 0
            ? PasswordValidationResult.Success
            : new PasswordValidationResult(false, errors);
    }
}
