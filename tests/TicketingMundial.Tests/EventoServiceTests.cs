using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Services;

namespace TicketingMundial.Tests;

public sealed class EventoServiceTests
{
    [Fact]
    public async Task ObtenerDetalleAsync_RetornaNull_CuandoNoExisteEvento()
    {
        var repository = new FakeEventoRepository(null, []);
        var service = new EventoService(repository);

        var result = await service.ObtenerDetalleAsync(99, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ObtenerDetalleAsync_CombinaEventoYSectores()
    {
        var evento = new EventoResumenDto
        {
            IdEvento = 10,
            FechaHora = new DateTime(2026, 6, 22, 20, 0, 0),
            Estado = "PROGRAMADO",
            IdEstadio = 1,
            Estadio = "Estadio Central",
            PaisEstadio = "Uruguay",
            LocalidadEstadio = "Montevideo",
            EquipoLocal = "Uruguay",
            EquipoVisitante = "Canada"
        };

        var sectores = new[]
        {
            new SectorDisponibilidadDto
            {
                IdEvento = 10,
                IdSector = 5,
                NombreSector = "A",
                Capacidad = 100,
                PrecioBase = 75,
                EntradasEmitidas = 20,
                LugaresDisponibles = 80
            }
        };

        var repository = new FakeEventoRepository(evento, sectores);
        var service = new EventoService(repository);

        var result = await service.ObtenerDetalleAsync(10, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(10UL, result.IdEvento);
        Assert.Single(result.Sectores);
        Assert.Equal(80, result.Sectores[0].LugaresDisponibles);
    }

    private sealed class FakeEventoRepository(
        EventoResumenDto? evento,
        IReadOnlyList<SectorDisponibilidadDto> sectores) : IEventoRepository
    {
        public Task<IReadOnlyList<EventoResumenDto>> ObtenerEventosAsync(
            EventoFiltroDto filtro,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<EventoResumenDto> eventos = evento is null ? [] : [evento];
            return Task.FromResult(eventos);
        }

        public Task<EventoResumenDto?> ObtenerEventoAsync(
            ulong idEvento,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(evento?.IdEvento == idEvento ? evento : null);
        }

        public Task<IReadOnlyList<SectorDisponibilidadDto>> ObtenerDisponibilidadAsync(
            ulong idEvento,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(sectores);
        }

        public Task<IReadOnlyList<EstadioFiltroDto>> ObtenerEstadiosAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<EstadioFiltroDto> estadios = [];
            return Task.FromResult(estadios);
        }
    }
}
