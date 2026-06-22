using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.DTOs;

public sealed class UsuarioAutenticadoDto
{
    public DocumentoUsuario Documento { get; init; } = new(string.Empty, string.Empty, string.Empty);
    public string PrimerNombre { get; init; } = string.Empty;
    public string PrimerApellido { get; init; } = string.Empty;
    public string CorreoElectronico { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; init; } = [];
}
