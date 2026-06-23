using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Abstractions.Security;

public interface IQrTokenService
{
    int MaxTokenLength { get; }

    QrTokenGenerado Generar(QrTokenContext contexto);

    ResultadoValidacionQr LeerPayload(string token);

    ResultadoValidacionQr Validar(string token, QrTokenValidationContext contexto);

    QrGenerationGrant GenerarPermisoGeneracion(QrTokenContext contexto, TimeSpan lifetime);

    ResultadoValidacionPermisoQr ValidarPermisoGeneracion(string grant, DocumentoUsuario propietario);
}
