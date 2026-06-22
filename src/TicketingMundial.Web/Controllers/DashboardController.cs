using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(RolesAplicacion.Administrador))
        {
            return RedirectToAction(nameof(Administrador));
        }

        if (User.IsInRole(RolesAplicacion.Funcionario))
        {
            return RedirectToAction(nameof(Funcionario));
        }

        return RedirectToAction(nameof(Usuario));
    }

    [Authorize(Roles = RolesAplicacion.UsuarioGeneral)]
    public IActionResult Usuario()
    {
        return View("Index", CreateModel());
    }

    [Authorize(Roles = RolesAplicacion.Funcionario)]
    public IActionResult Funcionario()
    {
        return RedirectToAction("Index", "Funcionario");
    }

    [Authorize(Roles = RolesAplicacion.Administrador)]
    public IActionResult Administrador()
    {
        return RedirectToAction("Index", "Admin");
    }

    private DashboardViewModel CreateModel()
    {
        return new DashboardViewModel
        {
            Nombre = User.Identity?.Name ?? "Usuario",
            CorreoElectronico = User.GetCorreoElectronico() ?? string.Empty,
            Roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            TipoDocumento = User.GetTipoDocumento(),
            PaisDocumento = User.GetPaisDocumento(),
            NumeroDocumento = User.GetNumeroDocumento()
        };
    }
}
