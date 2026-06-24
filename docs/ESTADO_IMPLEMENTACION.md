# Estado de implementación

## 2026-06-22 - Registro parametrizable y documentación base

Funcionalidades:

- Registro con catálogos configurables para países y tipos de documento.
- Selectores controlados para tipo de documento, país del documento y país de dirección.
- Validación servidor de país, tipo, combinación país/tipo, longitud y formato de documento.
- Normalización de códigos de catálogo a mayúsculas.
- Validación y normalización de teléfonos: mínimo uno, sin duplicados, 7 a 20 caracteres, prefijo `+` opcional y solo dígitos luego de normalizar espacios, guiones y paréntesis.
- Pantalla de registro reorganizada en Documento, Datos personales, Dirección, Contacto y Seguridad.
- JavaScript de apoyo para filtrar tipos por país, mostrar ayuda y reindexar teléfonos.
- Documentación de contexto para equipo y futuras herramientas de IA.

Archivos importantes:

- `src/TicketingMundial.Application/Options/CatalogosRegistroOptions.cs`
- `src/TicketingMundial.Application/Services/CatalogoRegistroService.cs`
- `src/TicketingMundial.Application/Validation/TelefonoValidator.cs`
- `src/TicketingMundial.Web/Controllers/AccountController.cs`
- `src/TicketingMundial.Web/Views/Account/Register.cshtml`
- `src/TicketingMundial.Web/wwwroot/js/site.js`
- `src/TicketingMundial.Web/appsettings.json`
- `tests/TicketingMundial.Tests/CatalogoRegistroServiceTests.cs`
- `tests/TicketingMundial.Tests/TelefonoValidatorTests.cs`
- `docs/CONTEXTO_PROYECTO.md`
- `docs/GUIA_EQUIPO.md`

Pruebas:

- `dotnet build TicketingMundial.sln`: correcto, 0 warnings, 0 errores.
- `dotnet test TicketingMundial.sln`: 55 exitosas, 0 fallidas.

Decisiones:

- Los catálogos viven en configuración no sensible, no en Razor.
- El navegador mejora la experiencia, pero el servidor es la autoridad final.
- No se envían expresiones regulares al navegador como fuente de verdad.
- No se modifican scripts originales ni se crean tablas nuevas.

Nota histórica: en esta primera iteración quedaban pendientes administración, compra, transferencias, validación y reportes. Esos puntos fueron cerrados en iteraciones posteriores.

## 2026-06-22 - Catálogo completo y módulo administrativo

Funcionalidades:

- Catálogo versionado en `src/TicketingMundial.Web/catalogos-registro.json` con 80 países ISO alpha-2.
- Registro con país inicial `UY` y tipos cargados desde servidor.
- JavaScript de documentos como mejora progresiva, sin dejar el selector vacío.
- Administración protegida por rol en `/Admin`.
- Dashboard administrativo con métricas reales.
- CRUD funcional de estadios, sectores y equipos.
- Creación transaccional de eventos con sectores y precios.
- Cambio de estado de evento con valores controlados.

Pruebas:

- Catálogo real: cantidad, duplicados, códigos y países requeridos.
- Reglas admin: jurisdicción, capacidad, estadio fuera de país, equipos iguales y sector de otro estadio.
- `dotnet test TicketingMundial.sln`: 73 exitosas.

Limitaciones de esa iteración:

- No se implementó eliminación física.
- Funcionarios y reportes se completaron en iteraciones posteriores.
- La prueba manual de creación real requiere un usuario administrador existente.

## 2026-06-22 - Recorrido operativo mínimo

Funcionalidades:

- Compra de entradas para `USUARIO_GENERAL` desde detalle de evento.
- Confirmación de compra con cálculo informativo y recálculo servidor.
- Transacción MySQL de compra con `SELECT ... FOR UPDATE`, inserción de `Venta` y `Entrada`, y confirmación de venta.
- `/Compras` y detalle de compra filtrados por claims.
- `/Entradas/MisEntradas` basado en `V_PropietarioActual`.
- Transferencias enviadas/recibidas, creación, aceptación, rechazo y cancelación.
- Administración mínima de asignaciones de funcionarios.
- Validación inicial de entrada para funcionario.
- Reportes básicos de eventos vendidos y compradores.

Pruebas:

- `dotnet build TicketingMundial.sln`: correcto, 0 warnings.
- `dotnet test TicketingMundial.sln`: 78 exitosas.

Limitaciones de esa iteración:

- No se ejecutó recorrido E2E completo con datos reales por falta de credenciales demo confirmadas en esa sesión.
- QR gráfico y cámara se implementaron después; dispositivos autorizados sigue parcial porque no existe entidad de base.

## 2026-06-22 - Pulido final de perfiles, navegación y eventos

Funcionalidades:

- Claim propio `PerfilActivo` para separar interfaz activa de roles reales.
- Login redirige a selección cuando un usuario posee múltiples perfiles.
- `/Account/SeleccionarPerfil` muestra solo perfiles reales del usuario.
- `/Account/CambiarPerfil` valida el perfil solicitado contra `ClaimTypes.Role`, reemite cookie y conserva todos los roles.
- `/Dashboard` decide por perfil activo y corrige cookies antiguas sin `PerfilActivo`.
- Navegación diferenciada para público, usuario general, administrador y funcionario.
- Formulario de evento muestra capacidad de sector como solo lectura y etiqueta precio como “Precio por entrada”.
- Precio de sector obligatorio solo si el sector está seleccionado, con `min="0.01"` y `step="0.01"`.
- Formato monetario unificado con `MoneyFormatter`.

Pruebas:

- `dotnet build TicketingMundial.sln`: correcto, 0 warnings.
- `DOTNET_ROLL_FORWARD=Major dotnet test TicketingMundial.sln`: 84 exitosas.

## 2026-06-23 - QR dinámico y validación por funcionario

Funcionalidades:

- Token QR versionado `v1` con HMAC-SHA256, Base64Url, ventana temporal de 30 segundos y marca de propietario.
- Endpoint `/Entradas/QrDinamico/{idEntrada}` con respuesta JSON no cacheable.
- Detalle de entrada con QR, contador, renovación automática y pausa por pestaña oculta.
- Permiso temporal firmado para renovar QR durante 5 minutos sin consultar ni escribir en la base.
- Descarga del QR actual como PNG ya renderizado.
- Pantalla `/Funcionario/Escanear` con carga de imagen, cámara mediante `BarcodeDetector` y fallback manual.
- `POST /Funcionario/ValidarQr` con antiforgery, rate limiting y token como único dato confiable.
- Validación transaccional con `SELECT ... FOR UPDATE`, verificación de propietario actual y asignación en `FuncionarioEventoSector`.
- Inserción en `Validacion` guardando el token exacto y uso de triggers existentes para marcar `Entrada` como `VALIDADA`.

Pruebas:

- `dotnet build TicketingMundial.sln`: correcto, 0 warnings.
- `dotnet test TicketingMundial.sln`: 110 exitosas.

Limitación:

- Dispositivos autorizados queda parcial porque la base actual no contiene tabla o entidad para persistirlos.

Limitaciones:

- El entorno local tiene runtime .NET 10 y no .NET 8; para ejecutar tests se usó `DOTNET_ROLL_FORWARD=Major`.
- Historial de validaciones se mantiene en el alcance actual de funcionario/asignaciones sin pantalla nueva específica.

## 2026-06-24 - Revisión E2E de compra, QR y consumo irreversible

Funcionalidades/correcciones:

- `/Entradas/MisEntradas` y detalle muestran estado de entrada y estado de evento.
- El detalle muestra QR solo si la entrada está `ACTIVA` y el evento está `PROGRAMADO` o `EN_CURSO`.
- La acción de transferir se oculta y se bloquea en servidor si la entrada no está `ACTIVA` o el evento está cerrado.
- Aceptar una transferencia revalida dentro de transacción que la entrada siga `ACTIVA` y el evento no esté cerrado.
- Validar una entrada cancela transferencias `PENDIENTE` de esa entrada dentro de la misma transacción.
- Segundo escaneo y QR posterior a validación quedan rechazados.

Pruebas:

- `dotnet build TicketingMundial.sln`: correcto.
- `DOTNET_ROLL_FORWARD=Major dotnet test TicketingMundial.sln`: 113 exitosas.
- Smoke E2E real contra MySQL: compra creó entrada `ACTIVA`, QR generado, validación insertó una fila en `Validacion`, trigger dejó entrada `VALIDADA`, QR posterior y segundo escaneo rechazados, transferencia pendiente cancelada.

## 2026-06-22 - Corrección edición de eventos y asignaciones

Funcionalidades:

- Botón `Editar evento` en detalle administrativo solo cuando el evento permite edición estructural.
- Edición completa de evento sin entradas emitidas: fecha, hora, estadio, equipos, sectores, precios y estado.
- Bloqueo servidor para edición estructural cuando existen entradas emitidas.
- Bloqueo servidor para eventos `FINALIZADO` y `CANCELADO`.
- Actualización transaccional de `Evento`, `EventoLocal`, `EventoVisita` y `EventoSector`.
- Selector de eventos para funcionarios filtrado a `PROGRAMADO` y `EN_CURSO`.
- Endpoint `/Admin/Funcionarios/SectoresPorEvento` con sectores habilitados por `EventoSector`.
- Validación server-side de sector habilitado por evento y asignación duplicada.

Pruebas:

- `dotnet build TicketingMundial.sln`: correcto, 0 warnings.
- `DOTNET_ROLL_FORWARD=Major dotnet test TicketingMundial.sln`: 98 exitosas.

Limitaciones:

- La prueba manual autenticada depende de usuarios demo con roles cargados en la base.
