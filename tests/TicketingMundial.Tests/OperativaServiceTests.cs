using TicketingMundial.Application.Abstractions.Repositories;
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
        var service = new OperativaService(new FakeOperativaRepository());

        var result = await service.ComprarAsync(Usuario, 1, [new CompraSectorCantidad(10, 6)], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("cinco", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComprarAsync_RechazaCantidadCero()
    {
        var service = new OperativaService(new FakeOperativaRepository());

        var result = await service.ComprarAsync(Usuario, 1, [new CompraSectorCantidad(10, 0)], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("al menos", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComprarAsync_NormalizaCantidadesYUsaRepositorio()
    {
        var repository = new FakeOperativaRepository();
        var service = new OperativaService(repository);

        var result = await service.ComprarAsync(Usuario, 1, [new CompraSectorCantidad(10, 2), new CompraSectorCantidad(10, 1)], CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, Assert.Single(repository.UltimasCantidades).Cantidad);
    }

    [Fact]
    public async Task ResponderTransferenciaAsync_RechazaEstadoInvalido()
    {
        var service = new OperativaService(new FakeOperativaRepository());

        var result = await service.ResponderTransferenciaAsync(Usuario, 1, "DROP", true, CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Reportes_ClampLimite()
    {
        var repository = new FakeOperativaRepository();
        var service = new OperativaService(repository);

        await service.ReporteCompradoresAsync(null, null, 500, CancellationToken.None);

        Assert.Equal(10, repository.UltimoLimiteReporte);
    }

    private sealed class FakeOperativaRepository : IOperativaRepository
    {
        public IReadOnlyList<CompraSectorCantidad> UltimasCantidades { get; private set; } = [];
        public int UltimoLimiteReporte { get; private set; }

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
        public Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ulong> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ValidacionEntradaDto> ValidarEntradaAsync(DocumentoUsuario funcionario, ulong idEntrada, string token, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
