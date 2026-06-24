using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;
using TicketingMundial.Domain.Estados;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Services;

public sealed class AdminService(
    IAdminRepository adminRepository,
    ICatalogoRegistroService catalogoRegistroService) : IAdminService
{
    private const int DefaultPageSize = 10;
    private static readonly string[] EstadosPermitidos =
    [
        EstadoEvento.Programado,
        EstadoEvento.EnCurso,
        EstadoEvento.Finalizado,
        EstadoEvento.Cancelado
    ];

    public Task<AdministradorActualDto?> ObtenerAdministradorAsync(DocumentoUsuario documento, CancellationToken cancellationToken)
    {
        return adminRepository.ObtenerAdministradorAsync(NormalizeDocumento(documento), cancellationToken);
    }

    public async Task<AdminDashboardDto> ObtenerDashboardAsync(DocumentoUsuario adminDocumento, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ObtenerDashboardAsync(admin.PaisSede, cancellationToken);
    }

    public async Task<PagedResult<EstadioAdminDto>> ListarEstadiosAsync(DocumentoUsuario adminDocumento, string? busqueda, int page, int pageSize, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ListarEstadiosAsync(admin.PaisSede, busqueda, ClampPage(page), ClampPageSize(pageSize), cancellationToken);
    }

    public async Task<IReadOnlyList<EstadioAdminDto>> ListarEstadiosParaSeleccionAsync(DocumentoUsuario adminDocumento, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ListarEstadiosParaSeleccionAsync(admin.PaisSede, cancellationToken);
    }

    public async Task<EstadioAdminDto?> ObtenerEstadioAsync(DocumentoUsuario adminDocumento, ulong idEstadio, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ObtenerEstadioAsync(idEstadio, admin.PaisSede, cancellationToken);
    }

    public async Task<OperationResult<ulong>> GuardarEstadioAsync(DocumentoUsuario adminDocumento, EstadioUpsertCommand command, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        var pais = NormalizeCode(command.UbicacionPais);
        if (pais != admin.PaisSede)
        {
            return OperationResult<ulong>.Failure("El estadio debe pertenecer al país sede del administrador.");
        }

        if (!PaisExiste(pais))
        {
            return OperationResult<ulong>.Failure("El país seleccionado no pertenece al catálogo permitido.");
        }

        if (string.IsNullOrWhiteSpace(command.Nombre) || string.IsNullOrWhiteSpace(command.UbicacionLocalidad) || string.IsNullOrWhiteSpace(command.UbicacionCalle))
        {
            return OperationResult<ulong>.Failure("Nombre, localidad y calle son obligatorios.");
        }

        var normalized = command with
        {
            Nombre = command.Nombre.Trim(),
            UbicacionPais = pais,
            UbicacionLocalidad = command.UbicacionLocalidad.Trim(),
            UbicacionCalle = command.UbicacionCalle.Trim(),
            UbicacionNumero = NullIfWhiteSpace(command.UbicacionNumero)
        };

        if (command.IdEstadio.HasValue)
        {
            var updated = await adminRepository.ActualizarEstadioAsync(normalized, admin.PaisSede, cancellationToken);
            return updated
                ? OperationResult<ulong>.Ok(command.IdEstadio.Value, "Estadio actualizado correctamente.")
                : OperationResult<ulong>.Failure("No se encontró el estadio dentro de su jurisdicción.");
        }

        var id = await adminRepository.CrearEstadioAsync(normalized, cancellationToken);
        return OperationResult<ulong>.Ok(id, "Estadio creado correctamente.");
    }

    public async Task<PagedResult<SectorAdminDto>> ListarSectoresAsync(DocumentoUsuario adminDocumento, ulong? idEstadio, string? busqueda, int page, int pageSize, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ListarSectoresAsync(admin.PaisSede, idEstadio, busqueda, ClampPage(page), ClampPageSize(pageSize), cancellationToken);
    }

    public async Task<IReadOnlyList<SectorAdminDto>> ListarSectoresPorEstadioAsync(DocumentoUsuario adminDocumento, ulong idEstadio, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ListarSectoresPorEstadioAsync(idEstadio, admin.PaisSede, cancellationToken);
    }

    public async Task<SectorAdminDto?> ObtenerSectorAsync(DocumentoUsuario adminDocumento, ulong idSector, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ObtenerSectorAsync(idSector, admin.PaisSede, cancellationToken);
    }

    public async Task<OperationResult<ulong>> GuardarSectorAsync(DocumentoUsuario adminDocumento, SectorUpsertCommand command, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        if (command.IdEstadio == 0)
        {
            return OperationResult<ulong>.Failure("Debe seleccionar un estadio.");
        }

        if (string.IsNullOrWhiteSpace(command.NombreSector))
        {
            return OperationResult<ulong>.Failure("El nombre del sector es obligatorio.");
        }

        if (command.Capacidad == 0)
        {
            return OperationResult<ulong>.Failure("La capacidad debe ser mayor a cero.");
        }

        var estadio = await adminRepository.ObtenerEstadioAsync(command.IdEstadio, admin.PaisSede, cancellationToken);
        if (estadio is null)
        {
            return OperationResult<ulong>.Failure("El estadio seleccionado no pertenece a su jurisdicción.");
        }

        if (await adminRepository.ExisteSectorNombreAsync(command.IdEstadio, command.NombreSector.Trim(), command.IdSector, cancellationToken))
        {
            return OperationResult<ulong>.Failure("Ya existe un sector con ese nombre en el estadio.");
        }

        var normalized = command with { NombreSector = command.NombreSector.Trim() };
        if (command.IdSector.HasValue)
        {
            var updated = await adminRepository.ActualizarSectorAsync(normalized, admin.PaisSede, cancellationToken);
            return updated
                ? OperationResult<ulong>.Ok(command.IdSector.Value, "Sector actualizado correctamente.")
                : OperationResult<ulong>.Failure("No se encontró el sector dentro de su jurisdicción.");
        }

        var id = await adminRepository.CrearSectorAsync(normalized, cancellationToken);
        return OperationResult<ulong>.Ok(id, "Sector creado correctamente.");
    }

    public Task<PagedResult<EquipoAdminDto>> ListarEquiposAsync(string? busqueda, int page, int pageSize, CancellationToken cancellationToken)
    {
        return adminRepository.ListarEquiposAsync(busqueda, ClampPage(page), ClampPageSize(pageSize), cancellationToken);
    }

    public Task<IReadOnlyList<EquipoAdminDto>> ListarEquiposParaSeleccionAsync(CancellationToken cancellationToken)
    {
        return adminRepository.ListarEquiposParaSeleccionAsync(cancellationToken);
    }

    public Task<EquipoAdminDto?> ObtenerEquipoAsync(ulong idEquipo, CancellationToken cancellationToken)
    {
        return adminRepository.ObtenerEquipoAsync(idEquipo, cancellationToken);
    }

    public async Task<OperationResult<ulong>> GuardarEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken)
    {
        var pais = NormalizeCode(command.Pais);
        if (!PaisExiste(pais))
        {
            return OperationResult<ulong>.Failure("El país seleccionado no pertenece al catálogo permitido.");
        }

        var normalized = command with
        {
            Pais = pais,
            Grupo = NullIfWhiteSpace(command.Grupo)?.ToUpperInvariant()
        };

        if (command.IdEquipo.HasValue)
        {
            var updated = await adminRepository.ActualizarEquipoAsync(normalized, cancellationToken);
            return updated
                ? OperationResult<ulong>.Ok(command.IdEquipo.Value, "Equipo actualizado correctamente.")
                : OperationResult<ulong>.Failure("No se encontró el equipo.");
        }

        var id = await adminRepository.CrearEquipoAsync(normalized, cancellationToken);
        return OperationResult<ulong>.Ok(id, "Equipo creado correctamente.");
    }

    public async Task<PagedResult<EventoAdminDto>> ListarEventosAsync(DocumentoUsuario adminDocumento, int page, int pageSize, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ListarEventosAsync(admin.PaisSede, ClampPage(page), ClampPageSize(pageSize), cancellationToken);
    }

    public async Task<IReadOnlyList<EventoAdminDto>> ListarEventosAsignablesAsync(DocumentoUsuario adminDocumento, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ListarEventosAsignablesAsync(admin.PaisSede, cancellationToken);
    }

    public async Task<EventoAdminDto?> ObtenerEventoAsync(DocumentoUsuario adminDocumento, ulong idEvento, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        return await adminRepository.ObtenerEventoAsync(idEvento, admin.PaisSede, cancellationToken);
    }

    public async Task<IReadOnlyList<EventoSectorAdminDto>> ListarSectoresHabilitadosEventoAsync(DocumentoUsuario adminDocumento, ulong idEvento, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        var evento = await adminRepository.ObtenerEventoAsync(idEvento, admin.PaisSede, cancellationToken);
        if (evento is null || EstadoEventoTransitions.EsCerrado(evento.EstadoEvento))
        {
            return [];
        }

        return await adminRepository.ListarSectoresHabilitadosEventoAsync(idEvento, admin.PaisSede, cancellationToken);
    }

    public async Task<OperationResult<ulong>> CrearEventoAsync(DocumentoUsuario adminDocumento, EventoCreateCommand command, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        var validation = await ValidateEventoFieldsAsync(
            command.EstadoEvento,
            command.Fecha,
            command.Hora,
            command.IdEstadio,
            command.IdEquipoLocal,
            command.IdEquipoVisitante,
            command.Sectores,
            admin.PaisSede,
            cancellationToken);
        if (!validation.Success)
        {
            return OperationResult<ulong>.Failure(validation.Message ?? "El evento no es válido.");
        }

        var normalized = command with
        {
            EstadoEvento = command.EstadoEvento.Trim().ToUpperInvariant(),
            Sectores = NormalizeSectores(command.Sectores)
        };

        var id = await adminRepository.CrearEventoAsync(normalized, admin.PaisSede, cancellationToken);
        return OperationResult<ulong>.Ok(id, "Evento creado correctamente.");
    }

    public async Task<OperationResult> EditarEventoAsync(DocumentoUsuario adminDocumento, EventoUpdateCommand command, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        var actual = await adminRepository.ObtenerEventoAsync(command.IdEvento, admin.PaisSede, cancellationToken);
        if (actual is null)
        {
            return OperationResult.Failure("No se encontró el evento dentro de su jurisdicción.");
        }

        if (actual.EntradasEmitidas > 0)
        {
            return OperationResult.Failure("Este evento ya tiene entradas emitidas. Para preservar la consistencia de las entradas, solamente puede modificarse su estado.");
        }

        if (EstadoEventoTransitions.EsCerrado(actual.EstadoEvento))
        {
            return OperationResult.Failure("Los eventos finalizados o cancelados no permiten edición estructural.");
        }

        var validation = await ValidateEventoFieldsAsync(
            command.EstadoEvento,
            command.Fecha,
            command.Hora,
            command.IdEstadio,
            command.IdEquipoLocal,
            command.IdEquipoVisitante,
            command.Sectores,
            admin.PaisSede,
            cancellationToken);
        if (!validation.Success)
        {
            return validation;
        }

        var normalized = command with
        {
            EstadoEvento = command.EstadoEvento.Trim().ToUpperInvariant(),
            Sectores = NormalizeSectores(command.Sectores)
        };
        var updated = await adminRepository.ActualizarEventoCompletoAsync(normalized, admin.PaisSede, cancellationToken);
        return updated
            ? OperationResult.Ok("Evento actualizado correctamente.")
            : OperationResult.Failure("No se encontró el evento dentro de su jurisdicción.");
    }

    public async Task<OperationResult<EventoCambioEstadoResultadoDto>> CambiarEstadoEventoAsync(DocumentoUsuario adminDocumento, ulong idEvento, string estado, CancellationToken cancellationToken)
    {
        var admin = await RequireAdminAsync(adminDocumento, cancellationToken);
        var normalized = estado.Trim().ToUpperInvariant();
        if (!EstadosPermitidos.Contains(normalized, StringComparer.Ordinal))
        {
            return OperationResult<EventoCambioEstadoResultadoDto>.Failure("El estado del evento no es válido.");
        }

        var actual = await adminRepository.ObtenerEventoAsync(idEvento, admin.PaisSede, cancellationToken);
        if (actual is null)
        {
            return OperationResult<EventoCambioEstadoResultadoDto>.Failure("No se encontró el evento dentro de su jurisdicción.");
        }

        var rechazo = EstadoEventoTransitions.ObtenerMotivoRechazo(actual.EstadoEvento, normalized);
        if (rechazo is not null)
        {
            return OperationResult<EventoCambioEstadoResultadoDto>.Failure(rechazo);
        }

        var result = await adminRepository.CambiarEstadoEventoAsync(idEvento, normalized, admin.PaisSede, cancellationToken);
        return result is not null
            ? OperationResult<EventoCambioEstadoResultadoDto>.Ok(result, "Estado actualizado correctamente.")
            : OperationResult<EventoCambioEstadoResultadoDto>.Failure("No se encontró el evento dentro de su jurisdicción o la transición ya no es válida.");
    }

    private async Task<AdministradorActualDto> RequireAdminAsync(DocumentoUsuario documento, CancellationToken cancellationToken)
    {
        var admin = await adminRepository.ObtenerAdministradorAsync(NormalizeDocumento(documento), cancellationToken);
        if (admin is null)
        {
            throw new UnauthorizedAccessException("No se encontró un administrador para la identidad autenticada.");
        }

        return admin;
    }

    private bool PaisExiste(string codigo)
    {
        return catalogoRegistroService.ObtenerPaises().Any(pais => pais.Codigo == codigo);
    }

    private static DocumentoUsuario NormalizeDocumento(DocumentoUsuario documento)
    {
        return new DocumentoUsuario(
            NormalizeCode(documento.TipoDocumento),
            NormalizeCode(documento.PaisDocumento),
            documento.NumeroDocumento.Trim());
    }

    private static string NormalizeCode(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<OperationResult> ValidateEventoFieldsAsync(
        string estadoEvento,
        DateOnly fecha,
        TimeOnly hora,
        ulong idEstadio,
        ulong idEquipoLocal,
        ulong idEquipoVisitante,
        IReadOnlyList<EventoSectorCreateCommand> sectoresCommand,
        string paisSede,
        CancellationToken cancellationToken)
    {
        var estado = estadoEvento.Trim().ToUpperInvariant();
        if (!EstadosPermitidos.Contains(estado, StringComparer.Ordinal))
        {
            return OperationResult.Failure("El estado del evento no es válido.");
        }

        if (idEquipoLocal == idEquipoVisitante)
        {
            return OperationResult.Failure("El equipo local y visitante deben ser distintos.");
        }

        var fechaHora = fecha.ToDateTime(hora);
        if (estado.Equals(EstadoEvento.Programado, StringComparison.Ordinal) && fechaHora <= DateTime.Now)
        {
            return OperationResult.Failure("Un evento programado debe tener fecha y hora futura.");
        }

        var estadio = await adminRepository.ObtenerEstadioAsync(idEstadio, paisSede, cancellationToken);
        if (estadio is null)
        {
            return OperationResult.Failure("El estadio seleccionado no pertenece a su jurisdicción.");
        }

        if (await adminRepository.ObtenerEquipoAsync(idEquipoLocal, cancellationToken) is null ||
            await adminRepository.ObtenerEquipoAsync(idEquipoVisitante, cancellationToken) is null)
        {
            return OperationResult.Failure("Debe seleccionar equipos existentes.");
        }

        var sectoresEstadio = await adminRepository.ListarSectoresPorEstadioAsync(idEstadio, paisSede, cancellationToken);
        var idsPermitidos = sectoresEstadio.Select(sector => sector.IdSector).ToHashSet();
        var sectores = NormalizeSectores(sectoresCommand);
        if (sectores.Count == 0)
        {
            return OperationResult.Failure("Debe habilitar al menos un sector.");
        }

        if (sectores.Any(sector => !idsPermitidos.Contains(sector.IdSector)))
        {
            return OperationResult.Failure("Todos los sectores deben pertenecer al estadio seleccionado.");
        }

        if (sectores.Any(sector => sector.PrecioBase <= 0))
        {
            return OperationResult.Failure("El precio por entrada debe ser mayor a cero.");
        }

        return OperationResult.Ok();
    }

    private static IReadOnlyList<EventoSectorCreateCommand> NormalizeSectores(IReadOnlyList<EventoSectorCreateCommand> sectores)
    {
        return sectores
            .Where(sector => sector.IdSector > 0)
            .GroupBy(sector => sector.IdSector)
            .Select(group => group.First())
            .ToArray();
    }

    private static int ClampPage(int page)
    {
        return page < 1 ? 1 : page;
    }

    private static int ClampPageSize(int pageSize)
    {
        return pageSize is < 1 or > 50 ? DefaultPageSize : pageSize;
    }
}
