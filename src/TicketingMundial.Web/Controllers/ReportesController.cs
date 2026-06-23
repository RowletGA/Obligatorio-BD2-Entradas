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
        var hasta = NormalizeInclusiveHasta(model.Hasta);
        model.Eventos = await operativaService.ReporteEventosVendidosAsync(model.Desde, hasta, model.Limite, cancellationToken);
        model.Compradores = await operativaService.ReporteCompradoresAsync(model.Desde, hasta, model.Limite, cancellationToken);
        return View(model);
    }

    private static DateTime? NormalizeInclusiveHasta(DateTime? hasta)
    {
        return hasta.HasValue && hasta.Value.TimeOfDay == TimeSpan.Zero
            ? hasta.Value.Date.AddDays(1).AddTicks(-1)
            : hasta;
    }
}
