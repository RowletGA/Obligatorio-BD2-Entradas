using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingMundial.Application.Abstractions.Services;
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
        return View(await operativaService.ListarAsignacionesFuncionarioAsync(GetDocumento(), cancellationToken));
    }

    [HttpGet("Validar")]
    public IActionResult Validar() => RedirectToAction(nameof(Escanear));

    [HttpGet("Escanear")]
    public IActionResult Escanear() => View(new ValidarQrViewModel());

    [HttpPost("ValidarQr")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("qr-validation")]
    public async Task<IActionResult> ValidarQr(ValidarQrViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Token QR inválido.");
            return View("Escanear", model);
        }

        try
        {
            var result = await operativaService.ValidarQrAsync(GetDocumento(), model.Token, cancellationToken);
            if (result.Success)
            {
                model.Resultado = result.Value;
                TempData["Success"] = result.Message;
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "QR rechazado.");
            }
        }
        catch (DatabaseException ex)
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
        }

        model.Token = string.Empty;
        return View("Escanear", model);
    }

    private DocumentoUsuario GetDocumento() => new(User.GetTipoDocumento() ?? string.Empty, User.GetPaisDocumento() ?? string.Empty, User.GetNumeroDocumento() ?? string.Empty);
}
