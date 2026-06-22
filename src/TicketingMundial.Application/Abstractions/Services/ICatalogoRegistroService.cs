using TicketingMundial.Application.Options;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Abstractions.Services;

public interface ICatalogoRegistroService
{
    IReadOnlyList<PaisOption> ObtenerPaises();
    IReadOnlyList<TipoDocumentoOption> ObtenerTiposDocumento();
    IReadOnlyList<TipoDocumentoOption> ObtenerTiposDocumentoPermitidos(string? codigoPais);
    TipoDocumentoOption? ObtenerTipoDocumento(string? codigoTipo);
    CatalogoDocumentoValidationResult ValidarDocumento(DocumentoUsuario documento);
}

public sealed record CatalogoDocumentoValidationResult(
    bool IsValid,
    DocumentoUsuario DocumentoNormalizado,
    IReadOnlyList<string> Errors)
{
    public static CatalogoDocumentoValidationResult Success(DocumentoUsuario documento)
    {
        return new CatalogoDocumentoValidationResult(true, documento, []);
    }

    public static CatalogoDocumentoValidationResult Failure(DocumentoUsuario documento, IReadOnlyList<string> errors)
    {
        return new CatalogoDocumentoValidationResult(false, documento, errors);
    }
}
