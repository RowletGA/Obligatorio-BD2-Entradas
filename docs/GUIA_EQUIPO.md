# Guía del equipo

## Qué hace

TicketingMundial permite registrar usuarios, iniciar sesión, consultar eventos y preparar los flujos de compra, transferencia, validación y reportes contra la base MySQL del obligatorio.

## Cómo levantarla

```bash
dotnet restore
dotnet build TicketingMundial.sln
dotnet test TicketingMundial.sln
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/TicketingMundial.Web --urls http://localhost:5000
```

En PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/TicketingMundial.Web --urls "http://localhost:5000"
```

Configurar `src/TicketingMundial.Web/appsettings.Development.json` desde `appsettings.example.json`. No compartir credenciales reales.
Para QR dinámico, configurar también `QrSecurity__SigningKey` con una clave local:

```bash
openssl rand -base64 32
```

## Cómo probar cada rol

- Usuario general: registrarse en `/Account/Register` y luego ingresar en `/Account/Login`.
- Administrador: requiere datos existentes en tablas `Usuario` y `Administrador`.
- Funcionario: requiere datos existentes en tablas `Usuario` y `Funcionario`.

Si una misma persona tiene más de un perfil, después del login debe elegir en `/Account/SeleccionarPerfil`. El menú muestra solo la interfaz del perfil activo y permite cambiar sin cerrar sesión.

## Registro

Probar CI uruguaya, DNI argentino y pasaporte. El país y tipo de documento salen de `src/TicketingMundial.Web/catalogos-registro.json`. Los teléfonos se normalizan antes de guardar.

## Eventos

El catálogo está en `/Eventos`. Si no hay eventos visibles, cargar datos con el módulo administrativo cuando esté implementado o mediante datos autorizados por el equipo.

## Crear evento

Como administrador, abrir `/Admin`, crear estadio, sectores y equipos. Luego ir a `/Admin/Eventos/Nuevo`, seleccionar estadio, equipos y sectores. Cada sector muestra su capacidad de solo lectura y pide “Precio por entrada”. La creación usa transacción y triggers de la base.

Para editar eventos, abrir el detalle administrativo y usar `Editar evento`. La edición completa está disponible solo si no hay entradas emitidas. Con entradas o eventos cerrados, usar únicamente `Cambiar estado`.

Comprar: iniciar sesión como usuario general, abrir un evento programado y seleccionar cantidades por sector.

Transferir: abrir `/Entradas/MisEntradas`, ver detalle y usar el correo del receptor.

Validar: iniciar sesión como funcionario asignado y abrir `/Funcionario/Escanear`. La cámara usa `BarcodeDetector`; si el navegador no lo soporta, usar el token manual del QR como fallback.

Asignar funcionario: en `/Admin/Funcionarios`, elegir primero el evento. El selector de sector se carga con los sectores habilitados específicamente para ese evento. No se deben crear asignaciones sobre eventos finalizados o cancelados.

## Dónde está cada módulo

- Controladores: `src/TicketingMundial.Web/Controllers`.
- Vistas: `src/TicketingMundial.Web/Views`.
- Servicios: `src/TicketingMundial.Application/Services`.
- Repositorios: `src/TicketingMundial.Infrastructure/Repositories`.
- Pruebas: `tests/TicketingMundial.Tests`.
- Documentación larga: `docs/CONTEXTO_PROYECTO.md`.

## Cómo agregar una funcionalidad

Crear DTOs/contratos en Application, reglas en servicios, SQL parametrizado en Infrastructure y vistas/controladores en Web. No colocar SQL en controladores. No confiar en campos ocultos para usuario activo.

## Errores frecuentes

- `dotnet: command not found`: revisar SDK 8 y PATH.
- `/health` falla: revisar connection string local.
- Login 429: rate limiting por intentos repetidos.
- Usuario viejo no ingresa: `HashContrasena` está NULL.
- Documento rechazado: revisar `CatalogosRegistro`.
- Usuario con varios roles ve menú incorrecto: ir a `/Account/SeleccionarPerfil` y confirmar el perfil activo.
- Sector duplicado en asignación: verificar que se esté usando el endpoint `SectoresPorEvento` y no sectores cargados globalmente.

## Reglas para commits

No incluir secretos. No modificar scripts originales. Correr build y tests. Actualizar documentación si cambia un flujo, contrato o regla de base.
