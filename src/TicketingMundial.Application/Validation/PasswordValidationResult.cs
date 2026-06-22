namespace TicketingMundial.Application.Validation;

public sealed record PasswordValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PasswordValidationResult Success { get; } = new(true, []);
}
