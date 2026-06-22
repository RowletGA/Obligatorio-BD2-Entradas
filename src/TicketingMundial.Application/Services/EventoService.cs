using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Application.Services;

public sealed class EventoService(IEventoRepository eventoRepository) : IEventoService
{
    public Task<IReadOnlyList<EventoResumenDto>> BuscarEventosAsync(
        EventoFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        return eventoRepository.ObtenerEventosAsync(filtro, cancellationToken);
    }

    public async Task<EventoDetalleDto?> ObtenerDetalleAsync(
        ulong idEvento,
        CancellationToken cancellationToken)
    {
        var evento = await eventoRepository.ObtenerEventoAsync(idEvento, cancellationToken);
        if (evento is null)
        {
            return null;
        }

        var sectores = await eventoRepository.ObtenerDisponibilidadAsync(idEvento, cancellationToken);

        return new EventoDetalleDto
        {
            IdEvento = evento.IdEvento,
            FechaHora = evento.FechaHora,
            Estado = evento.Estado,
            IdEstadio = evento.IdEstadio,
            Estadio = evento.Estadio,
            PaisEstadio = evento.PaisEstadio,
            LocalidadEstadio = evento.LocalidadEstadio,
            EquipoLocal = evento.EquipoLocal,
            EquipoVisitante = evento.EquipoVisitante,
            Sectores = sectores
        };
    }

    public Task<IReadOnlyList<EstadioFiltroDto>> ObtenerEstadiosAsync(
        CancellationToken cancellationToken)
    {
        return eventoRepository.ObtenerEstadiosAsync(cancellationToken);
    }
}
