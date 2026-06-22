using Microsoft.AspNetCore.Identity;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Authentication;

namespace TicketingMundial.Tests;

public sealed class PasswordServiceTests
{
    private static readonly UsuarioAutenticacion Usuario = new()
    {
        Documento = new DocumentoUsuario("CI", "Uruguay", "12345678"),
        CorreoElectronico = "usuario@example.com",
        PrimerNombre = "Ana",
        PrimerApellido = "Demo"
    };

    [Fact]
    public void HashPassword_DoesNotReturnPlainText_AndVerifies()
    {
        var service = new PasswordService();

        var hash = service.HashPassword(Usuario, "Entrada2026");
        var result = service.VerifyPassword(Usuario, hash, "Entrada2026");

        Assert.NotEqual("Entrada2026", hash);
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyPassword_RejectsWrongPassword()
    {
        var service = new PasswordService();
        var hash = service.HashPassword(Usuario, "Entrada2026");

        var result = service.VerifyPassword(Usuario, hash, "Otra2026");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyPassword_RejectsTamperedHash()
    {
        var service = new PasswordService();
        var hash = service.HashPassword(Usuario, "Entrada2026");
        var tampered = hash[..^2] + "AA";

        var result = service.VerifyPassword(Usuario, tampered, "Entrada2026");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void PasswordHasher_CanReportSuccessRehashNeeded()
    {
        var weakOptions = Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions
        {
            IterationCount = 1
        });
        var weakHasher = new PasswordHasher<UsuarioAutenticacion>(weakOptions);
        var weakHash = weakHasher.HashPassword(Usuario, "Entrada2026");

        var result = new PasswordService().VerifyPassword(Usuario, weakHash, "Entrada2026");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }
}
