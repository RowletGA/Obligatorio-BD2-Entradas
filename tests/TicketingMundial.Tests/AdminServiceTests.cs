using Microsoft.Extensions.Options;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Options;
using TicketingMundial.Application.Services;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Tests;

public sealed class AdminServiceTests
{
    private static readonly DocumentoUsuario AdminDocumento = new("CI", "UY", "12345678");

    [Fact]
    public async Task GuardarEstadioAsync_RechazaPaisFueraDeJurisdiccion()
    {
        var service = CreateService(new FakeAdminRepository());

        var result = await service.GuardarEstadioAsync(AdminDocumento, new EstadioUpsertCommand
        {
            Nombre = "Estadio demo",
            UbicacionPais = "AR",
            UbicacionLocalidad = "Montevideo",
            UbicacionCalle = "Principal"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("país sede", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardarSectorAsync_RechazaCapacidadCero()
    {
        var service = CreateService(new FakeAdminRepository());

        var result = await service.GuardarSectorAsync(AdminDocumento, new SectorUpsertCommand
        {
            IdEstadio = 1,
            NombreSector = "Tribuna",
            Capacidad = 0
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("capacidad", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardarSectorAsync_RechazaEstadioFueraDeJurisdiccion()
    {
        var service = CreateService(new FakeAdminRepository { EstadioVisible = false });

        var result = await service.GuardarSectorAsync(AdminDocumento, new SectorUpsertCommand
        {
            IdEstadio = 99,
            NombreSector = "Tribuna",
            Capacidad = 100
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("jurisdicción", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearEventoAsync_RechazaEquipoLocalIgualAVisitante()
    {
        var service = CreateService(new FakeAdminRepository());

        var result = await service.CrearEventoAsync(AdminDocumento, new EventoCreateCommand
        {
            Fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Hora = new TimeOnly(20, 0),
            IdEstadio = 1,
            IdEquipoLocal = 10,
            IdEquipoVisitante = 10,
            Sectores = [new EventoSectorCreateCommand { IdSector = 2, PrecioBase = 100 }]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("distintos", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearEventoAsync_RechazaSectorDeOtroEstadio()
    {
        var service = CreateService(new FakeAdminRepository());

        var result = await service.CrearEventoAsync(AdminDocumento, new EventoCreateCommand
        {
            Fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Hora = new TimeOnly(20, 0),
            IdEstadio = 1,
            IdEquipoLocal = 10,
            IdEquipoVisitante = 11,
            Sectores = [new EventoSectorCreateCommand { IdSector = 99, PrecioBase = 100 }]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("sectores", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminService CreateService(IAdminRepository repository)
    {
        var catalogo = new CatalogoRegistroService(Options.Create(new CatalogosRegistroOptions
        {
            Paises =
            [
                new PaisOption { Codigo = "UY", Nombre = "Uruguay" },
                new PaisOption { Codigo = "AR", Nombre = "Argentina" }
            ],
            TiposDocumento =
            [
                new TipoDocumentoOption { Codigo = "PASAPORTE", Nombre = "Pasaporte", PaisesPermitidos = ["*"], Patron = "^[A-Za-z0-9]{5,20}$", LongitudMaxima = 20 }
            ]
        }));
        return new AdminService(repository, catalogo);
    }

    private sealed class FakeAdminRepository : IAdminRepository
    {
        public bool EstadioVisible { get; init; } = true;

        public Task<AdministradorActualDto?> ObtenerAdministradorAsync(DocumentoUsuario documento, CancellationToken cancellationToken)
        {
            return Task.FromResult<AdministradorActualDto?>(new AdministradorActualDto { Documento = documento, PaisSede = "UY" });
        }

        public Task<EstadioAdminDto?> ObtenerEstadioAsync(ulong idEstadio, string paisSede, CancellationToken cancellationToken)
        {
            return Task.FromResult(EstadioVisible
                ? new EstadioAdminDto { IdEstadio = idEstadio, UbicacionPais = paisSede, Nombre = "Demo", UbicacionLocalidad = "Montevideo", UbicacionCalle = "Principal" }
                : null);
        }

        public Task<EquipoAdminDto?> ObtenerEquipoAsync(ulong idEquipo, CancellationToken cancellationToken)
        {
            return Task.FromResult<EquipoAdminDto?>(new EquipoAdminDto { IdEquipo = idEquipo, Pais = "UY" });
        }

        public Task<IReadOnlyList<SectorAdminDto>> ListarSectoresPorEstadioAsync(ulong idEstadio, string paisSede, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SectorAdminDto>>([new SectorAdminDto { IdSector = 2, IdEstadio = idEstadio, NombreSector = "Tribuna", Capacidad = 100, PaisEstadio = paisSede }]);
        }

        public Task<bool> ExisteSectorNombreAsync(ulong idEstadio, string nombreSector, ulong? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<AdminDashboardDto> ObtenerDashboardAsync(string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<EstadioAdminDto>> ListarEstadiosAsync(string paisSede, string? busqueda, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<EstadioAdminDto>> ListarEstadiosParaSeleccionAsync(string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ulong> CrearEstadioAsync(EstadioUpsertCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> ActualizarEstadioAsync(EstadioUpsertCommand command, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<SectorAdminDto>> ListarSectoresAsync(string paisSede, ulong? idEstadio, string? busqueda, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SectorAdminDto?> ObtenerSectorAsync(ulong idSector, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ulong> CrearSectorAsync(SectorUpsertCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> ActualizarSectorAsync(SectorUpsertCommand command, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<EquipoAdminDto>> ListarEquiposAsync(string? busqueda, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<EquipoAdminDto>> ListarEquiposParaSeleccionAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ulong> CrearEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> ActualizarEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<EventoAdminDto>> ListarEventosAsync(string paisSede, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<EventoAdminDto?> ObtenerEventoAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ulong> CrearEventoAsync(EventoCreateCommand command, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> CambiarEstadoEventoAsync(ulong idEvento, string estado, string paisSede, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
