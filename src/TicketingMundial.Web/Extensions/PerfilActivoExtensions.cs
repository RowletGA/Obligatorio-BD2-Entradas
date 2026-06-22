using System.Security.Claims;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Web.Extensions;

public static class PerfilActivoExtensions
{
    public const string ClaimType = "PerfilActivo";

    public static readonly IReadOnlyList<string> PerfilesOrdenados =
    [
        RolesAplicacion.UsuarioGeneral,
        RolesAplicacion.Administrador,
        RolesAplicacion.Funcionario
    ];

    public static IReadOnlyList<string> GetRolesReales(this ClaimsPrincipal principal)
    {
        return PerfilesOrdenados
            .Where(principal.IsInRole)
            .ToArray();
    }

    public static string? GetPerfilActivo(this ClaimsPrincipal principal)
    {
        var perfil = principal.FindFirstValue(ClaimType);
        return PerfilesOrdenados.Contains(perfil) ? perfil : null;
    }

    public static string? GetPerfilActivoSeguro(this ClaimsPrincipal principal)
    {
        var roles = principal.GetRolesReales();
        var perfil = principal.GetPerfilActivo();
        if (perfil is not null && roles.Contains(perfil))
        {
            return perfil;
        }

        return roles.Count == 1 ? roles[0] : null;
    }

    public static bool TieneMultiplesPerfiles(this ClaimsPrincipal principal)
    {
        return principal.GetRolesReales().Count > 1;
    }

    public static string GetNombrePerfil(string perfil)
    {
        return perfil switch
        {
            RolesAplicacion.UsuarioGeneral => "Usuario General",
            RolesAplicacion.Administrador => "Administrador",
            RolesAplicacion.Funcionario => "Funcionario",
            _ => perfil
        };
    }

    public static string GetDescripcionPerfil(string perfil)
    {
        return perfil switch
        {
            RolesAplicacion.UsuarioGeneral => "Consultá eventos, comprá entradas y administrá tus transferencias.",
            RolesAplicacion.Administrador => "Gestioná estadios, sectores, eventos, funcionarios y reportes.",
            RolesAplicacion.Funcionario => "Consultá asignaciones y validá el ingreso de entradas.",
            _ => string.Empty
        };
    }
}
