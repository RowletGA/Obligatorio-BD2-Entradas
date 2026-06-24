using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.Controllers;
using TicketingMundial.Web.Formatting;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Tests;

public sealed class VisualListingTests
{
    [Fact]
    public void VisualCodeFormatter_NoTruncaMayoresA999()
    {
        Assert.Equal("ENT-001", VisualCodeFormatter.Entrada(1));
        Assert.Equal("VENT-1000", VisualCodeFormatter.Venta(1000));
        Assert.Equal("TRF-1001", VisualCodeFormatter.Transferencia(1001));
    }

    [Fact]
    public void StatusBadgeHelper_UsaColoresEsperados()
    {
        Assert.Contains("success", StatusBadgeHelper.Transferencia("ACEPTADA"), StringComparison.Ordinal);
        Assert.Contains("danger", StatusBadgeHelper.Transferencia("CANCELADA"), StringComparison.Ordinal);
        Assert.Contains("success", StatusBadgeHelper.Venta("paga"), StringComparison.Ordinal);
        Assert.Contains("info", StatusBadgeHelper.Evento("PROGRAMADO"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Funcionario_Index_AgrupaSectoresYSeparaEventosActivosDeHistorial()
    {
        var service = new FakeOperativaService([
            new AsignacionFuncionarioDto { IdEvento = 1, Evento = "URU vs ARG", FechaEvento = new DateTime(2026, 6, 1, 20, 0, 0), EstadoEvento = "PROGRAMADO", Estadio = "Centenario", IdSector = 10, Sector = "Norte", EntradasValidadas = 2 },
            new AsignacionFuncionarioDto { IdEvento = 1, Evento = "URU vs ARG", FechaEvento = new DateTime(2026, 6, 1, 20, 0, 0), EstadoEvento = "PROGRAMADO", Estadio = "Centenario", IdSector = 11, Sector = "Sur", EntradasValidadas = 0 },
            new AsignacionFuncionarioDto { IdEvento = 2, Evento = "BRA vs CHI", FechaEvento = new DateTime(2026, 5, 1, 20, 0, 0), EstadoEvento = "FINALIZADO", Estadio = "Arena", IdSector = 12, Sector = "Este", EntradasValidadas = 4 }
        ]);
        var controller = CreateFuncionarioController(service);

        var result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<FuncionarioIndexViewModel>(view.Model);
        var activo = Assert.Single(model.EventosActivos);
        Assert.Equal(2, activo.Sectores.Count);
        Assert.Equal(2, activo.TotalValidaciones);
        var historico = Assert.Single(model.Historial);
        Assert.Equal("FINALIZADO", historico.EstadoEvento);
    }

    private static FuncionarioController CreateFuncionarioController(IOperativaService service)
    {
        var claims = new[]
        {
            new Claim("TipoDocumento", "CI"),
            new Claim("PaisDocumento", "UY"),
            new Claim("NumeroDocumento", "12345678"),
            new Claim(ClaimTypes.Role, RolesAplicacion.Funcionario)
        };

        return new FuncionarioController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
    }

    private sealed class FakeOperativaService(IReadOnlyList<AsignacionFuncionarioDto> asignaciones) : IOperativaService
    {
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken) => Task.FromResult(asignaciones);

        public Task<OperationResult<CompraPreviewDto>> PreviewCompraAsync(ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult<CompraResultadoDto>> ComprarAsync(DocumentoUsuario comprador, ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CompraListQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CompraDetalleDto?> ObtenerCompraAsync(DocumentoUsuario comprador, ulong idVenta, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, EntradaListQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<EntradaResumenDto?> ObtenerEntradaPropiaAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult<QrEntradaGeneradoDto>> GenerarQrEntradaAsync(DocumentoUsuario propietario, ulong idEntrada, string? generationGrant, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult<ulong>> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<TransferenciaDto>> ListarTransferenciasAsync(DocumentoUsuario usuario, TransferenciaListQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult> AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult<ValidacionEntradaDto>> ValidarQrAsync(DocumentoUsuario funcionario, string token, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteValidacionesFuncionarioDto>> ReporteValidacionesPorFuncionarioAsync(ulong? idEvento, string? funcionario, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteTransferidorDto>> ReporteTransferidoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
