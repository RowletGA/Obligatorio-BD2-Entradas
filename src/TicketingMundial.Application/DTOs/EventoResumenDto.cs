namespace TicketingMundial.Application.DTOs;

public sealed class EventoResumenDto
{
    public ulong IdEvento { get; init; }
    public DateTime FechaHora { get; init; }
    public string Estado { get; init; } = string.Empty;
    public ulong IdEstadio { get; init; }
    public string Estadio { get; init; } = string.Empty;
    public string PaisEstadio { get; init; } = string.Empty;
    public string LocalidadEstadio { get; init; } = string.Empty;
    public string? EquipoLocal { get; init; }
    public string? EquipoVisitante { get; init; }
    public string? GrupoLocal { get; init; }
    public string? GrupoVisitante { get; init; }
    public long LugaresDisponibles { get; init; }
    public bool PermiteCompra => Estado == "PROGRAMADO" && LugaresDisponibles > 0;
}
