# Ticketing Mundial 2026

Aplicacion ASP.NET Core MVC para venta, transferencia y validacion de entradas del Mundial 2026 usando una base MySQL existente.

## Requisitos

- .NET SDK 8
- MySQL 8 con la base `IC_Grupo4`
- Scripts originales de `database/`
- Sin ORM, sin Dapper y sin migraciones

Verificar el SDK:

```bash
dotnet --info
dotnet --list-sdks
```

## Base de datos

Los scripts originales son el contrato principal y no deben modificarse:

1. `database/01_CreacionTablas (v2).sql`
2. `database/02_CreacionVistas (v2).sql`
3. `database/03_CreacionTriggers (v2).sql`


## Configuracion

Copiar `src/TicketingMundial.Web/appsettings.example.json` a `src/TicketingMundial.Web/appsettings.Development.json` o configurar:

```bash
ConnectionStrings__MySql="Server=localhost;Port=3306;Database=IC_Grupo4;User ID=usuario;Password=contraseña;"
```

No guardar credenciales reales en Git.

`src/TicketingMundial.Web/appsettings.Development.json` es local y esta ignorado por Git. Debe tener este formato, usando valores reales solo en la maquina de desarrollo:

```json
{
  "ConnectionStrings": {
    "MySql": "Server=HOST;Port=PUERTO;Database=BASE;User ID=USUARIO;Password=CONTRASENA;SslMode=Preferred;AllowPublicKeyRetrieval=True;Connection Timeout=15;Default Command Timeout=30;"
  }
}
```

Confirmar que no se versiona:

```bash
git check-ignore src/TicketingMundial.Web/appsettings.Development.json
```

Comprobar conectividad TCP:

```bash
nc -vz HOST PUERTO
```

## Ejecucion

```bash
dotnet restore TicketingMundial.sln
dotnet build TicketingMundial.sln
dotnet test TicketingMundial.sln
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/TicketingMundial.Web
```

En PowerShell, la variable de entorno se define así:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/TicketingMundial.Web --urls "http://localhost:5000"
```

El health check esta disponible en `/health` y ejecuta solamente `SELECT 1;`.

URLs principales:

- `/`
- `/health`
- `/Eventos`
- `/Account/Login`
- `/Account/Register`
- `/Dashboard`
- `/Account/SeleccionarPerfil`
- `/Admin`

## Registro

La pantalla `/Account/Register` registra usuarios generales reales en:

- `Usuario`
- `UsuarioGeneral`
- `TelefonoUsuario`

El registro usa una unica transaccion MySQL. La contrasena se valida en servidor y se guarda solo como hash generado con `PasswordHasher<UsuarioAutenticacion>`.
Los paises y tipos de documento se cargan desde `src/TicketingMundial.Web/catalogos-registro.json`. El formulario renderiza opciones iniciales desde servidor y funciona aunque JavaScript no cargue.

## Administracion

El modulo `/Admin` requiere rol `ADMINISTRADOR`. Gestiona estadios, sectores, equipos y alta de eventos. El pais sede se obtiene desde la tabla `Administrador`, no desde el navegador.

La creacion de eventos usa una transaccion MySQL sobre `Evento`, `EventoLocal`, `EventoVisita` y `EventoSector`. Los triggers de la base controlan conflictos horarios, equipos simultaneos y sectores fuera del estadio.

## Flujo operativo

- Compra: usuarios generales compran desde el detalle de evento. El formulario solo envía evento, sector y cantidad.
- Mis compras: `/Compras`.
- Mis entradas: `/Entradas/MisEntradas`, usando propietario actual.
- Transferencias: `/Transferencias`.
- Funcionarios: `/Funcionario` y `/Funcionario/Validar`.
- Reportes: `/Admin/Reportes`.

La compra usa transacción y no confía en precios enviados por el navegador.

Para probarlo desde el navegador:

1. Abrir `/Account/Register`.
2. Completar documento, correo, datos personales, telefono y contrasena.
3. Al finalizar, la aplicacion redirige a login con mensaje de exito.
4. Verificar que no haya registros parciales si ocurre un error.

## Login

La pantalla `/Account/Login` solicita correo y contrasena. El login:

- busca el usuario por correo con SQL parametrizado;
- rechaza usuarios con `HashContrasena IS NULL`;
- verifica el hash con `PasswordHasher`;
- actualiza el hash si se requiere rehash;
- emite cookie segura;
- determina perfiles desde `UsuarioGeneral`, `Funcionario` y `Administrador`.

El mensaje de fallo es siempre `Correo o contraseña incorrectos.`.

Para probarlo:

1. Iniciar sesion con el correo registrado y la contrasena elegida.
2. Verificar redireccion al dashboard.
3. Cerrar sesion con el boton de la navegacion.
4. Probar credenciales incorrectas y payloads de SQL Injection; deben mostrar el mensaje generico o rate limit si se exceden intentos.

## Perfiles

Roles emitidos en claims:

- `USUARIO_GENERAL`
- `FUNCIONARIO`
- `ADMINISTRADOR`

Los roles se consultan en la base y no se aceptan desde formularios ni rutas.

Si un usuario tiene un solo rol, la aplicación activa ese perfil automáticamente. Si tiene varios roles, después del login ve `/Account/SeleccionarPerfil` y elige con qué interfaz operar. El claim propio `PerfilActivo` solo controla navegación y dashboard; todos los roles reales permanecen como `ClaimTypes.Role` y los `[Authorize(Roles = "...")]` siguen siendo la protección de seguridad.

Documento específico: `docs/PERFILES_Y_NAVEGACION.md`.

## Frontend de referencia

La carpeta `frontejemplo/` se uso solo como referencia visual: paneles blancos, fondo slate claro, acento teal, navegacion lateral en desktop y navegacion horizontal en movil.

La aplicacion no enlaza CSS, JavaScript, imagenes ni componentes desde esa carpeta. Puede eliminarse sin afectar la ejecucion.

## Pruebas

Las pruebas cubren:

- politica de contrasenas;
- generacion y verificacion de hash;
- rechazo de contrasena incorrecta y hash alterado;
- `SuccessRehashNeeded`;
- creacion de claims;
- deteccion de perfiles;
- lista blanca de ordenamiento;
- limites de paginacion;
- escape de `LIKE`;
- entradas maliciosas tratadas como texto;
- traduccion de errores funcionales MySQL.
- perfil activo, perfiles múltiples y formato monetario consistente.

## Limitacion de usuarios existentes

Usuarios creados antes de ejecutar `04_AgregarHashContrasena.sql` tendran `HashContrasena IS NULL`. Esos usuarios no pueden iniciar sesion hasta que definan una contrasena mediante un procedimiento posterior o hasta crear usuarios nuevos para la demostracion.

## Errores comunes

- `dotnet: command not found`: instalar .NET SDK 8 y revisar `PATH`.
- `/health` no responde: revisar cadena local, DNS, puerto, TLS y credenciales.
- Catalogo vacio: la base puede no tener eventos cargados; no insertar datos ficticios sin autorizacion.
- Login devuelve 429: se activo el rate limiting por intentos repetidos.
- Usuarios viejos no ingresan: verificar `HashContrasena IS NULL`.

Nunca publicar credenciales reales, cadenas de conexion reales ni hashes.
