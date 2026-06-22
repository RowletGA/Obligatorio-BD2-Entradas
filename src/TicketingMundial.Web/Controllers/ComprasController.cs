using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Errors;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize(Roles = RolesAplicacion.UsuarioGeneral)]
public sealed class ComprasController(IOperativaService operativaService) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(CompraFormViewModel model, CancellationToken cancellationToken)
    {
        var cantidades = ToCantidades(model);
        var preview = await operativaService.PreviewCompraAsync(model.IdEvento, cantidades, cancellationToken);
        if (!preview.Success || preview.Value is null)
        {
            TempData["Error"] = preview.Message;
            return RedirectToAction("Details", "Eventos", new { id = model.IdEvento });
        }

        return View(preview.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comprar(CompraFormViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var result = await operativaService.ComprarAsync(GetDocumento(), model.IdEvento, ToCantidades(model), cancellationToken);
            if (!result.Success || result.Value is null)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Details", "Eventos", new { id = model.IdEvento });
            }

            TempData["Success"] = $"{result.Message} Total: $ {result.Value.MontoTotal:N2}";
            return RedirectToAction(nameof(Detalle), new { id = result.Value.IdVenta });
        }
        catch (DatabaseException ex)
        {
            TempData["Error"] = ex.UserMessage;
            return RedirectToAction("Details", "Eventos", new { id = model.IdEvento });
        }
    }

    [HttpGet("/Compras")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await operativaService.ListarComprasAsync(GetDocumento(), cancellationToken));
    }

    [HttpGet("/Compras/Detalle/{id:long}")]
    public async Task<IActionResult> Detalle(ulong id, CancellationToken cancellationToken)
    {
        var compra = await operativaService.ObtenerCompraAsync(GetDocumento(), id, cancellationToken);
        return compra is null ? NotFound() : View(compra);
    }

    private DocumentoUsuario GetDocumento() => new(User.GetTipoDocumento() ?? string.Empty, User.GetPaisDocumento() ?? string.Empty, User.GetNumeroDocumento() ?? string.Empty);

    private static IReadOnlyList<CompraSectorCantidad> ToCantidades(CompraFormViewModel model) =>
        model.Sectores.Select(s => new CompraSectorCantidad(s.IdSector, s.Cantidad)).ToArray();
}
