using MySqlConnector;

namespace TicketingMundial.Infrastructure.Data;

public interface IDbConnectionFactory
{
    ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken);
}
