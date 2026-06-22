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
    ICatalogoRegistroService catalogoRegistroService) : Controller
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
                .Select(sector => new EventoSectorCreateCommand { IdSector = sector.IdSector, PrecioBase = sector.PrecioBase })
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

    [HttpPost("Eventos/{id:long}/Estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoEvento(ulong id, EventoEstadoViewModel model, CancellationToken cancellationToken)
    {
        var result = await adminService.CambiarEstadoEventoAsync(GetDocumento(), id, model.EstadoEvento, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(EventoDetalle), new { id });
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
                Seleccionado = item?.Seleccionado ?? false,
                PrecioBase = item?.PrecioBase ?? 0
            };
        }).ToList();
        return model;
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

    private DocumentoUsuario GetDocumento()
    {
        return new DocumentoUsuario(
            User.GetTipoDocumento() ?? string.Empty,
            User.GetPaisDocumento() ?? string.Empty,
            User.GetNumeroDocumento() ?? string.Empty);
    }
}
