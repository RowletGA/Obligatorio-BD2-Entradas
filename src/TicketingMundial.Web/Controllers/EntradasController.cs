using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        return View(await operativaService.ListarEntradasPropiasAsync(GetDocumento(), cancellationToken));
    }

    [HttpGet("/Entradas/Detalle/{id:long}")]
    public async Task<IActionResult> Detalle(ulong id, CancellationToken cancellationToken)
    {
        var entrada = await operativaService.ObtenerEntradaPropiaAsync(GetDocumento(), id, cancellationToken);
        return entrada is null ? NotFound() : View(entrada);
    }

    [HttpGet("/Entradas/Transferir/{id:long}")]
    public async Task<IActionResult> Transferir(ulong id, CancellationToken cancellationToken)
    {
        var entrada = await operativaService.ObtenerEntradaPropiaAsync(GetDocumento(), id, cancellationToken);
        return entrada is null ? NotFound() : View(new TransferenciaCrearViewModel { IdEntrada = id });
    }

    private DocumentoUsuario GetDocumento() => new(User.GetTipoDocumento() ?? string.Empty, User.GetPaisDocumento() ?? string.Empty, User.GetNumeroDocumento() ?? string.Empty);
}
