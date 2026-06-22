using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.Options;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Services;

public sealed class CatalogoRegistroService(
    IOptions<CatalogosRegistroOptions> options) : ICatalogoRegistroService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private readonly CatalogosRegistroOptions options = options.Value;

    public IReadOnlyList<PaisOption> ObtenerPaises()
    {
        return options.Paises
            .Select(pais => new PaisOption
            {
                Codigo = NormalizeCode(pais.Codigo),
                Nombre = pais.Nombre.Trim()
            })
            .Where(pais => pais.Codigo.Length > 0 && pais.Nombre.Length > 0)
            .OrderBy(pais => pais.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<TipoDocumentoOption> ObtenerTiposDocumento()
    {
        return options.TiposDocumento
            .Select(NormalizeTipo)
            .Where(tipo => tipo.Codigo.Length > 0 && tipo.Nombre.Length > 0)
            .OrderBy(tipo => tipo.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<TipoDocumentoOption> ObtenerTiposDocumentoPermitidos(string? codigoPais)
    {
        var pais = NormalizeCode(codigoPais);
        if (pais.Length == 0)
        {
            return [];
        }

        return ObtenerTiposDocumento()
            .Where(tipo => TipoPermitePais(tipo, pais))
            .ToArray();
    }

    public TipoDocumentoOption? ObtenerTipoDocumento(string? codigoTipo)
    {
        var tipoNormalizado = NormalizeCode(codigoTipo);
        return ObtenerTiposDocumento().FirstOrDefault(tipo => tipo.Codigo == tipoNormalizado);
    }

    public CatalogoDocumentoValidationResult ValidarDocumento(DocumentoUsuario documento)
    {
        var normalizado = new DocumentoUsuario(
            NormalizeCode(documento.TipoDocumento),
            NormalizeCode(documento.PaisDocumento),
            documento.NumeroDocumento.Trim());

        var errors = new List<string>();
        var pais = ObtenerPaises().FirstOrDefault(item => item.Codigo == normalizado.PaisDocumento);
        if (pais is null)
        {
            errors.Add("El país del documento no pertenece al catálogo permitido.");
        }

        var tipo = ObtenerTipoDocumento(normalizado.TipoDocumento);
        if (tipo is null)
        {
            errors.Add("El tipo de documento no pertenece al catálogo permitido.");
        }
        else
        {
            if (normalizado.PaisDocumento.Length > 0 && !TipoPermitePais(tipo, normalizado.PaisDocumento))
            {
                errors.Add("El tipo de documento no está permitido para el país seleccionado.");
            }

            if (normalizado.NumeroDocumento.Length == 0)
            {
                errors.Add("El número de documento es obligatorio.");
            }

            if (tipo.LongitudMaxima > 0 && normalizado.NumeroDocumento.Length > tipo.LongitudMaxima)
            {
                errors.Add($"El número de documento no puede superar {tipo.LongitudMaxima} caracteres.");
            }

            if (!string.IsNullOrWhiteSpace(tipo.Patron) &&
                !Regex.IsMatch(normalizado.NumeroDocumento, tipo.Patron, RegexOptions.CultureInvariant, RegexTimeout))
            {
                errors.Add(tipo.Ayuda.Length > 0
                    ? $"El número de documento no tiene un formato válido. {tipo.Ayuda}"
                    : "El número de documento no tiene un formato válido.");
            }
        }

        return errors.Count == 0
            ? CatalogoDocumentoValidationResult.Success(normalizado)
            : CatalogoDocumentoValidationResult.Failure(normalizado, errors);
    }

    private static bool TipoPermitePais(TipoDocumentoOption tipo, string pais)
    {
        return tipo.PaisesPermitidos.Contains("*", StringComparer.OrdinalIgnoreCase) ||
            tipo.PaisesPermitidos.Contains(pais, StringComparer.OrdinalIgnoreCase);
    }

    private static TipoDocumentoOption NormalizeTipo(TipoDocumentoOption tipo)
    {
        return new TipoDocumentoOption
        {
            Codigo = NormalizeCode(tipo.Codigo),
            Nombre = tipo.Nombre.Trim(),
            Patron = tipo.Patron.Trim(),
            LongitudMaxima = tipo.LongitudMaxima,
            Ayuda = tipo.Ayuda.Trim(),
            PaisesPermitidos = tipo.PaisesPermitidos
                .Select(NormalizeCode)
                .Where(pais => pais.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string NormalizeCode(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
