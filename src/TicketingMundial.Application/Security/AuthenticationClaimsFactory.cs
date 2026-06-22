using System.Security.Claims;
using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Application.Security;

public static class AuthenticationClaimsFactory
{
    public static IReadOnlyList<Claim> CreateClaims(UsuarioAutenticadoDto usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier,
                $"{usuario.Documento.TipoDocumento}|{usuario.Documento.PaisDocumento}|{usuario.Documento.NumeroDocumento}"),
            new(ClaimTypes.Name, $"{usuario.PrimerNombre} {usuario.PrimerApellido}".Trim()),
            new(ClaimTypes.Email, usuario.CorreoElectronico),
            new("TipoDocumento", usuario.Documento.TipoDocumento),
            new("PaisDocumento", usuario.Documento.PaisDocumento),
            new("NumeroDocumento", usuario.Documento.NumeroDocumento)
        };

        claims.AddRange(usuario.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return claims;
    }
}
