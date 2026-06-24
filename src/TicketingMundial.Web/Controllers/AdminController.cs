using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Estados;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Errors;
using TicketingMundial.Web.Extensions;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize(Roles = RolesAplicacion.Administrador)]
[Route("Admin")]
public sealed class AdminController(
    IAdminService adminService,
    ICatalogoRegistroService catalogoRegistroService,
    IOperativaService operativaService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await adminService.ObtenerDashboardAsync(GetDocumento(), cancellationToken);
        return View(new AdminDashboardViewModel { Dashboard = dashboard });
    }

    [HttpGet("Estadios")]
    public async Task<IActionResult> Estadios(string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var results = await adminService.ListarEstadiosAsync(GetDocumento(), q, page, 10, cancellationToken);
        return View(new AdminListViewModel<EstadioAdminDto> { Results = results, Busqueda = q });
    }

    [HttpGet("Estadios/Nuevo")]
    public async Task<IActionResult> NuevoEstadio(CancellationToken cancellationToken)
    {
        var admin = await adminService.ObtenerAdministradorAsync(GetDocumento(), cancellationToken);
        return View("EstadioForm", PrepareEstadioForm(new EstadioFormViewModel { UbicacionPais = admin?.PaisSede ?? string.Empty }, admin?.PaisSede));
    }

    [HttpPost("Estadios/Nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoEstadio(EstadioFormViewModel model, CancellationToken cancellationToken)
    {
        return await SaveEstadioAsync(model, cancellationToken);
    }

    [HttpGet("Estadios/{id:long}")]
    public async Task<IActionResult> EstadioDetalle(ulong id, CancellationToken cancellationToken)
    {
        var estadio = await adminService.ObtenerEstadioAsync(GetDocumento(), id, cancellationToken);
        return estadio is null ? NotFound() : View(estadio);
    }

    [HttpGet("Estadios/{id:long}/Editar")]
    public async Task<IActionResult> EditarEstadio(ulong id, CancellationToken cancellationToken)
    {
        var estadio = await adminService.ObtenerEstadioAsync(GetDocumento(), id, cancellationToken);
        if (estadio is null)
        {
            return NotFound();
        }

        return View("EstadioForm", PrepareEstadioForm(new EstadioFormViewModel
        {
            IdEstadio = estadio.IdEstadio,
            Nombre = estadio.Nombre,
            UbicacionPais = estadio.UbicacionPais,
            UbicacionLocalidad = estadio.UbicacionLocalidad,
            UbicacionCalle = estadio.UbicacionCalle,
            UbicacionNumero = estadio.UbicacionNumero
        }, estadio.UbicacionPais));
    }

    [HttpPost("Estadios/{id:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarEstadio(ulong id, EstadioFormViewModel model, CancellationToken cancellationToken)
    {
        model.IdEstadio = id;
        return await SaveEstadioAsync(model, cancellationToken);
    }

    [HttpGet("Sectores")]
    public async Task<IActionResult> Sectores(ulong? idEstadio, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var results = await adminService.ListarSectoresAsync(GetDocumento(), idEstadio, q, page, 10, cancellationToken);
        return View(new AdminListViewModel<SectorAdminDto>
        {
            Results = results,
            Busqueda = q,
            IdEstadio = idEstadio,
            Estadios = await GetEstadiosOptionsAsync(cancellationToken)
        });
    }

    [HttpGet("Sectores/Nuevo")]
    public async Task<IActionResult> NuevoSector(ulong? idEstadio, CancellationToken cancellationToken)
    {
        return View("SectorForm", await PrepareSectorFormAsync(new SectorFormViewModel { IdEstadio = idEstadio ?? 0 }, cancellationToken));
    }

    [HttpPost("Sectores/Nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoSector(SectorFormViewModel model, CancellationToken cancellationToken)
    {
        return await SaveSectorAsync(model, cancellationToken);
    }

    [HttpGet("Sectores/{id:long}/Editar")]
    public async Task<IActionResult> EditarSector(ulong id, CancellationToken cancellationToken)
    {
        var sector = await adminService.ObtenerSectorAsync(GetDocumento(), id, cancellationToken);
        if (sector is null)
        {
            return NotFound();
        }

        return View("SectorForm", await PrepareSectorFormAsync(new SectorFormViewModel
        {
            IdSector = sector.IdSector,
            IdEstadio = sector.IdEstadio,
            NombreSector = sector.NombreSector,
            Capacidad = sector.Capacidad
        }, cancellationToken));
    }

    [HttpPost("Sectores/{id:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarSector(ulong id, SectorFormViewModel model, CancellationToken cancellationToken)
    {
        model.IdSector = id;
        return await SaveSectorAsync(model, cancellationToken);
    }

    [HttpGet("Equipos")]
    public async Task<IActionResult> Equipos(string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var results = await adminService.ListarEquiposAsync(q, page, 10, cancellationToken);
        return View(new AdminListViewModel<EquipoAdminDto> { Results = results, Busqueda = q });
    }

    [HttpGet("Equipos/Nuevo")]
    public IActionResult NuevoEquipo()
    {
        return View("EquipoForm", PrepareEquipoForm(new EquipoFormViewModel()));
    }

    [HttpPost("Equipos/Nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoEquipo(EquipoFormViewModel model, CancellationToken cancellationToken)
    {
        return await SaveEquipoAsync(model, cancellationToken);
    }

    [HttpGet("Equipos/{id:long}/Editar")]
    public async Task<IActionResult> EditarEquipo(ulong id, CancellationToken cancellationToken)
    {
        var equipo = await adminService.ObtenerEquipoAsync(id, cancellationToken);
        if (equipo is null)
        {
            return NotFound();
        }

        return View("EquipoForm", PrepareEquipoForm(new EquipoFormViewModel { IdEquipo = equipo.IdEquipo, Pais = equipo.Pais, Grupo = equipo.Grupo }));
    }

    [HttpPost("Equipos/{id:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarEquipo(ulong id, EquipoFormViewModel model, CancellationToken cancellationToken)
    {
        model.IdEquipo = id;
        return await SaveEquipoAsync(model, cancellationToken);
    }

    [HttpGet("Eventos")]
    public async Task<IActionResult> Eventos(int page = 1, CancellationToken cancellationToken = default)
    {
        var results = await adminService.ListarEventosAsync(GetDocumento(), page, 10, cancellationToken);
        return View(new AdminListViewModel<EventoAdminDto> { Results = results });
    }

    [HttpGet("Eventos/Nuevo")]
    public async Task<IActionResult> NuevoEvento(ulong? idEstadio, CancellationToken cancellationToken)
    {
        var model = await PrepareEventoFormAsync(new EventoCreateViewModel { IdEstadio = idEstadio ?? 0 }, cancellationToken);
        return View("EventoForm", model);
    }

    [HttpPost("Eventos/Nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoEvento(EventoCreateViewModel model, CancellationToken cancellationToken)
    {
        ValidateEventoSectores(model);
        if (!ModelState.IsValid)
        {
            return View("EventoForm", await PrepareEventoFormAsync(model, cancellationToken));
        }

        var command = new EventoCreateCommand
        {
            Fecha = model.Fecha,
            Hora = model.Hora,
            IdEstadio = model.IdEstadio,
            IdEquipoLocal = model.IdEquipoLocal,
            IdEquipoVisitante = model.IdEquipoVisitante,
            EstadoEvento = model.EstadoEvento,
            Sectores = model.Sectores
                .Where(sector => sector.Seleccionado)
                .Select(sector => new EventoSectorCreateCommand { IdSector = sector.IdSector, PrecioBase = sector.PrecioBase!.Value })
                .ToArray()
        };

        try
        {
            var result = await adminService.CrearEventoAsync(GetDocumento(), command, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "No fue posible crear el evento.");
                return View("EventoForm", await PrepareEventoFormAsync(model, cancellationToken));
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(EventoDetalle), new { id = result.Value });
        }
        catch (DatabaseException ex)
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
            return View("EventoForm", await PrepareEventoFormAsync(model, cancellationToken));
        }
    }

    [HttpGet("Eventos/{id:long}")]
    public async Task<IActionResult> EventoDetalle(ulong id, CancellationToken cancellationToken)
    {
        var evento = await adminService.ObtenerEventoAsync(GetDocumento(), id, cancellationToken);
        return evento is null ? NotFound() : View(evento);
    }

    [HttpGet("Eventos/{id:long}/Editar")]
    public async Task<IActionResult> EditarEvento(ulong id, ulong? idEstadio, CancellationToken cancellationToken)
    {
        var evento = await adminService.ObtenerEventoAsync(GetDocumento(), id, cancellationToken);
        if (evento is null)
        {
            return NotFound();
        }

        var model = CreateEventoForm(evento);
        if (idEstadio.HasValue && !model.EdicionBloqueada)
        {
            model.IdEstadio = idEstadio.Value;
            model.Sectores = [];
        }

        return View("EventoForm", await PrepareEventoFormAsync(model, cancellationToken));
    }

    [HttpPost("Eventos/{id:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarEvento(ulong id, EventoCreateViewModel model, CancellationToken cancellationToken)
    {
        model.IdEvento = id;
        ValidateEventoSectores(model);
        if (!ModelState.IsValid)
        {
            return View("EventoForm", await PrepareEventoFormAsync(model, cancellationToken));
        }

        var command = new EventoUpdateCommand
        {
            IdEvento = id,
            Fecha = model.Fecha,
            Hora = model.Hora,
            IdEstadio = model.IdEstadio,
            IdEquipoLocal = model.IdEquipoLocal,
            IdEquipoVisitante = model.IdEquipoVisitante,
            EstadoEvento = model.EstadoEvento,
            Sectores = model.Sectores
                .Where(sector => sector.Seleccionado)
                .Select(sector => new EventoSectorCreateCommand { IdSector = sector.IdSector, PrecioBase = sector.PrecioBase!.Value })
                .ToArray()
        };

        try
        {
            var result = await adminService.EditarEventoAsync(GetDocumento(), command, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "No fue posible actualizar el evento.");
                return View("EventoForm", await PrepareEventoFormAsync(model, cancellationToken));
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(EventoDetalle), new { id });
        }
        catch (DatabaseException ex)
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
            return View("EventoForm", await PrepareEventoFormAsync(model, cancellationToken));
        }
    }

    [HttpPost("Eventos/{id:long}/Estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoEvento(ulong id, EventoEstadoViewModel model, CancellationToken cancellationToken)
    {
        var result = await adminService.CambiarEstadoEventoAsync(GetDocumento(), id, model.EstadoEvento, cancellationToken);
        if (result.Success && result.Value is not null)
        {
            TempData["Success"] = $"{result.Message} Entradas validadas: {result.Value.EntradasValidadas}. Entradas anuladas: {result.Value.EntradasAnuladas}. Transferencias pendientes canceladas: {result.Value.TransferenciasPendientesCanceladas}.";
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(EventoDetalle), new { id });
    }

    [HttpGet("Funcionarios")]
    public async Task<IActionResult> Funcionarios(CancellationToken cancellationToken)
    {
        var admin = await adminService.ObtenerAdministradorAsync(GetDocumento(), cancellationToken);
        ViewBag.Asignaciones = await operativaService.ListarAsignacionesAsync(admin?.PaisSede ?? string.Empty, cancellationToken);
        return View(await PrepareAsignarFuncionarioAsync(new AsignarFuncionarioViewModel(), cancellationToken));
    }

    [HttpGet("Funcionarios/SectoresPorEvento")]
    public async Task<IActionResult> SectoresPorEvento(ulong idEvento, CancellationToken cancellationToken)
    {
        var sectores = await adminService.ListarSectoresHabilitadosEventoAsync(GetDocumento(), idEvento, cancellationToken);
        return Json(sectores.Select(sector => new
        {
            idSector = sector.IdSector,
            nombre = sector.NombreSector,
            capacidad = sector.Capacidad
        }));
    }

    [HttpPost("Funcionarios/Asignar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarFuncionario(AsignarFuncionarioViewModel model, CancellationToken cancellationToken)
    {
        var admin = await adminService.ObtenerAdministradorAsync(GetDocumento(), cancellationToken);
        if (!ModelState.IsValid || admin is null)
        {
            ViewBag.Asignaciones = admin is null ? [] : await operativaService.ListarAsignacionesAsync(admin.PaisSede, cancellationToken);
            return View("Funcionarios", await PrepareAsignarFuncionarioAsync(model, cancellationToken));
        }

        var parts = model.FuncionarioKey.Split('|');
        if (parts.Length != 3)
        {
            TempData["Error"] = "Funcionario inválido.";
            return RedirectToAction(nameof(Funcionarios));
        }

        try
        {
            var result = await operativaService.AsignarFuncionarioAsync(new DocumentoUsuario(parts[0], parts[1], parts[2]), model.IdEvento, model.IdSector, admin.PaisSede, cancellationToken);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
        }
        catch (DatabaseException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Funcionarios));
    }

    private async Task<IActionResult> SaveEstadioAsync(EstadioFormViewModel model, CancellationToken cancellationToken)
    {
        var admin = await adminService.ObtenerAdministradorAsync(GetDocumento(), cancellationToken);
        model.UbicacionPais = admin?.PaisSede ?? model.UbicacionPais;
        if (!ModelState.IsValid)
        {
            return View("EstadioForm", PrepareEstadioForm(model, admin?.PaisSede));
        }

        var result = await adminService.GuardarEstadioAsync(GetDocumento(), new EstadioUpsertCommand
        {
            IdEstadio = model.IdEstadio,
            Nombre = model.Nombre,
            UbicacionPais = model.UbicacionPais,
            UbicacionLocalidad = model.UbicacionLocalidad,
            UbicacionCalle = model.UbicacionCalle,
            UbicacionNumero = model.UbicacionNumero
        }, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "No fue posible guardar el estadio.");
            return View("EstadioForm", PrepareEstadioForm(model, admin?.PaisSede));
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(EstadioDetalle), new { id = result.Value });
    }

    private async Task<IActionResult> SaveSectorAsync(SectorFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("SectorForm", await PrepareSectorFormAsync(model, cancellationToken));
        }

        var result = await adminService.GuardarSectorAsync(GetDocumento(), new SectorUpsertCommand
        {
            IdSector = model.IdSector,
            IdEstadio = model.IdEstadio,
            NombreSector = model.NombreSector,
            Capacidad = model.Capacidad
        }, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "No fue posible guardar el sector.");
            return View("SectorForm", await PrepareSectorFormAsync(model, cancellationToken));
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Sectores), new { idEstadio = model.IdEstadio });
    }

    private async Task<IActionResult> SaveEquipoAsync(EquipoFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("EquipoForm", PrepareEquipoForm(model));
        }

        var result = await adminService.GuardarEquipoAsync(new EquipoUpsertCommand { IdEquipo = model.IdEquipo, Pais = model.Pais, Grupo = model.Grupo }, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "No fue posible guardar el equipo.");
            return View("EquipoForm", PrepareEquipoForm(model));
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Equipos));
    }

    private EstadioFormViewModel PrepareEstadioForm(EstadioFormViewModel model, string? paisSede)
    {
        model.UbicacionPais = paisSede ?? model.UbicacionPais;
        model.Paises = catalogoRegistroService.ObtenerPaises()
            .Where(pais => string.IsNullOrWhiteSpace(paisSede) || pais.Codigo == paisSede)
            .Select(pais => new SelectListItem(pais.Nombre, pais.Codigo))
            .ToArray();
        return model;
    }

    private async Task<SectorFormViewModel> PrepareSectorFormAsync(SectorFormViewModel model, CancellationToken cancellationToken)
    {
        model.Estadios = await GetEstadiosOptionsAsync(cancellationToken);
        return model;
    }

    private EquipoFormViewModel PrepareEquipoForm(EquipoFormViewModel model)
    {
        model.Paises = catalogoRegistroService.ObtenerPaises()
            .Select(pais => new SelectListItem(pais.Nombre, pais.Codigo))
            .ToArray();
        model.Grupos = Enumerable.Range('A', 12)
            .Select(value => ((char)value).ToString())
            .Select(grupo => new SelectListItem(grupo, grupo))
            .ToArray();
        return model;
    }

    private async Task<EventoCreateViewModel> PrepareEventoFormAsync(EventoCreateViewModel model, CancellationToken cancellationToken)
    {
        model.Estadios = await GetEstadiosOptionsAsync(cancellationToken);
        model.Equipos = (await adminService.ListarEquiposParaSeleccionAsync(cancellationToken))
            .Select(equipo => new SelectListItem($"{equipo.Pais}{(string.IsNullOrWhiteSpace(equipo.Grupo) ? string.Empty : $" · Grupo {equipo.Grupo}")}", equipo.IdEquipo.ToString()))
            .ToArray();
        model.Estados = GetEstadoOptions();

        var sectores = model.IdEstadio > 0
            ? await adminService.ListarSectoresPorEstadioAsync(GetDocumento(), model.IdEstadio, cancellationToken)
            : [];
        var previous = model.Sectores.ToDictionary(sector => sector.IdSector);
        model.Sectores = sectores.Select(sector =>
        {
            previous.TryGetValue(sector.IdSector, out var item);
            return new EventoSectorFormViewModel
            {
                IdSector = sector.IdSector,
                NombreSector = sector.NombreSector,
                Capacidad = sector.Capacidad,
                Seleccionado = item?.Seleccionado ?? false,
                PrecioBase = item?.PrecioBase
            };
        }).ToList();
        return model;
    }

    private async Task<AsignarFuncionarioViewModel> PrepareAsignarFuncionarioAsync(AsignarFuncionarioViewModel model, CancellationToken cancellationToken)
    {
        model.Funcionarios = (await operativaService.ListarFuncionariosAsync(cancellationToken))
            .Select(f => new SelectListItem($"{f.Nombre} · {f.NumLegajo}", $"{f.Documento.TipoDocumento}|{f.Documento.PaisDocumento}|{f.Documento.NumeroDocumento}"))
            .ToArray();
        model.Eventos = (await adminService.ListarEventosAsignablesAsync(GetDocumento(), cancellationToken))
            .Select(e => new SelectListItem($"{e.FechaHora:dd/MM/yyyy HH:mm} · {e.EquipoLocal} vs {e.EquipoVisitante} · {e.Estadio} · {e.EstadoEvento}", e.IdEvento.ToString()))
            .ToArray();
        model.Sectores = model.IdEvento > 0
            ? (await adminService.ListarSectoresHabilitadosEventoAsync(GetDocumento(), model.IdEvento, cancellationToken))
                .Select(s => new SelectListItem($"{s.NombreSector} · Capacidad {s.Capacidad}", s.IdSector.ToString()))
                .ToArray()
            : [];
        return model;
    }

    private static EventoCreateViewModel CreateEventoForm(EventoAdminDto evento)
    {
        var bloqueado = evento.EntradasEmitidas > 0 ||
            evento.EstadoEvento is EstadoEvento.Finalizado or EstadoEvento.Cancelado;
        return new EventoCreateViewModel
        {
            IdEvento = evento.IdEvento,
            Fecha = DateOnly.FromDateTime(evento.FechaHora),
            Hora = TimeOnly.FromDateTime(evento.FechaHora),
            IdEstadio = evento.IdEstadio,
            IdEquipoLocal = evento.IdEquipoLocal ?? 0,
            IdEquipoVisitante = evento.IdEquipoVisitante ?? 0,
            EstadoEvento = evento.EstadoEvento,
            EntradasEmitidas = evento.EntradasEmitidas,
            EdicionBloqueada = bloqueado,
            AdvertenciaEdicion = evento.EntradasEmitidas > 0
                ? "Este evento ya tiene entradas emitidas. Para preservar la consistencia de las entradas, solamente puede modificarse su estado."
                : bloqueado
                    ? "Los eventos finalizados o cancelados no permiten edición estructural."
                    : null,
            Sectores = evento.Sectores.Select(sector => new EventoSectorFormViewModel
            {
                IdSector = sector.IdSector,
                NombreSector = sector.NombreSector,
                Capacidad = sector.Capacidad,
                Seleccionado = true,
                PrecioBase = sector.PrecioBase
            }).ToList()
        };
    }

    private async Task<IReadOnlyList<SelectListItem>> GetEstadiosOptionsAsync(CancellationToken cancellationToken)
    {
        return (await adminService.ListarEstadiosParaSeleccionAsync(GetDocumento(), cancellationToken))
            .Select(estadio => new SelectListItem($"{estadio.Nombre} · {estadio.UbicacionLocalidad}", estadio.IdEstadio.ToString()))
            .ToArray();
    }

    private static IReadOnlyList<SelectListItem> GetEstadoOptions()
    {
        return
        [
            new SelectListItem(EstadoEvento.Programado, EstadoEvento.Programado),
            new SelectListItem(EstadoEvento.EnCurso, EstadoEvento.EnCurso),
            new SelectListItem(EstadoEvento.Finalizado, EstadoEvento.Finalizado),
            new SelectListItem(EstadoEvento.Cancelado, EstadoEvento.Cancelado)
        ];
    }

    private void ValidateEventoSectores(EventoCreateViewModel model)
    {
        if (!model.Sectores.Any(sector => sector.Seleccionado))
        {
            ModelState.AddModelError(nameof(model.Sectores), "Debe habilitar al menos un sector.");
            return;
        }

        for (var i = 0; i < model.Sectores.Count; i++)
        {
            var sector = model.Sectores[i];
            if (!sector.Seleccionado)
            {
                ModelState.Remove($"Sectores[{i}].PrecioBase");
                continue;
            }

            if (!sector.PrecioBase.HasValue)
            {
                ModelState.AddModelError($"Sectores[{i}].PrecioBase", "Ingrese el precio por entrada.");
                continue;
            }

            if (sector.PrecioBase <= 0)
            {
                ModelState.AddModelError($"Sectores[{i}].PrecioBase", "El precio por entrada debe ser mayor a cero.");
            }
        }
    }

    private DocumentoUsuario GetDocumento()
    {
        return new DocumentoUsuario(
            User.GetTipoDocumento() ?? string.Empty,
            User.GetPaisDocumento() ?? string.Empty,
            User.GetNumeroDocumento() ?? string.Empty);
    }
}
