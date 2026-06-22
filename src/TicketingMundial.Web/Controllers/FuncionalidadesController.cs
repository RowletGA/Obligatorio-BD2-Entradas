using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Web.Controllers;

[Authorize]
public sealed class FuncionalidadesController : Controller
{
    [Authorize(Roles = RolesAplicacion.UsuarioGeneral)]
    public IActionResult MisEntradas() => EnDesarrollo(
        "Mis entradas",
        "Consulta de entradas actualmente asignadas, QR dinámico y estado de validación.");

    [Authorize(Roles = RolesAplicacion.UsuarioGeneral)]
    public IActionResult MisCompras() => EnDesarrollo(
        "Mis compras",
        "Historial de ventas y entradas emitidas usando V_DetalleVentas.");

    [Authorize(Roles = RolesAplicacion.UsuarioGeneral)]
    public IActionResult Transferencias() => EnDesarrollo(
        "Transferencias",
        "Solicitudes enviadas, recibidas, aceptación, rechazo e historial.");

    [Authorize(Roles = RolesAplicacion.Funcionario)]
    public IActionResult EventosAsignados() => EnDesarrollo(
        "Eventos asignados",
        "Eventos y sectores vinculados al funcionario autenticado.");

    [Authorize(Roles = RolesAplicacion.Funcionario)]
    public IActionResult ValidarEntrada() => EnDesarrollo(
        "Validar entrada",
        "Ingreso o escaneo de token y registro de validación.");

    [Authorize(Roles = RolesAplicacion.Funcionario)]
    public IActionResult Validaciones() => EnDesarrollo(
        "Validaciones",
        "Auditoría de validaciones realizadas por funcionario.");

    [Authorize(Roles = RolesAplicacion.Administrador)]
    public IActionResult Estadios() => EnDesarrollo("Estadios", "Gestión de estadios existentes.");

    [Authorize(Roles = RolesAplicacion.Administrador)]
    public IActionResult Sectores() => EnDesarrollo("Sectores", "Gestión de sectores por estadio.");

    [Authorize(Roles = RolesAplicacion.Administrador)]
    public IActionResult Equipos() => EnDesarrollo("Equipos", "Gestión y asignación de equipos.");

    [Authorize(Roles = RolesAplicacion.Administrador)]
    public IActionResult Funcionarios() => EnDesarrollo("Funcionarios", "Asignación de funcionarios a evento y sector.");

    [Authorize(Roles = RolesAplicacion.Administrador)]
    public IActionResult Reportes() => EnDesarrollo("Reportes", "Ranking de compradores, ventas y validaciones.");

    private IActionResult EnDesarrollo(string title, string description)
    {
        return View("EnDesarrollo", new PlaceholderViewModel
        {
            Title = title,
            Description = description
        });
    }
}
