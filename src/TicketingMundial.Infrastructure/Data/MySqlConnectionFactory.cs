using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace TicketingMundial.Infrastructure.Data;

public sealed class MySqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public async ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("MySql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "La cadena ConnectionStrings:MySql no esta configurada.");
        }

        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
