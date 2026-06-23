using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Abstractions.Repositories;

public interface IAdminRepository
{
    Task<AdministradorActualDto?> ObtenerAdministradorAsync(DocumentoUsuario documento, CancellationToken cancellationToken);
    Task<AdminDashboardDto> ObtenerDashboardAsync(string paisSede, CancellationToken cancellationToken);
    Task<PagedResult<EstadioAdminDto>> ListarEstadiosAsync(string paisSede, string? busqueda, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<EstadioAdminDto>> ListarEstadiosParaSeleccionAsync(string paisSede, CancellationToken cancellationToken);
    Task<EstadioAdminDto?> ObtenerEstadioAsync(ulong idEstadio, string paisSede, CancellationToken cancellationToken);
    Task<ulong> CrearEstadioAsync(EstadioUpsertCommand command, CancellationToken cancellationToken);
    Task<bool> ActualizarEstadioAsync(EstadioUpsertCommand command, string paisSede, CancellationToken cancellationToken);
    Task<PagedResult<SectorAdminDto>> ListarSectoresAsync(string paisSede, ulong? idEstadio, string? busqueda, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<SectorAdminDto>> ListarSectoresPorEstadioAsync(ulong idEstadio, string paisSede, CancellationToken cancellationToken);
    Task<SectorAdminDto?> ObtenerSectorAsync(ulong idSector, string paisSede, CancellationToken cancellationToken);
    Task<ulong> CrearSectorAsync(SectorUpsertCommand command, CancellationToken cancellationToken);
    Task<bool> ActualizarSectorAsync(SectorUpsertCommand command, string paisSede, CancellationToken cancellationToken);
    Task<bool> ExisteSectorNombreAsync(ulong idEstadio, string nombreSector, ulong? excludingId, CancellationToken cancellationToken);
    Task<PagedResult<EquipoAdminDto>> ListarEquiposAsync(string? busqueda, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipoAdminDto>> ListarEquiposParaSeleccionAsync(CancellationToken cancellationToken);
    Task<EquipoAdminDto?> ObtenerEquipoAsync(ulong idEquipo, CancellationToken cancellationToken);
    Task<ulong> CrearEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken);
    Task<bool> ActualizarEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken);
    Task<PagedResult<EventoAdminDto>> ListarEventosAsync(string paisSede, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<EventoAdminDto>> ListarEventosAsignablesAsync(string paisSede, CancellationToken cancellationToken);
    Task<EventoAdminDto?> ObtenerEventoAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken);
    Task<IReadOnlyList<EventoSectorAdminDto>> ListarSectoresHabilitadosEventoAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken);
    Task<ulong> CrearEventoAsync(EventoCreateCommand command, string paisSede, CancellationToken cancellationToken);
    Task<bool> ActualizarEventoCompletoAsync(EventoUpdateCommand command, string paisSede, CancellationToken cancellationToken);
    Task<bool> CambiarEstadoEventoAsync(ulong idEvento, string estado, string paisSede, CancellationToken cancellationToken);
}
