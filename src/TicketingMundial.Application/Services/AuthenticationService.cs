using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TicketingMundial.Application.Abstractions.Authentication;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Application.Services;

public sealed class AuthenticationService(
    IUsuarioRepository usuarioRepository,
    IPasswordService passwordService,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private const string LoginError = "Correo o contraseña incorrectos.";

    public async Task<AuthenticationResult> AuthenticateAsync(
        string correoElectronico,
        string password,
        CancellationToken cancellationToken)
    {
        var correo = correoElectronico.Trim();
        var usuario = await usuarioRepository.ObtenerParaAutenticacionAsync(correo, cancellationToken);

        if (usuario is null || string.IsNullOrWhiteSpace(usuario.HashContrasena))
        {
            logger.LogWarning("Intento de login fallido para correo normalizado {Correo}.", correo);
            return AuthenticationResult.Failure(LoginError);
        }

        var verification = passwordService.VerifyPassword(usuario, usuario.HashContrasena, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Intento de login fallido para usuario {TipoDocumento}-{PaisDocumento}-{Numero}.",
                usuario.Documento.TipoDocumento,
                usuario.Documento.PaisDocumento,
                usuario.Documento.NumeroDocumento);
            return AuthenticationResult.Failure(LoginError);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = passwordService.HashPassword(usuario, password);
            await usuarioRepository.ActualizarHashContrasenaAsync(
                usuario.Documento,
                newHash,
                cancellationToken);
        }

        var authenticated = new UsuarioAutenticadoDto
        {
            Documento = usuario.Documento,
            PrimerNombre = usuario.PrimerNombre,
            PrimerApellido = usuario.PrimerApellido,
            CorreoElectronico = usuario.CorreoElectronico,
            Roles = usuario.Roles
        };

        return AuthenticationResult.Success(authenticated);
    }
}
