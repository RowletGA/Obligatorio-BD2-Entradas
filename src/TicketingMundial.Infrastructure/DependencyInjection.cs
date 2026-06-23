using Microsoft.Extensions.DependencyInjection;
using TicketingMundial.Application.Abstractions.Authentication;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Infrastructure.Data;
using TicketingMundial.Infrastructure.Errors;
using TicketingMundial.Infrastructure.HealthChecks;
using TicketingMundial.Infrastructure.Repositories;
using TicketingMundial.Infrastructure.Authentication;
using TicketingMundial.Application.Abstractions.Security;
using TicketingMundial.Infrastructure.Security;

namespace TicketingMundial.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
        services.AddScoped<IMySqlExceptionTranslator, MySqlExceptionTranslator>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IOperativaRepository, OperativaRepository>();
        services.AddScoped<IEventoRepository, EventoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IQrTokenService, QrTokenService>();
        services.AddHealthChecks().AddCheck<MySqlHealthCheck>("mysql");

        return services;
    }
}
