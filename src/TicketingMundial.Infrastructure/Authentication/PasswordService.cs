using Microsoft.AspNetCore.Identity;
using TicketingMundial.Application.Abstractions.Authentication;
using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Infrastructure.Authentication;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<UsuarioAutenticacion> passwordHasher = new();

    public string HashPassword(UsuarioAutenticacion usuario, string password)
    {
        return passwordHasher.HashPassword(usuario, password);
    }

    public PasswordVerificationResult VerifyPassword(
        UsuarioAutenticacion usuario,
        string passwordHash,
        string providedPassword)
    {
        return passwordHasher.VerifyHashedPassword(usuario, passwordHash, providedPassword);
    }
}
