# Ticketing Mundial 2026

Aplicación ASP.NET Core MVC para venta, transferencia y validación de entradas del Mundial 2026 usando una base MySQL existente.

## Requisitos

- .NET SDK 8
- MySQL 8 con la base `IC_Grupo4`
- Scripts originales de `database/`
- Sin ORM, sin Dapper y sin migraciones

## Herramientas y tecnologías usadas

- C# y .NET 8.
- ASP.NET Core MVC con Razor Views.
- MySQL 8 como motor de base de datos.
- MySqlConnector para acceso SQL manual y parametrizado.
- HTML, CSS, Bootstrap 5 y JavaScript del navegador.
- QRCoder para generar imágenes QR.
- HMAC-SHA256 para firma de tokens QR dinámicos.
- `PasswordHasher` de ASP.NET Core Identity para hashes de contraseña.
- xUnit, Microsoft.NET.Test.Sdk y coverlet para pruebas automatizadas.
- Git para control de versiones.

Verificar el SDK:

```bash
dotnet --info
dotnet --list-sdks
```

## Base de datos

Los scripts originales son el contrato principal y no deben modificarse:

1. `database/01_CreacionTablas (v3).sql`
2. `database/02_CreacionVistas (v3).sql`
3. `database/03_CreacionTriggers (v3).sql`


## Configuración

Copiar `src/TicketingMundial.Web/appsettings.example.json` a `src/TicketingMundial.Web/appsettings.Development.json` o configurar:

```bash
ConnectionStrings__MySql="Server=HOST;Port=PUERTO;Database=BASE;User ID=USUARIO;Password=CONTRASEÑA;"
QrSecurity__SigningKey="$(openssl rand -base64 32)"
```

No guardar credenciales reales en Git.

Para desarrollo local, se recomienda guardar la clave QR con user-secrets:

```bash
dotnet user-secrets init --project src/TicketingMundial.Web

dotnet user-secrets set \
  "QrSecurity:SigningKey" \
  "$(openssl rand -base64 32)" \
  --project src/TicketingMundial.Web
```

Alternativamente, se puede usar una variable de entorno:

```bash
export QrSecurity__SigningKey="$(openssl rand -base64 32)"
```

`src/TicketingMundial.Web/appsettings.Development.json` es local y está ignorado por Git. Debe tener este formato, usando valores reales solo en la máquina de desarrollo:

```json
{
  "ConnectionStrings": {
    "MySql": "Server=HOST;Port=PUERTO;Database=BASE;User ID=USUARIO;Password=CONTRASEÑA;SslMode=Preferred;AllowPublicKeyRetrieval=True;Connection Timeout=15;Default Command Timeout=30;"
  },
  "QrSecurity": {
    "SigningKey": "CONFIGURAR_MEDIANTE_VARIABLE_DE_ENTORNO",
    "LifetimeSeconds": 30,
    "ClockSkewSeconds": 5
  }
}
```

El valor `CONFIGURAR_MEDIANTE_VARIABLE_DE_ENTORNO` es solo un marcador. En `Production`, la aplicación lo rechaza al iniciar. En `Development`, si `QrSecurity:SigningKey` falta o no es válida, se genera una clave temporal segura en memoria; los QR emitidos dejan de ser válidos al reiniciar la aplicación.

Confirmar que no se versiona:

```bash
git check-ignore src/TicketingMundial.Web/appsettings.Development.json
```

Comprobar conectividad TCP:

```bash
nc -vz HOST PUERTO
```

## Ejecución

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

El health check está disponible en `/health` y ejecuta solamente `SELECT 1;`.

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

El registro usa una única transacción MySQL. La contraseña se valida en servidor y se guarda solo como hash generado con `PasswordHasher<UsuarioAutenticacion>`.
Los países y tipos de documento se cargan desde `src/TicketingMundial.Web/catalogos-registro.json`. El formulario renderiza opciones iniciales desde servidor y funciona aunque JavaScript no cargue.

## Administración

El módulo `/Admin` requiere rol `ADMINISTRADOR`. Gestiona estadios, sectores, equipos y alta de eventos. El país sede se obtiene desde la tabla `Administrador`, no desde el navegador.

La creación de eventos usa una transacción MySQL sobre `Evento`, `EventoLocal`, `EventoVisita` y `EventoSector`. Los triggers de la base controlan conflictos horarios, equipos simultáneos y sectores fuera del estadio.

## Flujo operativo

- Compra: usuarios generales compran desde el detalle de evento. El formulario solo envía evento, sector y cantidad.
- Mis compras: `/Compras`.
- Mis entradas: `/Entradas/MisEntradas`, usando propietario actual.
- Transferencias: `/Transferencias`.
- Funcionarios: `/Funcionario` y `/Funcionario/Escanear`.
- Reportes: `/Admin/Reportes`.

La compra usa transacción y no confía en precios enviados por el navegador.
El QR dinámico usa HMAC-SHA256, vence cada 30 segundos y se valida contra propietario actual y asignación del funcionario. Para demo en una sola computadora, el usuario puede descargar el QR actual y el funcionario cargar esa imagen en `/Funcionario/Escanear`; la imagen se procesa en el navegador y el servidor recibe solo el token.

Para probarlo desde el navegador:

1. Abrir `/Account/Register`.
2. Completar documento, correo, datos personales, teléfono y contraseña.
3. Al finalizar, la aplicación redirige a login con mensaje de éxito.
4. Verificar que no haya registros parciales si ocurre un error.

## Login

La pantalla `/Account/Login` solicita correo y contraseña. El login:

- busca el usuario por correo con SQL parametrizado;
- rechaza usuarios con `HashContrasena IS NULL`;
- verifica el hash con `PasswordHasher`;
- actualiza el hash si se requiere rehash;
- emite cookie segura;
- determina perfiles desde `UsuarioGeneral`, `Funcionario` y `Administrador`.

El mensaje de fallo es siempre `Correo o contraseña incorrectos.`.

Para probarlo:

1. Iniciar sesión con el correo registrado y la contraseña elegida.
2. Verificar redirección al dashboard.
3. Cerrar sesión con el botón de la navegación.
4. Probar credenciales incorrectas y payloads de SQL Injection; deben mostrar el mensaje genérico o rate limit si se exceden intentos.

## Perfiles

Roles emitidos en claims:

- `USUARIO_GENERAL`
- `FUNCIONARIO`
- `ADMINISTRADOR`

Los roles se consultan en la base y no se aceptan desde formularios ni rutas.

Si un usuario tiene un solo rol, la aplicación activa ese perfil automáticamente. Si tiene varios roles, después del login ve `/Account/SeleccionarPerfil` y elige con qué interfaz operar. El claim propio `PerfilActivo` solo controla navegación y dashboard; todos los roles reales permanecen como `ClaimTypes.Role` y los `[Authorize(Roles = "...")]` siguen siendo la protección de seguridad.

## Pruebas

Las pruebas cubren:

- política de contraseñas;
- generación y verificación de hash;
- rechazo de contraseña incorrecta y hash alterado;
- `SuccessRehashNeeded`;
- creación de claims;
- detección de perfiles;
- lista blanca de ordenamiento;
- límites de paginación;
- escape de `LIKE`;
- entradas maliciosas tratadas como texto;
- traducción de errores funcionales MySQL.
- perfil activo, perfiles múltiples y formato monetario consistente.
- tokens QR dinámicos, firma, expiración, tolerancia e invalidación por cambio de propietario.

## Limitación de usuarios existentes

Usuarios creados antes de ejecutar `04_AgregarHashContrasena.sql` tendrán `HashContrasena IS NULL`. Esos usuarios no pueden iniciar sesión hasta que definan una contraseña mediante un procedimiento posterior o hasta crear usuarios nuevos para la demostración.

## Errores comunes

- `dotnet: command not found`: instalar .NET SDK 8 y revisar `PATH`.
- `/health` no responde: revisar cadena local, DNS, puerto, TLS y credenciales.
- Catálogo vacío: la base puede no tener eventos cargados; no insertar datos ficticios sin autorización.
- Login devuelve 429: se activó el rate limiting por intentos repetidos.
- Usuarios viejos no ingresan: verificar `HashContrasena IS NULL`.

Nunca publicar credenciales reales, cadenas de conexión reales ni hashes.

## Listados, códigos visuales y funcionario

Los códigos `ENT-000`, `VENT-000` y `TRF-000` son identificadores visuales dinámicos calculados por la aplicación. No reemplazan `IDEntrada`, `IDVenta` ni `IDTransferencia`, que siguen siendo las claves reales para URLs, formularios, relaciones y consultas.

- `VENT`: `ROW_NUMBER()` por usuario sobre `FechaVenta ASC, IDVenta ASC`; el listado inicia de más reciente a más antigua.
- `TRF`: numeración global por usuario sobre todas las transferencias visibles, con `FechaSolicitud ASC, IDTransferencia ASC`; evita duplicar `TRF-001` entre enviadas y recibidas.
- `ENT`: numeración por fecha efectiva de adquisición del propietario actual. Si recibió la entrada por transferencia aceptada, usa la última `FechaRespuesta` aceptada hacia ese propietario; si no, usa `Entrada.FechaEmision`.

Los listados de entradas, compras, transferencias, eventos y administración tienen filtros, búsqueda parametrizada, paginación limitada a 10/25/50 y ordenamiento por claves de lista blanca. Los badges de estado se centralizan en `StatusBadgeHelper` y clases CSS semánticas.

La interfaz de funcionario quedó centrada en `/Funcionario`: eventos asignados activos, escáner, historial, cambiar perfil si corresponde y cerrar sesión. `/Dashboard/Funcionario` continúa redirigiendo para compatibilidad.
