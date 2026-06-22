using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;

namespace TicketingMundial.Application.Abstractions.Services;

public interface IUsuarioService
{
    Task<OperationResult> RegistrarUsuarioGeneralAsync(
        RegistroUsuarioCommand command,
        CancellationToken cancellationToken);
}
