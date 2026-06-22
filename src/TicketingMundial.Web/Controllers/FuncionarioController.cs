using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public IActionResult Validar() => View(new ValidarEntradaViewModel());

    [HttpPost("Validar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Validar(ValidarEntradaViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await operativaService.ValidarEntradaAsync(GetDocumento(), model.IdEntrada, model.Token ?? string.Empty, cancellationToken);
            if (result.Success)
            {
                model.Resultado = result.Value;
                TempData["Success"] = result.Message;
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "No fue posible validar la entrada.");
            }
        }
        catch (DatabaseException ex)
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
        }

        return View(model);
    }

    private DocumentoUsuario GetDocumento() => new(User.GetTipoDocumento() ?? string.Empty, User.GetPaisDocumento() ?? string.Empty, User.GetNumeroDocumento() ?? string.Empty);
}
