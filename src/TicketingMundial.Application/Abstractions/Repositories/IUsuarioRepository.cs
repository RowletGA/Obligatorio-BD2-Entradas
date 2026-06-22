using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Abstractions.Repositories;

public interface IUsuarioRepository
{
    Task<UsuarioAutenticacion?> ObtenerParaAutenticacionAsync(
        string correoElectronico,
        CancellationToken cancellationToken);

    Task<bool> ExisteCorreoAsync(
        string correoElectronico,
        CancellationToken cancellationToken);

    Task<bool> ExisteDocumentoAsync(
        DocumentoUsuario documento,
        CancellationToken cancellationToken);

    Task RegistrarUsuarioGeneralAsync(
        RegistroUsuarioData usuario,
        string hashContrasena,
        CancellationToken cancellationToken);

    Task ActualizarHashContrasenaAsync(
        DocumentoUsuario documento,
        string hashContrasena,
        CancellationToken cancellationToken);
}
