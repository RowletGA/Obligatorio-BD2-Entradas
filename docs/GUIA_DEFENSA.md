# Guía de defensa

## Mensaje corto

La solución es ASP.NET Core MVC con MySqlConnector y SQL parametrizado. No usa ORM ni modifica los scripts originales. La base mantiene autoridad mediante triggers y vistas; la aplicación valida antes para dar mensajes claros.

## Flujos demostrables

1. Registro e inicio de sesión.
2. Selección de perfil si el usuario tiene múltiples roles.
3. Administración de estadios, sectores, equipos y eventos.
4. Compra de entradas como Usuario General.
5. Mis compras, Mis entradas y transferencias.
6. Asignación y validación como Funcionario.
7. Reportes administrativos.

## Puntos para explicar

- Roles reales: se emiten como `ClaimTypes.Role` desde base.
- Perfil activo: solo cambia navegación y dashboard; no da permisos.
- Seguridad: `[Authorize(Roles = "...")]`, antiforgery, cookies `HttpOnly`, SQL parametrizado y rate limiting en login.
- Evento: `Sector.Capacidad` es dato del sector; `EventoSector.PrecioBase` es precio por entrada para ese evento.
- Compra: el navegador no envía precio confiable; el servidor recarga disponibilidad y precio.
- Triggers: se respetan y sus errores funcionales se traducen para el usuario.

## Comandos

```bash
dotnet restore TicketingMundial.sln
dotnet build TicketingMundial.sln
dotnet test TicketingMundial.sln
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/TicketingMundial.Web --urls http://localhost:5000
```

Si el ambiente tiene runtime mayor sin .NET 8 instalado:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test TicketingMundial.sln
```

## Rutas clave

- `/health`
- `/Eventos`
- `/Account/Login`
- `/Account/SeleccionarPerfil`
- `/Dashboard`
- `/Admin`
- `/Compras`
- `/Entradas/MisEntradas`
- `/Transferencias`
- `/Funcionario`
- `/Funcionario/Validar`
- `/Admin/Reportes`
