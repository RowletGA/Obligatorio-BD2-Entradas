using TicketingMundial.Domain.Estados;

namespace TicketingMundial.Tests;

public sealed class EstadosTests
{
    [Theory]
    [InlineData(EstadoEvento.Programado)]
    [InlineData(EstadoEvento.EnCurso)]
    [InlineData(EstadoEvento.Finalizado)]
    [InlineData(EstadoEvento.Cancelado)]
    public void EstadosDeEvento_CoincidenConCheckConstraint(string estado)
    {
        var permitidos = new[] { "PROGRAMADO", "EN_CURSO", "FINALIZADO", "CANCELADO" };

        Assert.Contains(estado, permitidos);
    }

    [Theory]
    [InlineData(EstadoTransferencia.Pendiente)]
    [InlineData(EstadoTransferencia.Aceptada)]
    [InlineData(EstadoTransferencia.Rechazada)]
    [InlineData(EstadoTransferencia.Cancelada)]
    public void EstadosDeTransferencia_CoincidenConCheckConstraint(string estado)
    {
        var permitidos = new[] { "PENDIENTE", "ACEPTADA", "RECHAZADA", "CANCELADA" };

        Assert.Contains(estado, permitidos);
    }
}
