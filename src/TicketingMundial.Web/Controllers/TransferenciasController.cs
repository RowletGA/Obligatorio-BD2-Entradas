using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Errors;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize(Roles = RolesAplicacion.UsuarioGeneral)]
public sealed class TransferenciasController(IOperativaService operativaService) : Controller
{
    [HttpGet("/Transferencias")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var doc = GetDocumento();
        return View(new TransferenciasIndexViewModel
        {
            Enviadas = await operativaService.ListarTransferenciasEnviadasAsync(doc, cancellationToken),
            Recibidas = await operativaService.ListarTransferenciasRecibidasAsync(doc, cancellationToken)
        });
    }

    [HttpPost("/Transferencias/Crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(TransferenciaCrearViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Entradas/Transferir.cshtml", model);
        }

        try
        {
            var result = await operativaService.CrearTransferenciaAsync(GetDocumento(), model.IdEntrada, model.CorreoDestino, cancellationToken);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
        }
        catch (DatabaseException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/Transferencias/{id:long}/Aceptar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aceptar(ulong id, CancellationToken cancellationToken) => await Responder(id, "ACEPTADA", true, cancellationToken);

    [HttpPost("/Transferencias/{id:long}/Rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(ulong id, CancellationToken cancellationToken) => await Responder(id, "RECHAZADA", true, cancellationToken);

    [HttpPost("/Transferencias/{id:long}/Cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(ulong id, CancellationToken cancellationToken) => await Responder(id, "CANCELADA", false, cancellationToken);

    private async Task<IActionResult> Responder(ulong id, string estado, bool receptor, CancellationToken cancellationToken)
    {
        try
        {
            var result = await operativaService.ResponderTransferenciaAsync(GetDocumento(), id, estado, receptor, cancellationToken);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
        }
        catch (DatabaseException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    private DocumentoUsuario GetDocumento() => new(User.GetTipoDocumento() ?? string.Empty, User.GetPaisDocumento() ?? string.Empty, User.GetNumeroDocumento() ?? string.Empty);
}
