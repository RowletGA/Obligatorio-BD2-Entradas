using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize(Roles = RolesAplicacion.Administrador)]
[Route("Admin/Reportes")]
public sealed class ReportesController(IOperativaService operativaService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ReportesViewModel model, CancellationToken cancellationToken)
    {
        model.Eventos = await operativaService.ReporteEventosVendidosAsync(model.Desde, model.Hasta, model.Limite, cancellationToken);
        model.Compradores = await operativaService.ReporteCompradoresAsync(model.Desde, model.Hasta, model.Limite, cancellationToken);
        return View(model);
    }
}
