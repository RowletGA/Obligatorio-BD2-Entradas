using System.Security.Claims;

namespace TicketingMundial.Web.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetTipoDocumento(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("TipoDocumento");
    }

    public static string? GetPaisDocumento(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("PaisDocumento");
    }

    public static string? GetNumeroDocumento(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("NumeroDocumento");
    }

    public static string? GetCorreoElectronico(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email);
    }
}
