using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Web.ViewModels;

public sealed class CompraFormViewModel
{
    public ulong IdEvento { get; set; }
    public List<CompraSectorFormViewModel> Sectores { get; set; } = [];
}

public sealed class CompraSectorFormViewModel
{
    public ulong IdSector { get; set; }
    public string NombreSector { get; set; } = string.Empty;
    public long Disponibles { get; set; }
    [Range(0, 5)]
    public int Cantidad { get; set; }
}

public sealed class TransferenciaCrearViewModel
{
    public ulong IdEntrada { get; set; }
    [Required]
    [EmailAddress]
    public string CorreoDestino { get; set; } = string.Empty;
}

public sealed class TransferenciasIndexViewModel
{
    public PagedResult<TransferenciaDto> Results { get; init; } = new();
    public TransferenciaListQuery Query { get; init; } = new();
}

public sealed class TransferenciasTablaViewModel
{
    public IReadOnlyList<TransferenciaDto> Transferencias { get; init; } = [];
}

public sealed class ComprasIndexViewModel
{
    public PagedResult<CompraResumenDto> Results { get; init; } = new();
    public CompraListQuery Query { get; init; } = new();
}

public sealed class EntradasIndexViewModel
{
    public PagedResult<EntradaResumenDto> Results { get; init; } = new();
    public EntradaListQuery Query { get; init; } = new();
}

public sealed class FuncionarioIndexViewModel
{
    public IReadOnlyList<FuncionarioEventoAsignadoViewModel> EventosActivos { get; init; } = [];
    public IReadOnlyList<FuncionarioEventoAsignadoViewModel> Historial { get; init; } = [];
}

public sealed class FuncionarioEventoAsignadoViewModel
{
    public ulong IdEvento { get; init; }
    public DateTime FechaEvento { get; init; }
    public string Evento { get; init; } = string.Empty;
    public string Estadio { get; init; } = string.Empty;
    public string EstadoEvento { get; init; } = string.Empty;
    public IReadOnlyList<FuncionarioSectorResumenViewModel> Sectores { get; init; } = [];
    public int TotalValidaciones => Sectores.Sum(sector => sector.EntradasValidadas);
    public DateTime? UltimaValidacion => Sectores.Where(sector => sector.UltimaValidacion.HasValue).Max(sector => sector.UltimaValidacion);
}

public sealed class FuncionarioSectorResumenViewModel
{
    public ulong IdSector { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public int EntradasValidadas { get; init; }
    public DateTime? UltimaValidacion { get; init; }
}

public sealed class AsignarFuncionarioViewModel
{
    [Required]
    public string FuncionarioKey { get; set; } = string.Empty;
    [Required]
    public ulong IdEvento { get; set; }
    [Required]
    public ulong IdSector { get; set; }
    public IReadOnlyList<SelectListItem> Funcionarios { get; set; } = [];
    public IReadOnlyList<SelectListItem> Eventos { get; set; } = [];
    public IReadOnlyList<SelectListItem> Sectores { get; set; } = [];
}

public sealed class ValidarQrViewModel
{
    [Required]
    [StringLength(255)]
    public string Token { get; set; } = string.Empty;
    public ValidacionEntradaDto? Resultado { get; set; }
    public string? Error { get; set; }
}

public sealed class ReportesViewModel
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public ulong? IdEvento { get; set; }
    public string? Funcionario { get; set; }
    [Range(1, 100)]
    public int Limite { get; set; } = 10;
    public IReadOnlyList<ReporteEventoVendidoDto> Eventos { get; set; } = [];
    public IReadOnlyList<ReporteCompradorDto> Compradores { get; set; } = [];
    public IReadOnlyList<ReporteValidacionesFuncionarioDto> ValidacionesPorFuncionario { get; set; } = [];
    public IReadOnlyList<ReporteTransferidorDto> Transferidores { get; set; } = [];
}
