using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Application.Abstractions.Services;

public interface IEventoService
{
    Task<IReadOnlyList<EventoResumenDto>> BuscarEventosAsync(
        EventoFiltroDto filtro,
        CancellationToken cancellationToken);

    Task<EventoDetalleDto?> ObtenerDetalleAsync(
        ulong idEvento,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EstadioFiltroDto>> ObtenerEstadiosAsync(
        CancellationToken cancellationToken);
}
