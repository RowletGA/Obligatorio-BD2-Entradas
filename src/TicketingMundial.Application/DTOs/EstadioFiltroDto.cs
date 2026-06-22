namespace TicketingMundial.Application.DTOs;

public sealed class EstadioFiltroDto
{
    public ulong IdEstadio { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Pais { get; init; } = string.Empty;
    public string Localidad { get; init; } = string.Empty;
}
