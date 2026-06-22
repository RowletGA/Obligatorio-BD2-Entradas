using TicketingMundial.Application.DTOs;

namespace TicketingMundial.Application.Abstractions.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(
        string correoElectronico,
        string password,
        CancellationToken cancellationToken);
}
