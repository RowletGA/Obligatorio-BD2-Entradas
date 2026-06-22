using TicketingMundial.Application.Validation;

namespace TicketingMundial.Tests;

public sealed class PasswordPolicyValidatorTests
{
    [Fact]
    public void Validate_AcceptsValidPassword()
    {
        var result = PasswordPolicyValidator.Validate("Entrada2026", "Entrada2026");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsShortPassword()
    {
        var result = PasswordPolicyValidator.Validate("A123456", "A123456");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("al menos 8", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsLongPassword()
    {
        var password = $"A1{new string('x', 127)}";

        var result = PasswordPolicyValidator.Validate(password, password);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("128", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsPasswordWithoutLetters()
    {
        var result = PasswordPolicyValidator.Validate("12345678", "12345678");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("letra", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsPasswordWithoutNumbers()
    {
        var result = PasswordPolicyValidator.Validate("abcdefgh", "abcdefgh");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("número", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsDifferentConfirmation()
    {
        var result = PasswordPolicyValidator.Validate("Entrada2026", "Entrada2027");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("confirmación", StringComparison.OrdinalIgnoreCase));
    }
}
