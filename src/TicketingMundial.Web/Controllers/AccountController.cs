using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Security;
using TicketingMundial.Application.Validation;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Errors;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;
using AppAuthenticationService = TicketingMundial.Application.Abstractions.Authentication.IAuthenticationService;

namespace TicketingMundial.Web.Controllers;

public sealed class AccountController(
    AppAuthenticationService authenticationService,
    IUsuarioService usuarioService,
    ICatalogoRegistroService catalogoRegistroService) : Controller
{
    private const string PaisDocumentoInicial = "UY";

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var usuario = await authenticationService.AuthenticateAsync(
                model.CorreoElectronico,
                model.Password,
                cancellationToken);

            if (!usuario.Succeeded || usuario.Usuario is null)
            {
                ModelState.AddModelError(string.Empty, usuario.ErrorMessage ?? "Correo o contraseña incorrectos.");
                return View(model);
            }

            var roles = usuario.Usuario.Roles
                .Where(PerfilActivoExtensions.PerfilesOrdenados.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var perfilInicial = roles.Length == 1 ? roles[0] : null;

            await SignInAsync(usuario.Usuario, model.RememberMe, perfilInicial);

            TempData["Success"] = "Inicio de sesión correcto.";

            if (roles.Length > 1)
            {
                return RedirectToAction(nameof(SeleccionarPerfil));
            }

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToPerfilInicial(perfilInicial);
        }
        catch (DatabaseException ex)
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(PrepareRegisterViewModel(new RegisterViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        NormalizeCodes(model);
        ValidateDireccionPais(model);
        ValidateDocumento(model);
        ValidateTelefonos(model);

        if (!ModelState.IsValid)
        {
            return View(PrepareRegisterViewModel(model));
        }

        try
        {
            var command = new RegistroUsuarioCommand
            {
                Documento = new DocumentoUsuario(
                    model.TipoDocumento,
                    model.PaisDocumento,
                    model.NumeroDocumento.Trim()),
                PrimerNombre = model.PrimerNombre.Trim(),
                PrimerApellido = model.PrimerApellido.Trim(),
                CorreoElectronico = model.CorreoElectronico.Trim(),
                DireccionPais = model.DireccionPais,
                DireccionLocalidad = model.DireccionLocalidad,
                DireccionCalle = model.DireccionCalle,
                DireccionNumero = model.DireccionNumero,
                DireccionCodigoPostal = model.DireccionCodigoPostal,
                Telefonos = model.Telefonos,
                Password = model.Password,
                ConfirmPassword = model.ConfirmPassword
            };

            var result = await usuarioService.RegistrarUsuarioGeneralAsync(command, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "No fue posible registrar el usuario.");
                return View(PrepareRegisterViewModel(model));
            }

            TempData["Success"] = "Usuario registrado correctamente. Ya puede iniciar sesión.";
            return RedirectToAction(nameof(Login));
        }
        catch (DatabaseException ex)
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
            return View(PrepareRegisterViewModel(model));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Eventos");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SeleccionarPerfil()
    {
        var roles = User.GetRolesReales();
        if (roles.Count == 0)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        if (roles.Count == 1)
        {
            await EmitirCookieConPerfilActivoAsync(roles[0]);
            return RedirectToPerfilInicial(roles[0]);
        }

        return View(new SeleccionarPerfilViewModel
        {
            PerfilActivo = User.GetPerfilActivoSeguro(),
            Perfiles = roles
                .Select(perfil => new PerfilDisponibleViewModel
                {
                    Codigo = perfil,
                    Nombre = PerfilActivoExtensions.GetNombrePerfil(perfil),
                    Descripcion = PerfilActivoExtensions.GetDescripcionPerfil(perfil)
                })
                .ToArray()
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarPerfil(string perfil)
    {
        var solicitado = (perfil ?? string.Empty).Trim().ToUpperInvariant();
        var roles = User.GetRolesReales();
        if (!roles.Contains(solicitado))
        {
            TempData["Error"] = "No tenés ese perfil asignado.";
            return RedirectToAction(nameof(SeleccionarPerfil));
        }

        await EmitirCookieConPerfilActivoAsync(solicitado);
        TempData["Success"] = $"Perfil activo: {PerfilActivoExtensions.GetNombrePerfil(solicitado)}.";
        return RedirectToPerfilInicial(solicitado);
    }

    [Authorize]
    [HttpGet]
    public IActionResult Profile()
    {
        var model = new ProfileViewModel
        {
            Nombre = User.Identity?.Name ?? "Usuario",
            CorreoElectronico = User.GetCorreoElectronico() ?? string.Empty,
            TipoDocumento = User.GetTipoDocumento(),
            PaisDocumento = User.GetPaisDocumento(),
            NumeroDocumento = User.GetNumeroDocumento(),
            Roles = User.GetRolesReales(),
            PerfilActivo = PerfilActivoExtensions.GetNombrePerfil(User.GetPerfilActivoSeguro() ?? string.Empty),
            EsUsuarioGeneral = User.IsInRole(RolesAplicacion.UsuarioGeneral)
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Denied()
    {
        return View();
    }

    private Task SignInAsync(UsuarioAutenticadoDto usuario, bool rememberMe, string? perfilActivo)
    {
        var claims = AuthenticationClaimsFactory.CreateClaims(usuario).ToList();
        if (!string.IsNullOrWhiteSpace(perfilActivo))
        {
            claims.Add(new Claim(PerfilActivoExtensions.ClaimType, perfilActivo));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(rememberMe ? 12 : 2)
            });
    }

    private IActionResult RedirectToPerfilInicial(string? perfil) => perfil switch
    {
        RolesAplicacion.Funcionario => RedirectToAction("Index", "Funcionario"),
        RolesAplicacion.Administrador => RedirectToAction("Index", "Admin"),
        RolesAplicacion.UsuarioGeneral => RedirectToAction("Index", "Dashboard"),
        _ => RedirectToAction("Index", "Dashboard")
    };

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

    private RegisterViewModel PrepareRegisterViewModel(RegisterViewModel model)
    {
        var paisDocumento = NormalizeCode(model.PaisDocumento);
        if (paisDocumento.Length == 0)
        {
            paisDocumento = PaisDocumentoInicial;
            model.PaisDocumento = paisDocumento;
        }

        var tipos = paisDocumento.Length > 0
            ? catalogoRegistroService.ObtenerTiposDocumentoPermitidos(paisDocumento)
            : catalogoRegistroService.ObtenerTiposDocumento();
        if (tipos.Count == 0)
        {
            tipos = catalogoRegistroService.ObtenerTiposDocumento();
        }
        if (string.IsNullOrWhiteSpace(model.TipoDocumento) && tipos.Count > 0)
        {
            model.TipoDocumento = tipos[0].Codigo;
        }

        if (model.Telefonos.Count == 0)
        {
            model.Telefonos.Add(string.Empty);
        }

        var paises = catalogoRegistroService.ObtenerPaises()
            .Select(pais => new SelectListItem(pais.Nombre, pais.Codigo))
            .ToArray();

        model.PaisesDocumento = paises;
        model.PaisesDireccion = paises;
        model.TiposDocumento = tipos
            .Select(tipo => new SelectListItem(tipo.Nombre, tipo.Codigo))
            .ToArray();
        model.TiposDocumentoCatalogo = catalogoRegistroService.ObtenerTiposDocumento();

        return model;
    }

    private void ValidateTelefonos(RegisterViewModel model)
    {
        var result = TelefonoValidator.ValidateAndNormalize(model.Telefonos);
        if (result.IsValid)
        {
            model.Telefonos = result.TelefonosNormalizados.ToList();
        }
        else
        {
            model.Telefonos = model.Telefonos
                .Select(telefono => telefono?.Trim() ?? string.Empty)
                .ToList();
        }

        if (model.Telefonos.Count == 0)
        {
            model.Telefonos.Add(string.Empty);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(nameof(model.Telefonos), error);
        }
    }

    private void ValidateDireccionPais(RegisterViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DireccionPais))
        {
            return;
        }

        var paisExiste = catalogoRegistroService.ObtenerPaises()
            .Any(pais => pais.Codigo == model.DireccionPais);
        if (!paisExiste)
        {
            ModelState.AddModelError(nameof(model.DireccionPais), "El país de dirección no pertenece al catálogo permitido.");
        }
    }

    private void ValidateDocumento(RegisterViewModel model)
    {
        var result = catalogoRegistroService.ValidarDocumento(new DocumentoUsuario(
            model.TipoDocumento,
            model.PaisDocumento,
            model.NumeroDocumento));

        model.TipoDocumento = result.DocumentoNormalizado.TipoDocumento;
        model.PaisDocumento = result.DocumentoNormalizado.PaisDocumento;
        model.NumeroDocumento = result.DocumentoNormalizado.NumeroDocumento;

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(nameof(model.NumeroDocumento), error);
        }
    }

    private static void NormalizeCodes(RegisterViewModel model)
    {
        model.TipoDocumento = NormalizeCode(model.TipoDocumento);
        model.PaisDocumento = NormalizeCode(model.PaisDocumento);
        model.DireccionPais = string.IsNullOrWhiteSpace(model.DireccionPais)
            ? null
            : NormalizeCode(model.DireccionPais);
    }

    private static string NormalizeCode(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
