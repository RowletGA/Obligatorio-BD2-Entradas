using Microsoft.AspNetCore.Identity;
using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Application.Abstractions.Authentication;

public interface IPasswordService
{
    string HashPassword(UsuarioAutenticacion usuario, string password);

    PasswordVerificationResult VerifyPassword(
        UsuarioAutenticacion usuario,
        string passwordHash,
        string providedPassword);
}
