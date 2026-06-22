using Microsoft.Extensions.Options;
using TicketingMundial.Application.Options;
using TicketingMundial.Application.Services;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Tests;

public sealed class CatalogoRegistroServiceTests
{
    [Fact]
    public void ValidarDocumento_AcceptsUruguayanCi()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new DocumentoUsuario("ci", "uy", "12345678"));

        Assert.True(result.IsValid);
        Assert.Equal("CI", result.DocumentoNormalizado.TipoDocumento);
        Assert.Equal("UY", result.DocumentoNormalizado.PaisDocumento);
    }

    [Fact]
    public void ValidarDocumento_AcceptsArgentinianDni()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new DocumentoUsuario("DNI", "AR", "123456789"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidarDocumento_AcceptsPassportForAnyConfiguredCountry()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new DocumentoUsuario("PASAPORTE", "BR", "AB12345"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidarDocumento_RejectsInventedType()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new DocumentoUsuario("OTRO_INVENTADO", "UY", "12345678"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("tipo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidarDocumento_RejectsSqlPayloadCountry()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new DocumentoUsuario("CI", "DROP TABLE", "12345678"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("país", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidarDocumento_RejectsCiOutsideUruguay()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new DocumentoUsuario("CI", "AR", "12345678"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("permitido", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidarDocumento_RejectsInvalidFormat()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new DocumentoUsuario("PASAPORTE", "UY", "AB 123"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("formato", StringComparison.OrdinalIgnoreCase));
    }

    private static CatalogoRegistroService CreateService()
    {
        return new CatalogoRegistroService(Options.Create(new CatalogosRegistroOptions
        {
            Paises =
            [
                new PaisOption { Codigo = "UY", Nombre = "Uruguay" },
                new PaisOption { Codigo = "AR", Nombre = "Argentina" },
                new PaisOption { Codigo = "BR", Nombre = "Brasil" }
            ],
            TiposDocumento =
            [
                new TipoDocumentoOption
                {
                    Codigo = "CI",
                    Nombre = "Cédula de identidad",
                    PaisesPermitidos = ["UY"],
                    Patron = "^[0-9]{7,8}$",
                    LongitudMaxima = 8,
                    Ayuda = "Ingresá solamente números."
                },
                new TipoDocumentoOption
                {
                    Codigo = "DNI",
                    Nombre = "Documento Nacional de Identidad",
                    PaisesPermitidos = ["AR"],
                    Patron = "^[0-9]{7,9}$",
                    LongitudMaxima = 9,
                    Ayuda = "Ingresá solamente números."
                },
                new TipoDocumentoOption
                {
                    Codigo = "PASAPORTE",
                    Nombre = "Pasaporte",
                    PaisesPermitidos = ["*"],
                    Patron = "^[A-Za-z0-9]{5,20}$",
                    LongitudMaxima = 20,
                    Ayuda = "Ingresá letras y números sin espacios."
                }
            ]
        }));
    }
}
