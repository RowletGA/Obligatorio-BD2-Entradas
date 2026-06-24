using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Errors;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize(Roles = RolesAplicacion.Funcionario)]
[Route("Funcionario")]
public sealed class FuncionarioController(IOperativaService operativaService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var asignaciones = await operativaService.ListarAsignacionesFuncionarioAsync(GetDocumento(), cancellationToken);
        var eventos = asignaciones
            .GroupBy(a => a.IdEvento)
            .Select(group =>
            {
                var first = group.OrderBy(a => a.FechaEvento).First();
                return new FuncionarioEventoAsignadoViewModel
                {
                    IdEvento = first.IdEvento,
                    FechaEvento = first.FechaEvento,
                    Evento = string.IsNullOrWhiteSpace(first.Evento) ? $"Evento {first.IdEvento}" : first.Evento,
                    Estadio = first.Estadio,
                    EstadoEvento = first.EstadoEvento,
                    Sectores = group
                        .OrderBy(a => a.Sector)
                        .Select(a => new FuncionarioSectorResumenViewModel
                        {
                            IdSector = a.IdSector,
                            Nombre = a.Sector,
                            EntradasValidadas = a.EntradasValidadas,
                            UltimaValidacion = a.UltimaValidacion
                        })
                        .ToArray()
                };
            })
            .OrderBy(e => e.FechaEvento)
            .ToArray();

        return View(new FuncionarioIndexViewModel
        {
            EventosActivos = eventos.Where(e => e.EstadoEvento is "PROGRAMADO" or "EN_CURSO").ToArray(),
            Historial = eventos.Where(e => e.EstadoEvento is "FINALIZADO" or "CANCELADO").OrderByDescending(e => e.FechaEvento).ToArray()
        });
    }

    [HttpGet("Validar")]
    public IActionResult Validar() => RedirectToAction(nameof(Escanear));

    [HttpGet("Escanear")]
    public IActionResult Escanear()
    {
        SetNoStore();
        var model = new ValidarQrViewModel();
        if (TempData["QrResultado"] is string resultadoJson)
        {
            model.Resultado = JsonSerializer.Deserialize<ValidacionEntradaDto>(resultadoJson);
        }

        if (TempData["QrError"] is string error)
        {
            model.Error = error;
        }

        return View(model);
    }

    [HttpPost("ValidarQr")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("qr-validation")]
    public async Task<IActionResult> ValidarQr(ValidarQrViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["QrError"] = "Token QR inválido.";
            return RedirectToAction(nameof(Escanear));
        }

        try
        {
            var result = await operativaService.ValidarQrAsync(GetDocumento(), model.Token, cancellationToken);
            if (result.Success && result.Value is not null)
            {
                TempData["QrResultado"] = JsonSerializer.Serialize(result.Value);
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["QrError"] = result.Message ?? "QR rechazado.";
            }
        }
        catch (DatabaseException ex)
        {
            TempData["QrError"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Escanear));
    }

    private DocumentoUsuario GetDocumento() => new(User.GetTipoDocumento() ?? string.Empty, User.GetPaisDocumento() ?? string.Empty, User.GetNumeroDocumento() ?? string.Empty);

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
    }
}
