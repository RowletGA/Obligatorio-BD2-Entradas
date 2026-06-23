# Perfiles y navegación

## Roles reales

Un usuario puede existir en `UsuarioGeneral`, `Administrador` y `Funcionario` al mismo tiempo. Al iniciar sesión, la aplicación emite todos los roles reales como `ClaimTypes.Role`.

Roles permitidos:

- `USUARIO_GENERAL`
- `ADMINISTRADOR`
- `FUNCIONARIO`

## Perfil activo

`PerfilActivo` es un claim propio que define qué interfaz se muestra en ese momento. No se guarda en la base y no elimina roles reales.

Ejemplo:

- Roles reales: `USUARIO_GENERAL`, `ADMINISTRADOR`
- Perfil activo: `ADMINISTRADOR`

En ese caso el usuario sigue técnicamente autorizado para endpoints de ambos roles, pero la navegación muestra solo administración hasta que cambie de perfil.

## Selección inicial

Después del login:

- si el usuario tiene un solo rol, se activa automáticamente;
- si tiene más de un rol, se redirige a `/Account/SeleccionarPerfil`;
- la pantalla muestra únicamente perfiles presentes en `ClaimTypes.Role`.

## Cambio dinámico

La operación `POST /Account/CambiarPerfil`:

1. Recibe el perfil solicitado.
2. Lee los roles reales de la identidad autenticada.
3. Verifica que el usuario posea ese rol.
4. Rechaza perfiles manipulados.
5. Reemite la cookie con el nuevo `PerfilActivo`.
6. Conserva todos los `ClaimTypes.Role`.
7. Redirige a `/Dashboard`.

## Navegación

Sin sesión:

- Inicio
- Eventos
- Iniciar sesión
- Registrarse

Usuario General:

- Inicio
- Eventos
- Mis compras
- Mis entradas
- Transferencias
- Mi perfil
- Cambiar perfil, solo si posee más de uno
- Cerrar sesión

Administrador:

- Dashboard
- Estadios
- Sectores
- Equipos
- Eventos
- Funcionarios
- Reportes
- Cambiar perfil, solo si posee más de uno
- Cerrar sesión

Funcionario:

- Dashboard
- Eventos asignados
- Sectores asignados
- Escanear entradas
- Historial de validaciones
- Cambiar perfil, solo si posee más de uno
- Cerrar sesión

## Seguridad

`PerfilActivo` no sustituye autorización. Todos los endpoints sensibles mantienen `[Authorize(Roles = "...")]`. Cambiar a perfil Usuario General no otorga permisos administrativos, y operar como Administrador no elimina los roles reales de la cookie.

## Archivos principales

- `src/TicketingMundial.Web/Extensions/PerfilActivoExtensions.cs`
- `src/TicketingMundial.Web/Controllers/AccountController.cs`
- `src/TicketingMundial.Web/Controllers/DashboardController.cs`
- `src/TicketingMundial.Web/Views/Account/SeleccionarPerfil.cshtml`
- `src/TicketingMundial.Web/Views/Shared/_Layout.cshtml`
- `tests/TicketingMundial.Tests/PerfilActivoExtensionsTests.cs`

## Pruebas

Hay pruebas para:

- usuario de un solo rol;
- usuario con múltiples roles sin perfil activo;
- claim manipulado;
- conservación de todos los roles reales;
- formato monetario;
- rechazo de precio cero al crear evento.
