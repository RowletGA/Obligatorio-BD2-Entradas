using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.DTOs;

public sealed record CompraSectorCantidad(ulong IdSector, int Cantidad);

public sealed class CompraLineaDto
{
    public ulong IdSector { get; init; }
    public string Sector { get; init; } = string.Empty;
    public int Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Subtotal => PrecioUnitario * Cantidad;
    public long Disponibles { get; init; }
}

public sealed class CompraPreviewDto
{
    public ulong IdEvento { get; init; }
    public string Evento { get; init; } = string.Empty;
    public DateTime FechaHora { get; init; }
    public string Estadio { get; init; } = string.Empty;
    public decimal PorcentajeComision { get; init; } = 5m;
    public IReadOnlyList<CompraLineaDto> Lineas { get; init; } = [];
    public decimal Subtotal => Lineas.Sum(linea => linea.Subtotal);
    public decimal Total => Math.Round(Subtotal * (1 + PorcentajeComision / 100m), 2);
}

public sealed class CompraResultadoDto
{
    public ulong IdVenta { get; init; }
    public decimal MontoTotal { get; init; }
}

public sealed class CompraResumenDto
{
    public ulong IdVenta { get; init; }
    public DateTime FechaVenta { get; init; }
    public string EstadoVenta { get; init; } = string.Empty;
    public decimal MontoTotal { get; init; }
    public decimal PorcentajeComision { get; init; }
    public int CantidadEntradas { get; init; }
    public string Eventos { get; init; } = string.Empty;
}

public sealed class CompraDetalleDto
{
    public CompraResumenDto Venta { get; init; } = new();
    public IReadOnlyList<EntradaResumenDto> Entradas { get; init; } = [];
}

public sealed class EntradaResumenDto
{
    public ulong IdEntrada { get; init; }
    public ulong IdEvento { get; init; }
    public DateTime FechaEvento { get; init; }
    public string Evento { get; init; } = string.Empty;
    public string Estadio { get; init; } = string.Empty;
    public ulong IdSector { get; init; }
    public string Sector { get; init; } = string.Empty;
    public string EstadoEntrada { get; init; } = string.Empty;
    public decimal Costo { get; init; }
    public int TransferenciasAceptadas { get; init; }
}

public sealed class TransferenciaDto
{
    public ulong IdTransferencia { get; init; }
    public ulong IdEntrada { get; init; }
    public DateTime FechaSolicitud { get; init; }
    public DateTime? FechaRespuesta { get; init; }
    public string Estado { get; init; } = string.Empty;
    public string UsuarioOtorga { get; init; } = string.Empty;
    public string CorreoOtorga { get; init; } = string.Empty;
    public string UsuarioRecibe { get; init; } = string.Empty;
    public string CorreoRecibe { get; init; } = string.Empty;
    public string Evento { get; init; } = string.Empty;
    public DateTime FechaEvento { get; init; }
    public string Estadio { get; init; } = string.Empty;
    public string Sector { get; init; } = string.Empty;
}

public sealed class UsuarioDestinoDto
{
    public DocumentoUsuario Documento { get; init; } = new(string.Empty, string.Empty, string.Empty);
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
}

public sealed class FuncionarioDto
{
    public DocumentoUsuario Documento { get; init; } = new(string.Empty, string.Empty, string.Empty);
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string NumLegajo { get; init; } = string.Empty;
}

public sealed class AsignacionFuncionarioDto
{
    public DocumentoUsuario Documento { get; init; } = new(string.Empty, string.Empty, string.Empty);
    public string Funcionario { get; init; } = string.Empty;
    public string NumLegajo { get; init; } = string.Empty;
    public ulong IdEvento { get; init; }
    public DateTime FechaEvento { get; init; }
    public string EstadoEvento { get; init; } = string.Empty;
    public ulong IdSector { get; init; }
    public string Sector { get; init; } = string.Empty;
    public string Estadio { get; init; } = string.Empty;
    public int EntradasValidadas { get; init; }
}

public sealed class ValidacionEntradaDto
{
    public ulong IdEntrada { get; init; }
    public string EstadoEntrada { get; init; } = string.Empty;
    public string ResultadoValidacion { get; init; } = string.Empty;
    public ulong IdEvento { get; init; }
    public DateTime FechaEvento { get; init; }
    public ulong IdSector { get; init; }
    public string Sector { get; init; } = string.Empty;
    public string Estadio { get; init; } = string.Empty;
    public string Evento { get; init; } = string.Empty;
    public DateTime? FechaHoraValidacion { get; init; }
    public string FuncionarioValidador { get; init; } = string.Empty;
}

public sealed class ReporteEventoVendidoDto
{
    public ulong IdEvento { get; init; }
    public DateTime FechaHora { get; init; }
    public string Estadio { get; init; } = string.Empty;
    public string Evento { get; init; } = string.Empty;
    public int EntradasVendidas { get; init; }
}

public sealed class ReporteCompradorDto
{
    public string Comprador { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public int EntradasCompradas { get; init; }
    public decimal TotalGastado { get; init; }
}
