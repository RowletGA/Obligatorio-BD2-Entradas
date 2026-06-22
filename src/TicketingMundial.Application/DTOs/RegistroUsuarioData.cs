using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.DTOs;

public sealed class RegistroUsuarioData
{
    public DocumentoUsuario Documento { get; init; } = new(string.Empty, string.Empty, string.Empty);
    public string PrimerNombre { get; init; } = string.Empty;
    public string PrimerApellido { get; init; } = string.Empty;
    public string CorreoElectronico { get; init; } = string.Empty;
    public string? DireccionPais { get; init; }
    public string? DireccionLocalidad { get; init; }
    public string? DireccionCalle { get; init; }
    public string? DireccionNumero { get; init; }
    public string? DireccionCodigoPostal { get; init; }
    public IReadOnlyList<string> Telefonos { get; init; } = [];
}
