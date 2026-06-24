using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Infrastructure.Errors;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

public sealed class EventosController(IEventoService eventoService) : Controller
{
    public async Task<IActionResult> Index(
        DateTime? desde,
        DateTime? hasta,
        ulong? idEstadio,
        string? equipo,
        string? estado,
        CancellationToken cancellationToken)
    {
        try
        {
            var filtro = new EventoFiltroDto(desde, hasta, idEstadio, equipo, estado);
            var eventos = await eventoService.BuscarEventosAsync(filtro, cancellationToken);
            var estadios = await eventoService.ObtenerEstadiosAsync(cancellationToken);

            return View(new EventoIndexViewModel
            {
                Eventos = eventos,
                Estadios = estadios,
                Desde = desde,
                Hasta = hasta,
                IdEstadio = idEstadio,
                Equipo = equipo,
                Estado = estado
            });
        }
        catch (DatabaseException ex)
        {
            ViewData["Error"] = ex.UserMessage;
            return View(new EventoIndexViewModel());
        }
    }

    public async Task<IActionResult> Details(ulong id, CancellationToken cancellationToken)
    {
        try
        {
            var evento = await eventoService.ObtenerDetalleAsync(id, cancellationToken);
            if (evento is null)
            {
                return NotFound();
            }

            return View(new EventoDetalleViewModel { Evento = evento });
        }
        catch (DatabaseException ex)
        {
            TempData["Error"] = ex.UserMessage;
            return RedirectToAction(nameof(Index));
        }
    }
}
