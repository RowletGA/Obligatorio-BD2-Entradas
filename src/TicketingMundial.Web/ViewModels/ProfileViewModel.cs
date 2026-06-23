namespace TicketingMundial.Web.ViewModels;

public sealed class ProfileViewModel
{
    public string Nombre { get; init; } = string.Empty;
    public string CorreoElectronico { get; init; } = string.Empty;
    public string? TipoDocumento { get; init; }
    public string? PaisDocumento { get; init; }
    public string? NumeroDocumento { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public string PerfilActivo { get; init; } = string.Empty;
    public bool EsUsuarioGeneral { get; init; }
}
