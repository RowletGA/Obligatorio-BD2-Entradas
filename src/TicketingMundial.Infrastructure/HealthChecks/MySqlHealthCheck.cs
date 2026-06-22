using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using TicketingMundial.Infrastructure.Data;

namespace TicketingMundial.Infrastructure.HealthChecks;

public sealed class MySqlHealthCheck(
    IDbConnectionFactory connectionFactory,
    ILogger<MySqlHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("SELECT 1;", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(result) == 1
                ? HealthCheckResult.Healthy("MySQL disponible.")
                : HealthCheckResult.Unhealthy("MySQL respondio un valor inesperado.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el health check de MySQL.");
            return HealthCheckResult.Unhealthy("MySQL no esta disponible.", ex);
        }
    }
}
