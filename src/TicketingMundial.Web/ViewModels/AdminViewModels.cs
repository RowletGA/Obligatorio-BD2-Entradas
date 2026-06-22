using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Web.ViewModels;

public sealed class AdminListViewModel<T>
{
    public PagedResult<T> Results { get; init; } = new();
    public string? Busqueda { get; init; }
    public ulong? IdEstadio { get; init; }
    public IReadOnlyList<SelectListItem> Estadios { get; init; } = [];
}

public sealed class AdminDashboardViewModel
{
    public AdminDashboardDto Dashboard { get; init; } = new();
}

public sealed class EstadioFormViewModel
{
    public ulong? IdEstadio { get; set; }

    [Required]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "País")]
    public string UbicacionPais { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string UbicacionLocalidad { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string UbicacionCalle { get; set; } = string.Empty;

    [StringLength(20)]
    public string? UbicacionNumero { get; set; }

    public IReadOnlyList<SelectListItem> Paises { get; set; } = [];
}

public sealed class SectorFormViewModel
{
    public ulong? IdSector { get; set; }

    [Required]
    public ulong IdEstadio { get; set; }

    [Required]
    [StringLength(100)]
    public string NombreSector { get; set; } = string.Empty;

    [Range(1, uint.MaxValue)]
    public uint Capacidad { get; set; }

    public IReadOnlyList<SelectListItem> Estadios { get; set; } = [];
}

public sealed class EquipoFormViewModel
{
    public ulong? IdEquipo { get; set; }

    [Required]
    [StringLength(50)]
    public string Pais { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Grupo { get; set; }

    public IReadOnlyList<SelectListItem> Paises { get; set; } = [];
    public IReadOnlyList<SelectListItem> Grupos { get; set; } = [];
}

public sealed class EventoCreateViewModel
{
    [Required]
    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

    [Required]
    public TimeOnly Hora { get; set; } = new(20, 0);

    [Required]
    public ulong IdEstadio { get; set; }

    [Required]
    public ulong IdEquipoLocal { get; set; }

    [Required]
    public ulong IdEquipoVisitante { get; set; }

    [Required]
    public string EstadoEvento { get; set; } = "PROGRAMADO";

    public List<EventoSectorFormViewModel> Sectores { get; set; } = [];
    public IReadOnlyList<SelectListItem> Estadios { get; set; } = [];
    public IReadOnlyList<SelectListItem> Equipos { get; set; } = [];
    public IReadOnlyList<SelectListItem> Estados { get; set; } = [];
}

public sealed class EventoSectorFormViewModel
{
    public ulong IdSector { get; set; }
    public string NombreSector { get; set; } = string.Empty;
    public bool Seleccionado { get; set; }
    [Range(0, 9999999999)]
    public decimal PrecioBase { get; set; }
}

public sealed class EventoEstadoViewModel
{
    public ulong IdEvento { get; set; }
    public string EstadoEvento { get; set; } = string.Empty;
}
