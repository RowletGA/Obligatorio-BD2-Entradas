using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Web.ViewModels;

public sealed class EventoDetalleViewModel
{
    public EventoDetalleDto Evento { get; init; } = new();
}
