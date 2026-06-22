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

Limitaciones:

- Administración real, compra, transferencias, validación y reportes siguen pendientes.
- La carga de datos demostrables de eventos depende de implementar administración o de datos autorizados.

Próximos pasos:

1. Implementar CRUD administrativo de estadios, sectores y equipos.
2. Implementar creación transaccional de eventos.
3. Implementar compra real de entradas con bloqueo y triggers.
4. Implementar área de usuario, transferencias, validación y reportes.

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

Limitaciones:

- No se implementó eliminación física.
- Funcionarios y reportes siguen pendientes.
- La prueba manual de creación real requiere un usuario administrador existente en la base remota.
