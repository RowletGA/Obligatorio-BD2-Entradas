# Contexto del proyecto

## 1. Resumen del proyecto

TicketingMundial es una aplicación ASP.NET Core MVC para el obligatorio de Base de Datos II. El objetivo es operar un sistema de venta, transferencia y validación de entradas para el Mundial 2026 usando la base MySQL provista por los scripts del curso.

Tecnologías y restricciones: .NET 8, Razor Views, autenticación por cookies, MySqlConnector, SQL manual parametrizado, sin Entity Framework, sin Dapper, sin ORM, sin migraciones automáticas y sin frameworks SPA.

Estado actual: compila, ejecuta pruebas, registra usuarios generales reales, inicia/cierra sesión, emite roles por claims, maneja perfil activo para usuarios con múltiples roles, muestra catálogo de eventos desde vistas, usa hash de contraseña y tiene registro parametrizable por catálogos de configuración.
También cuenta con módulo administrativo, compra, compras, entradas, transferencias, asignación de funcionarios, validación y reportes básicos.

## 2. Arquitectura

Proyectos:

- `TicketingMundial.Domain`: constantes de estados, roles y tipos básicos.
- `TicketingMundial.Application`: DTOs, contratos, servicios, validadores y reglas de aplicación.
- `TicketingMundial.Infrastructure`: MySqlConnector, repositorios, health check y traducción de errores MySQL.
- `TicketingMundial.Web`: MVC, controladores, Razor Views, CSS/JS y configuración.
- `TicketingMundial.Tests`: pruebas unitarias.

Flujo esperado: Controller -> Service -> Repository -> MySQL. El SQL debe vivir en repositorios de Infrastructure. Los controladores no deben contener SQL ni confiar en campos ocultos para identidad operativa. La identidad activa se obtiene desde claims.

La inyección se configura en `Program.cs`, `Application/DependencyInjection.cs` e `Infrastructure/DependencyInjection.cs`.

## 3. Base de datos

Schema remoto usado en desarrollo: `IC_Grupo4`. No publicar connection strings reales.

Los scripts originales están en `database/01_CreacionTablas (v2).sql`, `database/02_CreacionVistas (v2).sql` y `database/03_CreacionTriggers (v2).sql`. No modificarlos.

Script adicional manual: `database/04_AgregarHashContrasena.sql`, que agrega `Usuario.HashContrasena VARCHAR(255) NULL`.

Tablas principales: `Usuario`, `UsuarioGeneral`, `TelefonoUsuario`, `Administrador`, `Funcionario`, `Estadio`, `Sector`, `Equipo`, `Evento`, `EventoLocal`, `EventoVisita`, `EventoSector`, `Venta`, `Entrada`, `Transferencia`, `FuncionarioEventoSector`, `Validacion`.

Vistas usadas o previstas: `V_Eventos`, `V_DisponibilidadSectores`, `V_DetalleVentas`, `V_PropietarioActual`, `V_ValidacionEntrada`, `V_ValidacionesPorFuncionario`, `V_RecaudacionPorEvento`.

Estados válidos modelados en Domain: evento, venta, entrada, transferencia y usuario general. Las claves compuestas de usuario son `TipoDocumento`, `PaisDocumento`, `Numero`.

La base remota tiene también una columna `Usuario.Contrasena` no presente en los scripts originales; por compatibilidad se guarda allí el mismo hash seguro, nunca texto plano.

## 4. Autenticación

Registro: `/Account/Register` crea `Usuario`, `UsuarioGeneral` y `TelefonoUsuario` en una transacción. Valida documentos contra `CatalogosRegistro` y teléfonos con normalización servidor. El catálogo vive en `catalogos-registro.json`, tiene 80 países ISO alpha-2 y tipos `CI`, `DNI` y `PASAPORTE`.

Hash: `PasswordHasher<UsuarioAutenticacion>`. Login rechaza usuarios con `HashContrasena IS NULL`.

Cookies: esquema `TicketingMundial.Auth`, `HttpOnly`, `SameSite=Lax`, expiración deslizante. Claims: documento, correo, nombre, roles `USUARIO_GENERAL`, `FUNCIONARIO`, `ADMINISTRADOR` y, cuando corresponde, `PerfilActivo`.

`PerfilActivo` no reemplaza autorización. Solo decide navegación y dashboard. Todos los roles reales permanecen como `ClaimTypes.Role` y cada endpoint conserva `[Authorize(Roles = "...")]`.

## 5. Flujos implementados

Registro:

- Pantalla: `Views/Account/Register.cshtml`.
- Controlador: `AccountController`.
- Servicio: `IUsuarioService`, `ICatalogoRegistroService`.
- Repositorio: `UsuarioRepository`.
- Objetos DB: `Usuario`, `UsuarioGeneral`, `TelefonoUsuario`.
- Transacción: sí, en repositorio.

Login/logout:

- Pantallas: `Login`, navegación de layout.
- Controlador: `AccountController`.
- Servicio: `IAuthenticationService`.
- Repositorio: `UsuarioRepository`.

Catálogo de eventos:

- Pantallas: `Eventos/Index`, `Eventos/Details`.
- Servicio: `IEventoService`.
- Repositorio: `EventoRepository`.
- Vistas DB: `V_Eventos`, `V_DisponibilidadSectores`.

Administración:

- Pantallas: `Views/Admin/*`.
- Controlador: `AdminController`.
- Servicio: `IAdminService`.
- Repositorio: `AdminRepository`.
- Objetos DB: `Administrador`, `Estadio`, `Sector`, `Equipo`, `Evento`, `EventoLocal`, `EventoVisita`, `EventoSector`.
- Transacción: sí, para creación de evento.
- Jurisdicción: `PaisSede` leído desde `Administrador`.

Perfiles y navegación:

- Pantalla: `Views/Account/SeleccionarPerfil.cshtml`.
- Rutas: `/Account/SeleccionarPerfil`, `/Account/CambiarPerfil`.
- Helpers: `PerfilActivoExtensions`.
- Dashboard: redirige según `PerfilActivo`; si falta y hay múltiples roles, exige selección.

## 6. Reglas críticas

- Máximo 5 entradas por venta: implementado en formulario y servicio; triggers son autoridad final.
- Capacidad: debe validarse contra `EventoSector`, `Sector` y triggers.
- Comisión y monto total: no sobrescribir manualmente montos calculados por triggers.
- Equipos distintos y conflicto horario: validar en servicio y respetar triggers.
- Propiedad actual: usar `V_PropietarioActual`.
- Máximo 3 transferencias: triggers y servicio.
- Validación única: triggers y servicio.
- Autorización por país: el `PaisSede` del administrador debe leerse desde `Administrador`.

## 7. Seguridad

La aplicación usa consultas parametrizadas y no usa ORM. Hay antiforgery en POST sensibles, autorización por roles, claims para identidad activa, rate limiting en login, archivo local de secretos ignorado y mensajes funcionales MySQL traducidos.

No versionar `appsettings.Development.json`, contraseñas, hashes, documentos reales ni datos personales reales.

## 8. Cómo ejecutar

```bash
dotnet restore
dotnet build TicketingMundial.sln
dotnet test TicketingMundial.sln
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/TicketingMundial.Web --urls http://localhost:5000
```

URLs: `/`, `/health`, `/Eventos`, `/Account/Register`, `/Account/Login`, `/Dashboard`.

## 9. Datos de prueba

Usar correos y documentos claramente artificiales. No borrar datos ajenos ni limpiar tablas completas. Para pruebas sobre la base remota, preferir transacciones con rollback cuando sea posible.

## 10. Estado actual

| Módulo | Implementado | Parcial | Pendiente | Archivos principales | Observaciones |
| --- | --- | --- | --- | --- | --- |
| Registro | Sí | - | - | `AccountController`, `UsuarioService`, `CatalogoRegistroService` | Catálogos configurables y teléfonos normalizados |
| Login/logout | Sí | - | - | `AuthenticationService`, `UsuarioRepository` | Usuarios con hash NULL no ingresan |
| Eventos lectura | Sí | - | - | `EventosController`, `EventoRepository` | Depende de datos visibles en DB |
| Administración | Sí | Baja física no implementada | Edición avanzada de eventos | `AdminController`, `AdminService`, `AdminRepository` | Respeta país del administrador |
| Compra | Sí | Falta E2E real documentado | Medios de pago reales | `OperativaRepository` | No confía en precios del cliente |
| Compras/entradas | Sí | - | - | `OperativaRepository` | Usa claims y `V_PropietarioActual` |
| Transferencias | Sí | Falta E2E real documentado | Notificaciones | `OperativaRepository` | Triggers son autoridad final |
| Validación | Sí | Manual por ID/token demo | QR/cámara/dispositivos | `OperativaRepository` | Dispositivos autorizados no están modelados |
| Reportes | Sí | Básicos | Gráficos/exportación | `OperativaRepository` | No crea vistas nuevas |

## 11. Próximos pasos

1. Mantener datos demo controlados para defensa.
2. Agregar QR/cámara solo si se decide abordar opcionales.
3. Revisar UX fina si aparecen nuevos hallazgos manuales.
4. Actualizar este documento después de cada iteración.

## 12. Instrucciones para otra IA

Leer este archivo primero. No recrear la solución. No usar ORM, Dapper ni migraciones. No modificar scripts originales. No exponer secretos. No inventar tablas. Mantener consultas parametrizadas. Obtener identidad desde claims. Ejecutar build y tests. Actualizar este documento, `docs/GUIA_EQUIPO.md` y `docs/ESTADO_IMPLEMENTACION.md` al cerrar una iteración.

## Problemas conocidos y soluciones

El tipo de documento quedaba vacío porque el selector dependía de JavaScript y no siempre se reconstruía desde servidor. Se corrigió cargando país inicial `UY` en GET y recalculando tipos permitidos en POST inválido. Para agregar un país, editar `catalogos-registro.json` con código ISO alpha-2 único. Para agregar un tipo, editar `TiposDocumento`, definir `PaisesPermitidos`, `Patron`, `LongitudMaxima` y `Ayuda`, y ejecutar pruebas de catálogo.

## Instrucciones obligatorias para una IA

1. Leer todo este archivo.
2. Leer los scripts SQL.
3. No recrear la solución.
4. No introducir ORM.
5. No modificar scripts originales.
6. No inventar columnas.
7. Verificar nombres reales.
8. Usar transacciones.
9. Parametrizar SQL.
10. Ejecutar build y tests.
11. Actualizar documentación.
12. No exponer secretos.
13. No borrar datos remotos.
14. No afirmar que algo funciona sin probarlo.
