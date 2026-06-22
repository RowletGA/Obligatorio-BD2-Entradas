using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Application.Abstractions.Repositories;

public interface IEventoRepository
{
    Task<IReadOnlyList<EventoResumenDto>> ObtenerEventosAsync(
        EventoFiltroDto filtro,
        CancellationToken cancellationToken);

    Task<EventoResumenDto?> ObtenerEventoAsync(
        ulong idEvento,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorDisponibilidadDto>> ObtenerDisponibilidadAsync(
        ulong idEvento,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EstadioFiltroDto>> ObtenerEstadiosAsync(
        CancellationToken cancellationToken);
}
