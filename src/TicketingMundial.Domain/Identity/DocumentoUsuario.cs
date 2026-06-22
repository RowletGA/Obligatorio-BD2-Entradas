namespace TicketingMundial.Domain.Identity;

public sealed record DocumentoUsuario(
    string TipoDocumento,
    string PaisDocumento,
    string NumeroDocumento);
