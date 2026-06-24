using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize(Roles = RolesAplicacion.UsuarioGeneral)]
public sealed class EntradasController(IOperativaService operativaService) : Controller
{
    [HttpGet("/Entradas/MisEntradas")]
    public async Task<IActionResult> MisEntradas(CancellationToken cancellationToken)
    {
        SetNoStore();
        return View(await operativaService.ListarEntradasPropiasAsync(GetDocumento(), cancellationToken));
    }

    [HttpGet("/Entradas/Detalle/{id:long}")]
    public async Task<IActionResult> Detalle(ulong id, CancellationToken cancellationToken)
    {
        SetNoStore();
        var entrada = await operativaService.ObtenerEntradaPropiaAsync(GetDocumento(), id, cancellationToken);
        return entrada is null ? NotFound() : View(entrada);
    }

    [HttpGet("/Entradas/QrDinamico/{idEntrada:long}")]
    public async Task<IActionResult> QrDinamico(ulong idEntrada, string? grant, CancellationToken cancellationToken)
    {
        var result = await operativaService.GenerarQrEntradaAsync(GetDocumento(), idEntrada, grant, cancellationToken);
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        if (!result.Success || result.Value is null)
        {
            return NotFound(new { mensaje = result.Message ?? "Entrada no disponible." });
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(result.Value.Token, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return Json(new
        {
            imagenBase64 = Convert.ToBase64String(png),
            token = result.Value.Token,
            venceUtc = result.Value.VenceUtc,
            segundosRestantes = result.Value.SegundosRestantes,
            generationGrant = result.Value.GenerationGrant,
            generationGrantVenceUtc = result.Value.GenerationGrantVenceUtc,
            consultoBase = result.Value.ConsultoBase
        });
    }

    [HttpGet("/Entradas/Transferir/{id:long}")]
    public async Task<IActionResult> Transferir(ulong id, CancellationToken cancellationToken)
    {
        var entrada = await operativaService.ObtenerEntradaPropiaAsync(GetDocumento(), id, cancellationToken);
        if (entrada is null)
        {
            return NotFound();
        }

        if (entrada.EstadoEntrada != "ACTIVA" || entrada.EstadoEvento is "FINALIZADO" or "CANCELADO")
        {
            TempData["Error"] = "La entrada ya no puede transferirse.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return View(new TransferenciaCrearViewModel { IdEntrada = id });
    }

    private DocumentoUsuario GetDocumento() => new(User.GetTipoDocumento() ?? string.Empty, User.GetPaisDocumento() ?? string.Empty, User.GetNumeroDocumento() ?? string.Empty);

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
    }
}
