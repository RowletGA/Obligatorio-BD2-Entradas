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
    public IReadOnlyList<TransferenciaDto> Enviadas { get; init; } = [];
    public IReadOnlyList<TransferenciaDto> Recibidas { get; init; } = [];
}

public sealed class TransferenciasTablaViewModel
{
    public IReadOnlyList<TransferenciaDto> Transferencias { get; init; } = [];
    public bool EsEnviadas { get; init; }
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
}

public sealed class ReportesViewModel
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    [Range(1, 100)]
    public int Limite { get; set; } = 10;
    public IReadOnlyList<ReporteEventoVendidoDto> Eventos { get; set; } = [];
    public IReadOnlyList<ReporteCompradorDto> Compradores { get; set; } = [];
}
