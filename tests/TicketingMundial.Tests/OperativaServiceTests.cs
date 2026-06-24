using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.Abstractions.Security;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Services;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Tests;

public sealed class OperativaServiceTests
{
    private static readonly DocumentoUsuario Usuario = new("CI", "UY", "12345678");

    [Fact]
    public async Task ComprarAsync_RechazaSeisEntradas()
    {
        var service = new OperativaService(new FakeOperativaRepository(), new FakeQrTokenService());

        var result = await service.ComprarAsync(Usuario, 1, [new CompraSectorCantidad(10, 6)], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("cinco", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComprarAsync_RechazaCantidadCero()
    {
        var service = new OperativaService(new FakeOperativaRepository(), new FakeQrTokenService());

        var result = await service.ComprarAsync(Usuario, 1, [new CompraSectorCantidad(10, 0)], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("al menos", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComprarAsync_NormalizaCantidadesYUsaRepositorio()
    {
        var repository = new FakeOperativaRepository();
        var service = new OperativaService(repository, new FakeQrTokenService());

        var result = await service.ComprarAsync(Usuario, 1, [new CompraSectorCantidad(10, 2), new CompraSectorCantidad(10, 1)], CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, Assert.Single(repository.UltimasCantidades).Cantidad);
    }

    [Fact]
    public async Task ResponderTransferenciaAsync_RechazaEstadoInvalido()
    {
        var service = new OperativaService(new FakeOperativaRepository(), new FakeQrTokenService());

        var result = await service.ResponderTransferenciaAsync(Usuario, 1, "DROP", true, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GenerarQrEntradaAsync_PermiteEntradaActiva()
    {
        var service = new OperativaService(new FakeOperativaRepository(), new FakeQrTokenService());

        var result = await service.GenerarQrEntradaAsync(Usuario, 10, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("token-demo", result.Value?.Token);
    }

    [Fact]
    public async Task GenerarQrEntradaAsync_RechazaEntradaValidada()
    {
        var repository = new FakeOperativaRepository
        {
            EntradaQr = new EntradaQrDto
            {
                IdEntrada = 10,
                IdEvento = 7,
                EstadoEntrada = "VALIDADA",
                EstadoEvento = "PROGRAMADO",
                PropietarioActual = Usuario
            }
        };
        var service = new OperativaService(repository, new FakeQrTokenService());

        var result = await service.GenerarQrEntradaAsync(Usuario, 10, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("validada", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerarQrEntradaAsync_RechazaEventoCancelado()
    {
        var repository = new FakeOperativaRepository
        {
            EntradaQr = new EntradaQrDto
            {
                IdEntrada = 10,
                IdEvento = 7,
                EstadoEntrada = "ACTIVA",
                EstadoEvento = "CANCELADO",
                PropietarioActual = Usuario
            }
        };
        var service = new OperativaService(repository, new FakeQrTokenService());

        var result = await service.GenerarQrEntradaAsync(Usuario, 10, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("cancelado", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reportes_ClampLimite()
    {
        var repository = new FakeOperativaRepository();
        var service = new OperativaService(repository, new FakeQrTokenService());

        await service.ReporteCompradoresAsync(null, null, 500, CancellationToken.None);

        Assert.Equal(10, repository.UltimoLimiteReporte);
    }

    [Fact]
    public async Task AsignarFuncionarioAsync_RechazaEventoFinalizadoManipulado()
    {
        var repository = new FakeOperativaRepository { EventoAsignable = null };
        var service = new OperativaService(repository, new FakeQrTokenService());

        var result = await service.AsignarFuncionarioAsync(Usuario, 7, 2, "UY", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no admite", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsignarFuncionarioAsync_RechazaSectorNoHabilitado()
    {
        var repository = new FakeOperativaRepository { SectorHabilitado = false };
        var service = new OperativaService(repository, new FakeQrTokenService());

        var result = await service.AsignarFuncionarioAsync(Usuario, 7, 99, "UY", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("habilitado", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsignarFuncionarioAsync_RechazaAsignacionDuplicada()
    {
        var repository = new FakeOperativaRepository { AsignacionDuplicada = true };
        var service = new OperativaService(repository, new FakeQrTokenService());

        var result = await service.AsignarFuncionarioAsync(Usuario, 7, 2, "UY", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ya se encuentra asignado", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AsignarFuncionarioAsync_InsertaAsignacionValida()
    {
        var repository = new FakeOperativaRepository();
        var service = new OperativaService(repository, new FakeQrTokenService());

        var result = await service.AsignarFuncionarioAsync(Usuario, 7, 2, "UY", CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(repository.AsignacionInsertada);
    }

    private sealed class FakeOperativaRepository : IOperativaRepository
    {
        public IReadOnlyList<CompraSectorCantidad> UltimasCantidades { get; private set; } = [];
        public int UltimoLimiteReporte { get; private set; }
        public EventoAdminDto? EventoAsignable { get; init; } = new()
        {
            IdEvento = 7,
            EstadoEvento = "PROGRAMADO",
            PaisEstadio = "UY",
            IdEstadio = 1,
            FechaHora = DateTime.Today.AddDays(2)
        };
        public bool SectorHabilitado { get; init; } = true;
        public bool AsignacionDuplicada { get; init; }
        public bool AsignacionInsertada { get; private set; }
        public EntradaQrDto? EntradaQr { get; init; } = new()
        {
            IdEntrada = 10,
            IdEvento = 7,
            EstadoEntrada = "ACTIVA",
            EstadoEvento = "PROGRAMADO",
            PropietarioActual = Usuario
        };

        public Task<CompraResultadoDto> ComprarAsync(DocumentoUsuario comprador, ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken)
        {
            UltimasCantidades = cantidades;
            return Task.FromResult(new CompraResultadoDto { IdVenta = 1, MontoTotal = 100 });
        }

        public Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken)
        {
            UltimoLimiteReporte = limite;
            return Task.FromResult<IReadOnlyList<ReporteCompradorDto>>([]);
        }

        public Task<CompraPreviewDto> ObtenerPreviewCompraAsync(ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CompraDetalleDto?> ObtenerCompraAsync(DocumentoUsuario comprador, ulong idVenta, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<EntradaResumenDto?> ObtenerEntradaPropiaAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<EntradaQrDto?> ObtenerEntradaQrAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken) => Task.FromResult(EntradaQr);
        public Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ulong> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> ExisteFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<EventoAdminDto?> ObtenerEventoAsignableAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken) => Task.FromResult(EventoAsignable);
        public Task<bool> SectorHabilitadoParaEventoAsync(ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken) => Task.FromResult(SectorHabilitado);
        public Task<bool> ExisteAsignacionFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, CancellationToken cancellationToken) => Task.FromResult(AsignacionDuplicada);
        public Task AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken)
        {
            AsignacionInsertada = true;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ValidacionEntradaDto> ValidarEntradaQrAsync(DocumentoUsuario funcionario, string token, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteValidacionesFuncionarioDto>> ReporteValidacionesPorFuncionarioAsync(ulong? idEvento, string? funcionario, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReporteValidacionesFuncionarioDto>>([]);
        public Task<IReadOnlyList<ReporteTransferidorDto>> ReporteTransferidoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReporteTransferidorDto>>([]);
    }

    private sealed class FakeQrTokenService : IQrTokenService
    {
        public int MaxTokenLength => 255;
        public QrTokenGenerado Generar(QrTokenContext contexto) => new() { Token = "token-demo", VenceUtc = DateTimeOffset.UtcNow.AddSeconds(30), SegundosRestantes = 30 };
        public QrGenerationGrant GenerarPermisoGeneracion(QrTokenContext contexto, TimeSpan lifetime) => new() { Grant = "grant-demo", VenceUtc = DateTimeOffset.UtcNow.AddMinutes(5) };
        public ResultadoValidacionQr LeerPayload(string token) => ResultadoValidacionQr.Valido(new QrTokenPayload { IdEntrada = 1, IdEvento = 1 });
        public ResultadoValidacionQr Validar(string token, QrTokenValidationContext contexto) => ResultadoValidacionQr.Valido(new QrTokenPayload { IdEntrada = contexto.IdEntrada, IdEvento = contexto.IdEvento });
        public ResultadoValidacionPermisoQr ValidarPermisoGeneracion(string grant, DocumentoUsuario propietario) => ResultadoValidacionPermisoQr.Rechazado("No usado.");
    }
}
