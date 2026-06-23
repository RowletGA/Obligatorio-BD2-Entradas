using Microsoft.Extensions.Options;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Options;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Security;

namespace TicketingMundial.Tests;

public sealed class QrTokenServiceTests
{
    private static readonly DocumentoUsuario Owner = new("CI", "UY", "12345678");
    private static readonly QrSecurityOptions QrOptions = new()
    {
        SigningKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
        LifetimeSeconds = 30,
        ClockSkewSeconds = 5
    };

    [Fact]
    public void Generar_MismoTokenDuranteMismaVentana()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);

        var first = service.Generar(new QrTokenContext(10, 7, Owner)).Token;
        clock.SetUtc(new DateTimeOffset(2026, 6, 23, 20, 30, 25, TimeSpan.Zero));
        var second = service.Generar(new QrTokenContext(10, 7, Owner)).Token;

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generar_TokenDiferenteDespuesDeTreintaSegundos()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);

        var first = service.Generar(new QrTokenContext(10, 7, Owner)).Token;
        clock.SetUtc(new DateTimeOffset(2026, 6, 23, 20, 30, 40, TimeSpan.Zero));
        var second = service.Generar(new QrTokenContext(10, 7, Owner)).Token;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Validar_AceptaFirmaVigente()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);
        var token = service.Generar(new QrTokenContext(10, 7, Owner)).Token;

        var result = service.Validar(token, new QrTokenValidationContext(10, 7, Owner));

        Assert.True(result.EsValido);
    }

    [Theory]
    [InlineData(1, "11")]
    [InlineData(2, "8")]
    [InlineData(3, "999999")]
    [InlineData(4, "alterada")]
    public void Validar_RechazaComponentesAlterados(int index, string replacement)
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);
        var parts = service.Generar(new QrTokenContext(10, 7, Owner)).Token.Split('.');
        parts[index] = replacement;
        var altered = string.Join('.', parts);

        var result = service.Validar(altered, new QrTokenValidationContext(10, 7, Owner));

        Assert.False(result.EsValido);
    }

    [Fact]
    public void Validar_RechazaFirmaAlterada()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);
        var token = service.Generar(new QrTokenContext(10, 7, Owner)).Token;
        var altered = token[..^1] + (token[^1] == 'A' ? "B" : "A");

        var result = service.Validar(altered, new QrTokenValidationContext(10, 7, Owner));

        Assert.False(result.EsValido);
    }

    [Fact]
    public void Validar_RechazaTokenVencido()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);
        var token = service.Generar(new QrTokenContext(10, 7, Owner)).Token;
        clock.SetUtc(new DateTimeOffset(2026, 6, 23, 20, 30, 37, TimeSpan.Zero));

        var result = service.Validar(token, new QrTokenValidationContext(10, 7, Owner));

        Assert.False(result.EsValido);
        Assert.Contains("vencido", result.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validar_AceptaToleranciaCorta()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);
        var token = service.Generar(new QrTokenContext(10, 7, Owner)).Token;
        clock.SetUtc(new DateTimeOffset(2026, 6, 23, 20, 30, 34, TimeSpan.Zero));

        var result = service.Validar(token, new QrTokenValidationContext(10, 7, Owner));

        Assert.True(result.EsValido);
    }

    [Fact]
    public void LeerPayload_RechazaFormatoVersionYLongitud()
    {
        var service = Create(new MutableTimeProvider(DateTimeOffset.UtcNow));

        Assert.False(service.LeerPayload("no-es-token").EsValido);
        Assert.False(service.LeerPayload("v2.1.1.1.abc.def").EsValido);
        Assert.False(service.LeerPayload(new string('a', service.MaxTokenLength + 1)).EsValido);
    }

    [Fact]
    public void Validar_RechazaPropietarioAnterior()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);
        var token = service.Generar(new QrTokenContext(10, 7, Owner)).Token;
        var newOwner = new DocumentoUsuario("CI", "UY", "87654321");

        var result = service.Validar(token, new QrTokenValidationContext(10, 7, newOwner));

        Assert.False(result.EsValido);
        Assert.Contains("propietario", result.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PermisoGeneracion_ValidaHastaSuVencimiento()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 23, 20, 30, 10, TimeSpan.Zero));
        var service = Create(clock);
        var grant = service.GenerarPermisoGeneracion(new QrTokenContext(10, 7, Owner), TimeSpan.FromMinutes(5)).Grant;

        Assert.True(service.ValidarPermisoGeneracion(grant, Owner).EsValido);

        clock.SetUtc(new DateTimeOffset(2026, 6, 23, 20, 35, 11, TimeSpan.Zero));
        Assert.False(service.ValidarPermisoGeneracion(grant, Owner).EsValido);
    }

    private static QrTokenService Create(TimeProvider clock) => new(Options.Create(QrOptions), clock);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void SetUtc(DateTimeOffset value) => utcNow = value;
    }
}
