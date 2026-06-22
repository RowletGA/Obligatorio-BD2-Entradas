using System.Text.Json;
using Microsoft.Extensions.Options;
using TicketingMundial.Application.Options;
using TicketingMundial.Application.Services;

namespace TicketingMundial.Tests;

public sealed class CatalogosRegistroJsonTests
{
    [Fact]
    public void CatalogoVersionado_ContieneCantidadRazonableDePaisesSinDuplicados()
    {
        var options = LoadOptions();

        Assert.True(options.Paises.Count >= 60);
        Assert.All(options.Paises, pais => Assert.Equal(2, pais.Codigo.Trim().Length));
        Assert.Equal(options.Paises.Count, options.Paises.Select(pais => pais.Codigo.Trim().ToUpperInvariant()).Distinct().Count());
    }

    [Theory]
    [InlineData("UY")]
    [InlineData("AR")]
    [InlineData("BR")]
    [InlineData("US")]
    [InlineData("CA")]
    [InlineData("MX")]
    [InlineData("ES")]
    [InlineData("FR")]
    [InlineData("DE")]
    [InlineData("JP")]
    public void CatalogoVersionado_ContienePaisesRequeridos(string codigo)
    {
        var service = CreateService();

        Assert.Contains(service.ObtenerPaises(), pais => pais.Codigo == codigo);
    }

    [Fact]
    public void TiposDocumento_SeFiltranPorPaisConComodin()
    {
        var service = CreateService();

        var uy = service.ObtenerTiposDocumentoPermitidos("uy").Select(tipo => tipo.Codigo).ToArray();
        var ar = service.ObtenerTiposDocumentoPermitidos("AR").Select(tipo => tipo.Codigo).ToArray();
        var jp = service.ObtenerTiposDocumentoPermitidos("jp").Select(tipo => tipo.Codigo).ToArray();

        Assert.Contains("CI", uy);
        Assert.Contains("PASAPORTE", uy);
        Assert.DoesNotContain("DNI", uy);
        Assert.Contains("DNI", ar);
        Assert.Contains("PASAPORTE", ar);
        Assert.Equal(["PASAPORTE"], jp);
    }

    [Fact]
    public void ValidarDocumento_RechazaPaisDesconocido()
    {
        var service = CreateService();

        var result = service.ValidarDocumento(new("PASAPORTE", "XX", "AB12345"));

        Assert.False(result.IsValid);
    }

    private static CatalogoRegistroService CreateService()
    {
        return new CatalogoRegistroService(Options.Create(LoadOptions()));
    }

    private static CatalogosRegistroOptions LoadOptions()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/TicketingMundial.Web/catalogos-registro.json"));
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        var section = document.RootElement.GetProperty(CatalogosRegistroOptions.SectionName);
        return JsonSerializer.Deserialize<CatalogosRegistroOptions>(section.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
