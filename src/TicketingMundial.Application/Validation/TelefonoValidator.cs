namespace TicketingMundial.Application.Validation;

public static class TelefonoValidator
{
    public static TelefonoValidationResult ValidateAndNormalize(IEnumerable<string?> telefonos)
    {
        var errors = new List<string>();
        var normalizados = new List<string>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var original in telefonos)
        {
            var value = original?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                errors.Add("No se permiten teléfonos vacíos.");
                continue;
            }

            var normalizado = Normalize(value);
            if (normalizado.Length < 7 || normalizado.Length > 20)
            {
                errors.Add("Cada teléfono debe tener entre 7 y 20 caracteres.");
            }

            if (!IsValid(normalizado))
            {
                errors.Add("Los teléfonos solo pueden contener un prefijo internacional + seguido de dígitos.");
            }

            if (!vistos.Add(normalizado))
            {
                errors.Add("No se permiten teléfonos duplicados.");
            }
            else
            {
                normalizados.Add(normalizado);
            }
        }

        if (normalizados.Count == 0)
        {
            errors.Add("Ingrese al menos un teléfono.");
        }

        return errors.Count == 0
            ? TelefonoValidationResult.Success(normalizados)
            : TelefonoValidationResult.Failure(normalizados, errors.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string Normalize(string value)
    {
        var result = new char[value.Length];
        var length = 0;

        foreach (var character in value)
        {
            if (character is ' ' or '-' or '(' or ')')
            {
                continue;
            }

            result[length++] = character;
        }

        return new string(result, 0, length);
    }

    private static bool IsValid(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var start = 0;
        if (value[0] == '+')
        {
            start = 1;
            if (value.Length == 1)
            {
                return false;
            }
        }

        for (var index = start; index < value.Length; index++)
        {
            if (!char.IsAsciiDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
