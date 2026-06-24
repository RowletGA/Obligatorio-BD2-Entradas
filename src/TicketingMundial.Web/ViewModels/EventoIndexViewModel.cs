using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Web.ViewModels;

public sealed class EventoIndexViewModel
{
    public IReadOnlyList<EventoResumenDto> Eventos { get; init; } = [];
    public IReadOnlyList<EstadioFiltroDto> Estadios { get; init; } = [];
    public DateTime? Desde { get; init; }
    public DateTime? Hasta { get; init; }
    public ulong? IdEstadio { get; init; }
    public ulong? IdSector { get; init; }
    public string? Equipo { get; init; }
    public string? Estado { get; init; }
    public string? Sort { get; init; }
    public string? Direction { get; init; }
}
