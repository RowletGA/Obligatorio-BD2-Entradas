namespace TicketingMundial.Application.Options;

public sealed class CatalogosRegistroOptions
{
    public const string SectionName = "CatalogosRegistro";

    public List<PaisOption> Paises { get; init; } = [];
    public List<TipoDocumentoOption> TiposDocumento { get; init; } = [];
}

public sealed class PaisOption
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
}

public sealed class TipoDocumentoOption
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public List<string> PaisesPermitidos { get; init; } = [];
    public string Patron { get; init; } = string.Empty;
    public int LongitudMaxima { get; init; }
    public string Ayuda { get; init; } = string.Empty;
}
