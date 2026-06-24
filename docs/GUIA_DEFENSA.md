# Guía de defensa

## Mensaje corto

La solución es ASP.NET Core MVC con MySqlConnector y SQL parametrizado. No usa ORM ni modifica los scripts originales. La base mantiene autoridad mediante triggers y vistas; la aplicación valida antes para dar mensajes claros.

## Flujos demostrables

1. Registro e inicio de sesión.
2. Selección de perfil si el usuario tiene múltiples roles.
3. Administración de estadios, sectores, equipos y eventos.
4. Compra de entradas como Usuario General.
5. Mis compras, Mis entradas y transferencias.
6. QR dinámico, escaneo y validación como Funcionario.
7. Reportes administrativos.

## Puntos para explicar

- Roles reales: se emiten como `ClaimTypes.Role` desde base.
- Perfil activo: solo cambia navegación y dashboard; no da permisos.
- Seguridad: `[Authorize(Roles = "...")]`, antiforgery, cookies `HttpOnly`, SQL parametrizado y rate limiting en login.
- Evento: `Sector.Capacidad` es dato del sector; `EventoSector.PrecioBase` es precio por entrada para ese evento.
- Edición de evento: si no hay entradas, se actualizan evento, equipos y sectores en transacción. Si hay entradas, se bloquea edición estructural para no invalidar entradas ya emitidas.
- Asignaciones: los eventos cerrados no se ofrecen y los sectores salen de `EventoSector` filtrado por `IDEvento`, lo que evita duplicados y sectores incorrectos.
- Compra: el navegador no envía precio confiable; el servidor recarga disponibilidad y precio.
- Triggers: se respetan y sus errores funcionales se traducen para el usuario.
- QR: el token no es JWT ni GUID en memoria; es HMAC-SHA256 con ventana de 30 segundos y marca de propietario derivada de `V_PropietarioActual`.
- Renovación: no escribe tokens ni estados. Cada renovación consulta la base para invalidar inmediatamente entradas validadas, anuladas, transferidas o eventos cerrados.
- Consumo irreversible: al validar se inserta `Validacion`, el trigger pasa la entrada a `VALIDADA` y la aplicación cancela transferencias `PENDIENTE` de esa entrada en la misma transacción.
- Demo en una PC: descargar el QR actual como PNG y cargarlo en `/Funcionario/Escanear`; la imagen se decodifica en el navegador y no llega al servidor.

## Comandos

```bash
dotnet restore TicketingMundial.sln
dotnet build TicketingMundial.sln
dotnet test TicketingMundial.sln
QrSecurity__SigningKey="$(openssl rand -base64 32)" ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/TicketingMundial.Web --urls http://localhost:5000
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
- `/Funcionario/Escanear`
- `/Admin/Reportes`
