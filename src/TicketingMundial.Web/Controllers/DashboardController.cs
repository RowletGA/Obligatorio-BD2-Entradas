using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    public async Task<IActionResult> Index()
    {
        var roles = User.GetRolesReales();
        if (roles.Count == 0)
        {
            return RedirectToAction("Denied", "Account");
        }

        var perfilActivo = User.GetPerfilActivo();
        if (perfilActivo is null || !roles.Contains(perfilActivo))
        {
            if (roles.Count > 1)
            {
                return RedirectToAction("SeleccionarPerfil", "Account");
            }

            perfilActivo = roles[0];
            await EmitirCookieConPerfilActivoAsync(perfilActivo);
        }

        return perfilActivo switch
        {
            RolesAplicacion.Administrador => RedirectToAction(nameof(Administrador)),
            RolesAplicacion.Funcionario => RedirectToAction(nameof(Funcionario)),
            _ => RedirectToAction(nameof(Usuario))
        };
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

    private async Task EmitirCookieConPerfilActivoAsync(string perfilActivo)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var claims = User.Claims
            .Where(claim => claim.Type != PerfilActivoExtensions.ClaimType)
            .Append(new Claim(PerfilActivoExtensions.ClaimType, perfilActivo))
            .ToArray();
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = authenticateResult.Properties ?? new AuthenticationProperties { AllowRefresh = true };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }
}
