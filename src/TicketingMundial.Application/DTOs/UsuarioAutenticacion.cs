using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.DTOs;

public sealed class UsuarioAutenticacion
{
    public DocumentoUsuario Documento { get; init; } = new(string.Empty, string.Empty, string.Empty);
    public string PrimerNombre { get; init; } = string.Empty;
    public string PrimerApellido { get; init; } = string.Empty;
    public string CorreoElectronico { get; init; } = string.Empty;
    public string? HashContrasena { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
}
