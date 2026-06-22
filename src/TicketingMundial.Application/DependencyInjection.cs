using Microsoft.Extensions.DependencyInjection;
using TicketingMundial.Application.Abstractions.Authentication;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.Services;

namespace TicketingMundial.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICatalogoRegistroService, CatalogoRegistroService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IOperativaService, OperativaService>();
        services.AddScoped<IEventoService, EventoService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        return services;
    }
}
