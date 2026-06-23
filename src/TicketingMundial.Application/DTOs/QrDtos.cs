using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.DTOs;

public sealed record QrTokenContext(
    ulong IdEntrada,
    ulong IdEvento,
    DocumentoUsuario Propietario);

public sealed record QrTokenValidationContext(
    ulong IdEntrada,
    ulong IdEvento,
    DocumentoUsuario PropietarioActual);

public sealed class QrTokenGenerado
{
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset VenceUtc { get; init; }
    public int SegundosRestantes { get; init; }
}

public sealed class QrTokenPayload
{
    public ulong IdEntrada { get; init; }
    public ulong IdEvento { get; init; }
    public long VentanaTemporal { get; init; }
    public string MarcaPropietario { get; init; } = string.Empty;
}

public sealed class QrGenerationGrant
{
    public string Grant { get; init; } = string.Empty;
    public DateTimeOffset VenceUtc { get; init; }
}

public sealed class QrGenerationGrantPayload
{
    public ulong IdEntrada { get; init; }
    public ulong IdEvento { get; init; }
    public string MarcaPropietario { get; init; } = string.Empty;
    public DateTimeOffset VenceUtc { get; init; }
}

public sealed class ResultadoValidacionPermisoQr
{
    public bool EsValido { get; init; }
    public string? Motivo { get; init; }
    public QrGenerationGrantPayload? Payload { get; init; }

    public static ResultadoValidacionPermisoQr Valido(QrGenerationGrantPayload payload) => new()
    {
        EsValido = true,
        Payload = payload
    };

    public static ResultadoValidacionPermisoQr Rechazado(string motivo) => new()
    {
        EsValido = false,
        Motivo = motivo
    };
}

public sealed class ResultadoValidacionQr
{
    public bool EsValido { get; init; }
    public string? Motivo { get; init; }
    public QrTokenPayload? Payload { get; init; }

    public static ResultadoValidacionQr Valido(QrTokenPayload payload) => new()
    {
        EsValido = true,
        Payload = payload
    };

    public static ResultadoValidacionQr Rechazado(string motivo, QrTokenPayload? payload = null) => new()
    {
        EsValido = false,
        Motivo = motivo,
        Payload = payload
    };
}

public sealed class EntradaQrDto
{
    public ulong IdEntrada { get; init; }
    public ulong IdEvento { get; init; }
    public DateTime FechaEvento { get; init; }
    public string EstadoEvento { get; init; } = string.Empty;
    public ulong IdSector { get; init; }
    public string Sector { get; init; } = string.Empty;
    public string EstadoEntrada { get; init; } = string.Empty;
    public string Evento { get; init; } = string.Empty;
    public string Estadio { get; init; } = string.Empty;
    public DocumentoUsuario PropietarioActual { get; init; } = new(string.Empty, string.Empty, string.Empty);
}

public sealed class QrEntradaGeneradoDto
{
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset VenceUtc { get; init; }
    public int SegundosRestantes { get; init; }
    public string GenerationGrant { get; init; } = string.Empty;
    public DateTimeOffset GenerationGrantVenceUtc { get; init; }
    public bool ConsultoBase { get; init; }
}
