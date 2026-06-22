namespace TicketingMundial.Application.Validation;

public sealed record TelefonoValidationResult(
    bool IsValid,
    IReadOnlyList<string> TelefonosNormalizados,
    IReadOnlyList<string> Errors)
{
    public static TelefonoValidationResult Success(IReadOnlyList<string> telefonos)
    {
        return new TelefonoValidationResult(true, telefonos, []);
    }

    public static TelefonoValidationResult Failure(IReadOnlyList<string> telefonos, IReadOnlyList<string> errors)
    {
        return new TelefonoValidationResult(false, telefonos, errors);
    }
}
