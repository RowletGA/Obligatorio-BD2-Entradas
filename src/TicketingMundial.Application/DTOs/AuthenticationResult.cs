namespace TicketingMundial.Application.DTOs;

public sealed record AuthenticationResult(
    bool Succeeded,
    UsuarioAutenticadoDto? Usuario,
    string? ErrorMessage)
{
    public static AuthenticationResult Success(UsuarioAutenticadoDto usuario) =>
        new(true, usuario, null);

    public static AuthenticationResult Failure(string message) =>
        new(false, null, message);
}
