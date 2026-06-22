using TicketingMundial.Application.Validation;

namespace TicketingMundial.Tests;

public sealed class TelefonoValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_AcceptsSinglePhone()
    {
        var result = TelefonoValidator.ValidateAndNormalize(["099 123 456"]);

        Assert.True(result.IsValid);
        Assert.Equal("099123456", Assert.Single(result.TelefonosNormalizados));
    }

    [Fact]
    public void ValidateAndNormalize_AcceptsManyPhones()
    {
        var result = TelefonoValidator.ValidateAndNormalize(["099123456", "+598 91 234 567"]);

        Assert.True(result.IsValid);
        Assert.Equal(["099123456", "+59891234567"], result.TelefonosNormalizados);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsDuplicatesAfterNormalization()
    {
        var result = TelefonoValidator.ValidateAndNormalize(["099-123-456", "099123456"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("duplicados", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAndNormalize_RejectsEmptyValue()
    {
        var result = TelefonoValidator.ValidateAndNormalize(["099123456", ""]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("vacíos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAndNormalize_RejectsInvalidCharacters()
    {
        var result = TelefonoValidator.ValidateAndNormalize(["099ABC456"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("dígitos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAndNormalize_RejectsMaliciousPayload()
    {
        var result = TelefonoValidator.ValidateAndNormalize(["099123456<script>"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("dígitos", StringComparison.OrdinalIgnoreCase));
    }
}
