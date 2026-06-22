using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Abstractions.Services;

public interface IAdminService
{
    Task<AdministradorActualDto?> ObtenerAdministradorAsync(DocumentoUsuario documento, CancellationToken cancellationToken);
    Task<AdminDashboardDto> ObtenerDashboardAsync(DocumentoUsuario adminDocumento, CancellationToken cancellationToken);
    Task<PagedResult<EstadioAdminDto>> ListarEstadiosAsync(DocumentoUsuario adminDocumento, string? busqueda, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<EstadioAdminDto>> ListarEstadiosParaSeleccionAsync(DocumentoUsuario adminDocumento, CancellationToken cancellationToken);
    Task<EstadioAdminDto?> ObtenerEstadioAsync(DocumentoUsuario adminDocumento, ulong idEstadio, CancellationToken cancellationToken);
    Task<OperationResult<ulong>> GuardarEstadioAsync(DocumentoUsuario adminDocumento, EstadioUpsertCommand command, CancellationToken cancellationToken);
    Task<PagedResult<SectorAdminDto>> ListarSectoresAsync(DocumentoUsuario adminDocumento, ulong? idEstadio, string? busqueda, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<SectorAdminDto>> ListarSectoresPorEstadioAsync(DocumentoUsuario adminDocumento, ulong idEstadio, CancellationToken cancellationToken);
    Task<SectorAdminDto?> ObtenerSectorAsync(DocumentoUsuario adminDocumento, ulong idSector, CancellationToken cancellationToken);
    Task<OperationResult<ulong>> GuardarSectorAsync(DocumentoUsuario adminDocumento, SectorUpsertCommand command, CancellationToken cancellationToken);
    Task<PagedResult<EquipoAdminDto>> ListarEquiposAsync(string? busqueda, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipoAdminDto>> ListarEquiposParaSeleccionAsync(CancellationToken cancellationToken);
    Task<EquipoAdminDto?> ObtenerEquipoAsync(ulong idEquipo, CancellationToken cancellationToken);
    Task<OperationResult<ulong>> GuardarEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken);
    Task<PagedResult<EventoAdminDto>> ListarEventosAsync(DocumentoUsuario adminDocumento, int page, int pageSize, CancellationToken cancellationToken);
    Task<EventoAdminDto?> ObtenerEventoAsync(DocumentoUsuario adminDocumento, ulong idEvento, CancellationToken cancellationToken);
    Task<OperationResult<ulong>> CrearEventoAsync(DocumentoUsuario adminDocumento, EventoCreateCommand command, CancellationToken cancellationToken);
    Task<OperationResult> CambiarEstadoEventoAsync(DocumentoUsuario adminDocumento, ulong idEvento, string estado, CancellationToken cancellationToken);
}
