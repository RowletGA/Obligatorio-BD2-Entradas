using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Web.Controllers;
using TicketingMundial.Web.ViewModels;

namespace TicketingMundial.Tests;

public sealed class TransferenciasControllerTests
{
    private static readonly DateTime FechaSolicitudPendiente = new(2026, 6, 22, 10, 30, 0);
    private static readonly DateTime FechaSolicitudAceptada = new(2026, 6, 22, 11, 45, 0);
    private static readonly DateTime FechaRespuestaAceptada = new(2026, 6, 22, 12, 15, 0);
    private static readonly DateTime FechaEvento = new(2026, 6, 29, 20, 0, 0);

    [Fact]
    public async Task Index_RetornaVistaConTransferenciaPendienteYAceptada()
    {
        var service = new FakeOperativaService(
            enviadas: [CreateTransferencia("PENDIENTE", FechaSolicitudPendiente, null)],
            recibidas: [CreateTransferencia("ACEPTADA", FechaSolicitudAceptada, FechaRespuestaAceptada)]);
        var controller = CreateController(service);

        var result = await controller.Index(new TransferenciaListQuery(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.StatusCode);
        var model = Assert.IsType<TransferenciasIndexViewModel>(view.Model);
        Assert.Equal(2, model.Results.Items.Count);
    }

    [Fact]
    public void TransferenciaPendiente_PermiteFechaRespuestaNull()
    {
        var transferencia = CreateTransferencia("PENDIENTE", FechaSolicitudPendiente, null);

        Assert.Equal("PENDIENTE", transferencia.Estado);
        Assert.Equal(FechaSolicitudPendiente, transferencia.FechaSolicitud);
        Assert.Null(transferencia.FechaRespuesta);
    }

    [Fact]
    public void TransferenciaAceptada_ConservaFechaRespuesta()
    {
        var transferencia = CreateTransferencia("ACEPTADA", FechaSolicitudAceptada, FechaRespuestaAceptada);

        Assert.Equal("ACEPTADA", transferencia.Estado);
        Assert.Equal(FechaRespuestaAceptada, transferencia.FechaRespuesta);
    }

    [Fact]
    public void Transferencia_ConservaFechaSolicitudYFechaEventoComoDateTime()
    {
        var transferencia = CreateTransferencia("PENDIENTE", FechaSolicitudPendiente, null);

        Assert.Equal(FechaSolicitudPendiente, transferencia.FechaSolicitud);
        Assert.Equal(FechaEvento, transferencia.FechaEvento);
    }

    private static TransferenciasController CreateController(IOperativaService service)
    {
        var claims = new[]
        {
            new Claim("TipoDocumento", "CI"),
            new Claim("PaisDocumento", "UY"),
            new Claim("NumeroDocumento", "12345678"),
            new Claim(ClaimTypes.Role, RolesAplicacion.UsuarioGeneral)
        };

        return new TransferenciasController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }

    private static TransferenciaDto CreateTransferencia(string estado, DateTime fechaSolicitud, DateTime? fechaRespuesta)
    {
        return new TransferenciaDto
        {
            IdTransferencia = estado == "PENDIENTE" ? 1UL : 2UL,
            IdEntrada = 10,
            FechaSolicitud = fechaSolicitud,
            FechaRespuesta = fechaRespuesta,
            Estado = estado,
            UsuarioOtorga = "Ana Demo",
            CorreoOtorga = "ana.demo@example.com",
            UsuarioRecibe = "Bruno Demo",
            CorreoRecibe = "bruno.demo@example.com",
            Evento = "Evento 7",
            FechaEvento = FechaEvento,
            Estadio = "Estadio Demo",
            Sector = "Norte"
        };
    }

    private sealed class FakeOperativaService(
        IReadOnlyList<TransferenciaDto> enviadas,
        IReadOnlyList<TransferenciaDto> recibidas) : IOperativaService
    {
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => Task.FromResult(enviadas);
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => Task.FromResult(recibidas);
        public Task<PagedResult<TransferenciaDto>> ListarTransferenciasAsync(DocumentoUsuario usuario, TransferenciaListQuery query, CancellationToken cancellationToken)
        {
            var items = query.Tipo == "enviadas" ? enviadas : query.Tipo == "recibidas" ? recibidas : enviadas.Concat(recibidas).ToArray();
            return Task.FromResult(new PagedResult<TransferenciaDto> { Items = items, Page = 1, PageSize = 10, TotalItems = items.Count });
        }

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
        public Task<OperationResult> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult> AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OperationResult<ValidacionEntradaDto>> ValidarQrAsync(DocumentoUsuario funcionario, string token, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteValidacionesFuncionarioDto>> ReporteValidacionesPorFuncionarioAsync(ulong? idEvento, string? funcionario, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReporteValidacionesFuncionarioDto>>([]);
        public Task<IReadOnlyList<ReporteTransferidorDto>> ReporteTransferidoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReporteTransferidorDto>>([]);
    }
}
