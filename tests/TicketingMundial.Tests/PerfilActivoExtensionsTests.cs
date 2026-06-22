using System.Security.Claims;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.Extensions;

namespace TicketingMundial.Tests;

public sealed class PerfilActivoExtensionsTests
{
    [Fact]
    public void GetPerfilActivoSeguro_RetornaUnicoRol_SiNoExisteClaim()
    {
        var principal = CreatePrincipal([RolesAplicacion.UsuarioGeneral]);

        var perfil = principal.GetPerfilActivoSeguro();

        Assert.Equal(RolesAplicacion.UsuarioGeneral, perfil);
    }

    [Fact]
    public void GetPerfilActivoSeguro_RetornaNull_SiMultiplesRolesNoTienenClaim()
    {
        var principal = CreatePrincipal([RolesAplicacion.UsuarioGeneral, RolesAplicacion.Administrador]);

        var perfil = principal.GetPerfilActivoSeguro();

        Assert.Null(perfil);
    }

    [Fact]
    public void GetPerfilActivoSeguro_RechazaClaimManipulado()
    {
        var principal = CreatePrincipal([RolesAplicacion.UsuarioGeneral], RolesAplicacion.Administrador);

        var perfil = principal.GetPerfilActivoSeguro();

        Assert.Equal(RolesAplicacion.UsuarioGeneral, perfil);
    }

    [Fact]
    public void GetRolesReales_ConservaTodosLosRolesPermitidos()
    {
        var principal = CreatePrincipal([
            RolesAplicacion.UsuarioGeneral,
            RolesAplicacion.Administrador,
            RolesAplicacion.Funcionario
        ], RolesAplicacion.Funcionario);

        var roles = principal.GetRolesReales();

        Assert.Equal([
            RolesAplicacion.UsuarioGeneral,
            RolesAplicacion.Administrador,
            RolesAplicacion.Funcionario
        ], roles);
    }

    private static ClaimsPrincipal CreatePrincipal(IReadOnlyList<string> roles, string? perfilActivo = null)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
        if (perfilActivo is not null)
        {
            claims.Add(new Claim(PerfilActivoExtensions.ClaimType, perfilActivo));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
