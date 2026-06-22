using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.DTOs;

public sealed class AdministradorActualDto
{
    public DocumentoUsuario Documento { get; init; } = new(string.Empty, string.Empty, string.Empty);
    public DateOnly FechaAsignacion { get; init; }
    public string PaisSede { get; init; } = string.Empty;
}

public sealed class AdminDashboardDto
{
    public string PaisSede { get; init; } = string.Empty;
    public int Estadios { get; init; }
    public int Sectores { get; init; }
    public int Equipos { get; init; }
    public int Eventos { get; init; }
    public int EventosSinSectores { get; init; }
    public IReadOnlyList<EventoAdminDto> ProximosEventos { get; init; } = [];
}

public sealed class EstadioAdminDto
{
    public ulong IdEstadio { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string UbicacionPais { get; init; } = string.Empty;
    public string UbicacionLocalidad { get; init; } = string.Empty;
    public string UbicacionCalle { get; init; } = string.Empty;
    public string? UbicacionNumero { get; init; }
    public int Sectores { get; init; }
    public int Eventos { get; init; }
}

public sealed class SectorAdminDto
{
    public ulong IdSector { get; init; }
    public string NombreSector { get; init; } = string.Empty;
    public uint Capacidad { get; init; }
    public ulong IdEstadio { get; init; }
    public string Estadio { get; init; } = string.Empty;
    public string PaisEstadio { get; init; } = string.Empty;
}

public sealed class EquipoAdminDto
{
    public ulong IdEquipo { get; init; }
    public string Pais { get; init; } = string.Empty;
    public string? Grupo { get; init; }
}

public sealed class EventoAdminDto
{
    public ulong IdEvento { get; init; }
    public DateTime FechaHora { get; init; }
    public string EstadoEvento { get; init; } = string.Empty;
    public ulong IdEstadio { get; init; }
    public string Estadio { get; init; } = string.Empty;
    public string PaisEstadio { get; init; } = string.Empty;
    public ulong? IdEquipoLocal { get; init; }
    public string? EquipoLocal { get; init; }
    public ulong? IdEquipoVisitante { get; init; }
    public string? EquipoVisitante { get; init; }
    public IReadOnlyList<EventoSectorAdminDto> Sectores { get; init; } = [];
}

public sealed class EventoSectorAdminDto
{
    public ulong IdSector { get; init; }
    public string NombreSector { get; init; } = string.Empty;
    public decimal PrecioBase { get; init; }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public sealed record EstadioUpsertCommand
{
    public ulong? IdEstadio { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string UbicacionPais { get; init; } = string.Empty;
    public string UbicacionLocalidad { get; init; } = string.Empty;
    public string UbicacionCalle { get; init; } = string.Empty;
    public string? UbicacionNumero { get; init; }
}

public sealed record SectorUpsertCommand
{
    public ulong? IdSector { get; init; }
    public ulong IdEstadio { get; init; }
    public string NombreSector { get; init; } = string.Empty;
    public uint Capacidad { get; init; }
}

public sealed record EquipoUpsertCommand
{
    public ulong? IdEquipo { get; init; }
    public string Pais { get; init; } = string.Empty;
    public string? Grupo { get; init; }
}

public sealed record EventoCreateCommand
{
    public DateOnly Fecha { get; init; }
    public TimeOnly Hora { get; init; }
    public ulong IdEstadio { get; init; }
    public ulong IdEquipoLocal { get; init; }
    public ulong IdEquipoVisitante { get; init; }
    public string EstadoEvento { get; init; } = "PROGRAMADO";
    public IReadOnlyList<EventoSectorCreateCommand> Sectores { get; init; } = [];
}

public sealed record EventoSectorCreateCommand
{
    public ulong IdSector { get; init; }
    public decimal PrecioBase { get; init; }
}
