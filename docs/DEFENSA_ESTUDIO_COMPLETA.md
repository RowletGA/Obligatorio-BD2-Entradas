# Defensa del proyecto - guía de estudio

## 1. Mensaje principal

El proyecto es una aplicación ASP.NET Core MVC para gestionar eventos, ventas, transferencias y validación de entradas del Mundial 2026 sobre una base MySQL provista por la cátedra.

La solución respeta la base existente: no modifica los scripts originales, no usa ORM, no inventa tablas para resolver reglas obligatorias y mantiene a la base como autoridad final mediante claves, vistas, triggers y transacciones.

Frase corta para abrir la defensa:

> Implementamos un sistema MVC en capas, con autenticación por cookies, roles reales desde la base, SQL parametrizado, operaciones transaccionales y validación de entradas mediante QR dinámico firmado. La aplicación valida antes para mejorar la experiencia, pero la base sigue siendo la última barrera de consistencia.

## 2. Arquitectura general

La solución está separada en proyectos:

- `TicketingMundial.Domain`: constantes de roles, estados y tipos del dominio.
- `TicketingMundial.Application`: servicios, DTOs, validaciones y contratos.
- `TicketingMundial.Infrastructure`: acceso a MySQL, repositorios, seguridad QR, health check y traducción de errores.
- `TicketingMundial.Web`: controladores MVC, Razor Views, CSS, JavaScript y configuración.
- `TicketingMundial.Tests`: pruebas unitarias.

El flujo normal es:

```text
Razor View -> Controller -> Application Service -> Repository -> MySQL
```

Por qué lo hicimos así:

- Los controladores quedan livianos y no contienen SQL.
- Las reglas de aplicación quedan en servicios.
- El SQL queda concentrado en repositorios.
- La base conserva reglas fuertes con triggers, vistas, claves y transacciones.
- Es más fácil probar servicios, validadores y seguridad sin depender siempre de la interfaz.

## 3. Tecnologías usadas

- ASP.NET Core MVC con Razor Views.
- .NET 8.
- MySqlConnector para acceso manual a MySQL.
- Autenticación con cookies.
- `PasswordHasher` de ASP.NET Core para contraseñas.
- QRCoder para renderizar tokens como imagen QR.
- `BarcodeDetector` del navegador para leer QR desde cámara o imagen.
- Rate limiting de ASP.NET Core para login y validación QR.
- xUnit para pruebas.

Decisión importante:

No usamos Entity Framework ni Dapper porque el obligatorio está centrado en base de datos, SQL, vistas, triggers y transacciones. El acceso manual con parámetros deja explícito qué consulta se ejecuta y evita depender de migraciones automáticas.

## 4. Autenticación y roles

El login valida usuarios contra la base y emite claims en la cookie de autenticación.

Roles implementados:

- `USUARIO_GENERAL`
- `FUNCIONARIO`
- `ADMINISTRADOR`

Los roles reales se emiten como `ClaimTypes.Role`. Eso permite usar:

```csharp
[Authorize(Roles = RolesAplicacion.Administrador)]
```

Qué pasa si un usuario tiene más de un rol:

Se implementó `PerfilActivo`, que sirve para elegir navegación y dashboard. No reemplaza la autorización. Aunque el usuario elija un perfil visual, los endpoints siguen protegidos por roles reales.

Por qué es seguro:

- El permiso no depende de botones visibles.
- Cada ruta sensible tiene `[Authorize]`.
- La identidad operativa se toma desde claims, no desde campos ocultos enviados por el navegador.
- Las cookies son `HttpOnly` y `SameSite=Lax`.
- Los POST sensibles usan antiforgery.

Pregunta probable:

**¿El perfil activo puede darle permisos extra a alguien?**

Respuesta:

No. El perfil activo solo cambia la experiencia de navegación. Los permisos los define el conjunto real de roles emitidos en claims y los atributos `[Authorize(Roles = "...")]` en los controladores.

## 5. Contraseñas

Las contraseñas no se guardan en texto plano.

Se usa `PasswordHasher<UsuarioAutenticacion>`, el hasher estándar de ASP.NET Core. En login se compara la contraseña ingresada contra el hash almacenado.

También se rechazó el ingreso de usuarios que no tengan hash seguro, para no aceptar datos antiguos o incompletos.

Pregunta probable:

**¿Por qué no guardaron la contraseña cifrada?**

Respuesta:

Porque las contraseñas no deberían poder recuperarse. Se guardan con hash seguro, no con cifrado reversible. En login se verifica el hash.

## 6. SQL y seguridad contra inyección

El proyecto usa SQL manual, pero siempre con parámetros tipados mediante `MySqlParameter`.

No se concatenan valores del usuario dentro del SQL.

Ejemplo conceptual:

```text
WHERE TipoDocumento = @TipoDocumento
```

En lugar de:

```text
WHERE TipoDocumento = '" + valorUsuario + "'
```

Por qué es seguro:

- Los parámetros separan código SQL de datos.
- Evita SQL injection.
- Permite tipar valores como enteros, fechas, decimales o strings.
- Mantiene control explícito sobre cada consulta.

Pregunta probable:

**Si no usan ORM, cómo evitan SQL injection?**

Respuesta:

Usamos consultas parametrizadas con `MySqlParameter`, no concatenamos inputs del usuario. Además, la identidad sensible se toma desde claims de la sesión y no desde el formulario.

## 7. Transacciones

Las operaciones que modifican varias tablas o dependen de consistencia se hacen en transacción.

Ejemplos:

- Registro de usuario: `Usuario`, `UsuarioGeneral`, teléfonos.
- Creación de evento: evento, equipos local/visitante, sectores habilitados.
- Compra: venta y entradas.
- Validación QR: verificación de entrada y registro de validación.

Por qué importa:

Si una parte falla, se hace rollback y no queda la base en estado intermedio.

Pregunta probable:

**¿Qué pasa si se cae la operación a mitad de una compra?**

Respuesta:

La compra está envuelta en transacción. Si falla la creación de entradas, disponibilidad, trigger o cualquier insert relacionado, se revierte todo.

## 8. Relación con triggers y vistas

La aplicación no reemplaza las reglas de la base. Valida antes para dar mejores mensajes, pero los triggers siguen siendo autoridad final.

Vistas importantes:

- `V_Eventos`: catálogo de eventos.
- `V_DisponibilidadSectores`: disponibilidad por evento y sector.
- `V_PropietarioActual`: propietario real de una entrada considerando transferencias.
- `V_ValidacionEntrada`: información de validación.
- `V_ValidacionesPorFuncionario`: actividad de funcionarios.
- `V_RecaudacionPorEvento`: reportes.

Triggers importantes:

- Reglas de compra, capacidad y montos.
- Reglas de transferencia.
- Reglas de validación única.
- Cambios automáticos de estado cuando corresponde.

Pregunta probable:

**¿Por qué validan en aplicación si ya hay triggers?**

Respuesta:

Para dar una mejor experiencia y mensajes más claros antes de llegar a la base. Pero no confiamos solo en la aplicación: la base sigue teniendo la última palabra con triggers y constraints.

## 9. Compra de entradas

El usuario elige evento, sector y cantidad. El servidor vuelve a consultar disponibilidad y precio; no confía en precios enviados desde el navegador.

Reglas aplicadas:

- Máximo 5 entradas por venta.
- Cantidades positivas.
- Evento y sector existentes.
- Disponibilidad suficiente.
- Precio leído desde base, no desde el frontend.
- Operación transaccional.

Por qué es seguro:

Un usuario podría modificar el HTML o el request. Por eso el backend recalcula todo desde base antes de insertar.

Pregunta probable:

**¿Qué pasa si el usuario cambia el precio en el navegador?**

Respuesta:

No afecta la compra. El precio real se lee desde la base en el servidor. El navegador no decide precios ni montos finales.

## 10. Transferencias

La transferencia permite que un usuario entregue una entrada a otro usuario general.

La propiedad actual no se calcula a mano en el cliente. Se usa `V_PropietarioActual`, que refleja quién es el propietario vigente.

El QR se invalida al transferir porque la marca del propietario cambia. Un QR emitido para el propietario anterior deja de validar.

Pregunta probable:

**¿Cómo evitan que alguien use un QR viejo después de transferir la entrada?**

Respuesta:

El token QR incluye una marca criptográfica del propietario. Al validar, el servidor recalcula la marca usando el propietario actual desde `V_PropietarioActual`. Si cambió, el QR anterior se rechaza.

## 11. QR dinámico

El QR no es un ID simple, no es un GUID guardado en memoria y no es un JWT.

Es un token propio firmado con HMAC-SHA256.

Formato lógico:

```text
v1.<IDEntrada>.<IDEvento>.<VentanaTemporal>.<MarcaPropietario>.<Firma>
```

Componentes:

- `v1`: versión del token.
- `IDEntrada`: entrada que se quiere validar.
- `IDEvento`: evento asociado.
- `VentanaTemporal`: bloque de tiempo actual.
- `MarcaPropietario`: HMAC truncado del documento del propietario.
- `Firma`: HMAC-SHA256 sobre los componentes anteriores.

La imagen QR se genera con QRCoder en `EntradasController`. El QR contiene el token, no datos personales.

Duración:

- Por defecto vence cada 30 segundos.
- Hay una tolerancia corta de reloj configurable, actualmente 5 segundos.

Por qué es seguro:

- Si alteran el ID de entrada, la firma no coincide.
- Si alteran el evento, la firma no coincide.
- Si cambió el propietario, la marca no coincide.
- Si pasan más de 30 segundos, el token vence.
- No hay datos personales visibles en el QR.
- No se guardan tokens previos en base.

Pregunta probable:

**¿Por qué no usaron un QR estático?**

Respuesta:

Porque un QR estático se puede copiar y reutilizar más fácilmente. El QR dinámico vence rápido y además queda ligado a la entrada, al evento y al propietario actual.

Pregunta probable:

**¿Dónde se guarda el QR?**

Respuesta:

No se guarda como imagen ni como token permanente. Se genera bajo demanda. La única escritura ocurre cuando un funcionario valida correctamente, registrando el token validado en `Validacion`.

## 12. Renovación del QR

La pantalla de detalle de entrada llama a:

```text
GET /Entradas/QrDinamico/{idEntrada}
```

El servidor devuelve:

- imagen QR en Base64;
- vencimiento UTC;
- segundos restantes;
- permiso temporal de generación;
- indicador de si consultó la base.

Para no consultar la base cada 30 segundos, se implementó un `generationGrant`.

Qué es:

Un permiso temporal firmado, válido por 5 minutos, que permite regenerar QR para esa entrada y propietario sin volver a consultar la base en cada renovación.

Por qué sigue siendo seguro:

- El permiso también está firmado.
- Está asociado a entrada, evento y propietario.
- Tiene vencimiento.
- Si el permiso vence, se vuelve a consultar la base.
- Para validar el ingreso real, siempre se consulta la entrada y propietario actual.

Pregunta probable:

**¿Ese permiso permite validar una entrada sin consultar la base?**

Respuesta:

No. Solo permite regenerar la imagen QR durante unos minutos. La validación real del funcionario sí consulta la base, revisa estado, propietario actual y asignación del funcionario.

## 13. Validación por funcionario

El funcionario entra a:

```text
/Funcionario/Escanear
```

Tiene tres formas de ingresar el QR:

- cámara;
- carga de imagen;
- token manual como fallback.

La lectura de cámara o imagen se hace en el navegador con `BarcodeDetector`. La imagen no se envía al servidor; al servidor llega solo el token.

El POST de validación:

```text
POST /Funcionario/ValidarQr
```

Controles aplicados:

- Requiere rol `FUNCIONARIO`.
- Usa antiforgery.
- Tiene rate limiting.
- Valida formato y largo máximo.
- Abre transacción.
- Busca la entrada real.
- Valida firma, vencimiento y propietario actual.
- Verifica estado de entrada y evento.
- Verifica que el funcionario esté asignado a ese evento y sector.
- Inserta en `Validacion`.

Pregunta probable:

**¿Puede cualquier funcionario validar cualquier entrada?**

Respuesta:

No. Además de tener rol `FUNCIONARIO`, debe existir asignación en `FuncionarioEventoSector` para el evento y sector de esa entrada.

Pregunta probable:

**¿Qué pasa si escanean dos veces la misma entrada?**

Respuesta:

Se rechaza. La aplicación verifica estado y la base tiene reglas/triggers para impedir doble validación.

## 14. Rate limiting

Se configuró rate limiting para reducir abuso:

- Login: 5 intentos por minuto.
- Validación QR: 20 intentos por minuto por usuario/IP.

Esto no reemplaza la autorización ni la firma criptográfica, pero limita intentos repetidos.

Pregunta probable:

**¿El rate limiting es la seguridad principal del QR?**

Respuesta:

No. La seguridad principal es la firma HMAC, vencimiento, propietario actual, asignación del funcionario y validación transaccional. El rate limiting es una capa adicional contra abuso.

## 15. Administración

El administrador puede gestionar:

- estadios;
- sectores;
- equipos;
- eventos;
- funcionarios;
- asignaciones;
- reportes.

La jurisdicción se controla por el país sede del administrador.

Edición de eventos:

- Si el evento no tiene entradas emitidas, se puede editar estructura.
- Si ya tiene entradas, se bloquea edición estructural para no dejar entradas inconsistentes.
- En eventos con entradas o estados cerrados, se restringen cambios peligrosos.

Pregunta probable:

**¿Por qué no dejan editar libremente un evento con entradas vendidas?**

Respuesta:

Porque cambiar sectores, equipos, fecha o estructura podría invalidar entradas ya emitidas, compras y validaciones. Se protege la consistencia histórica.

## 16. Reportes

Se implementaron reportes básicos usando consultas a la base.

La idea es mostrar información administrativa sin duplicar lógica en memoria ni crear cálculos confiables en el frontend.

Pregunta probable:

**¿Los reportes salen del frontend?**

Respuesta:

No. El frontend solo muestra datos. Los cálculos y agregaciones salen desde consultas del backend contra la base.

## 17. Catálogos y registro

El registro usa `catalogos-registro.json` para países y tipos de documento.

Se validan:

- país;
- tipo de documento permitido;
- formato;
- longitud;
- teléfono.

Por qué se hizo configurable:

Permite ajustar países y tipos sin tocar código, manteniendo validación del lado servidor.

Pregunta probable:

**¿Qué pasa si desactivo JavaScript en el registro?**

Respuesta:

La validación principal está en servidor. JavaScript mejora la experiencia, pero el backend reconstruye y valida los datos permitidos.

## 18. Manejo de errores

Los errores técnicos de MySQL se traducen a mensajes funcionales cuando corresponde.

Ejemplo:

Si un trigger rechaza una operación, el usuario recibe un mensaje claro en lugar de un stack trace o error SQL crudo.

Por qué importa:

- Evita exponer detalles internos.
- Mejora la demo.
- Mantiene la base como autoridad sin empeorar la experiencia.

## 19. Qué implementamos como seguro

Lista rápida:

- Autorización por roles con `[Authorize]`.
- Claims para identidad autenticada.
- Perfil activo sin permisos extra.
- Cookies `HttpOnly`.
- Antiforgery en POST.
- SQL parametrizado.
- Transacciones en operaciones críticas.
- Password hashing, no texto plano.
- Rate limiting en login y QR.
- QR firmado con HMAC-SHA256.
- QR con vencimiento corto.
- QR ligado a propietario actual.
- Validación de funcionario por evento y sector.
- No confiar en precios ni documentos enviados desde el cliente.
- No exponer secretos en Git.
- No guardar datos personales dentro del QR.

## 20. Limitaciones conocidas

Conviene decirlas con tranquilidad si preguntan. Tener limitaciones claras suma credibilidad.

- Dispositivos autorizados está parcial: la base provista no modela dispositivos. Hoy se controla por sesión autenticada, rol funcionario y asignación evento-sector.
- No hay pasarela de pago real; la compra es funcional para el dominio del obligatorio.
- Reportes son básicos; no hay exportación avanzada ni gráficos.
- Algunas pruebas E2E reales dependen de datos demo existentes en la base.
- No se usa inventario persistente de dispositivos porque requeriría extender el modelo.

Respuesta recomendada:

> Preferimos no inventar tablas fuera del alcance de la base provista. Donde faltaba modelo persistente, documentamos la cobertura parcial y dejamos una mejora futura concreta.

## 21. Preguntas rápidas de defensa

**¿Qué capa conoce SQL?**

Los repositorios de Infrastructure.

**¿Los controladores acceden directo a la base?**

No. Pasan por servicios de Application.

**¿Cómo obtienen el usuario actual?**

Desde claims de la cookie autenticada.

**¿Confían en IDs/documentos enviados por formularios?**

No para identidad operativa. Se toma desde claims.

**¿Cómo evitan SQL injection?**

Con consultas parametrizadas y tipos `MySqlDbType`.

**¿Cómo evitan doble validación?**

Servicio, transacción, estado de entrada y reglas de base/triggers.

**¿Cómo saben quién es el propietario actual?**

Con `V_PropietarioActual`.

**¿Qué pasa si se transfiere una entrada?**

Cambia el propietario actual. Los QR emitidos antes quedan inválidos porque la marca de propietario ya no coincide.

**¿El QR contiene nombre, documento o correo?**

No. Contiene IDs técnicos, ventana temporal, marca HMAC del propietario y firma.

**¿Qué pasa si copian el PNG del QR?**

Solo sirve durante unos segundos. Después vence. Además, si cambia propietario o ya fue validado, se rechaza.

**¿Qué pasa si alteran el token manualmente?**

La firma HMAC deja de coincidir y se rechaza.

**¿Qué pasa si un funcionario no asignado escanea una entrada válida?**

Se rechaza por falta de asignación al evento y sector.

**¿Por qué no guardan todos los QR emitidos?**

No hace falta. La firma permite verificar autenticidad sin persistir cada token. Solo se guarda el token que efectivamente fue validado.

**¿Qué pasa si el navegador no soporta cámara o BarcodeDetector?**

Hay fallback manual. También se puede cargar imagen si el navegador soporta detección en imágenes.

**¿La imagen subida se guarda?**

No. Se procesa en el navegador y al servidor solo llega el token.

**¿Por qué usan Base64Url?**

Para que la firma y marcas viajen como texto seguro dentro del token, sin caracteres problemáticos.

**¿Cuál es la clave de firma?**

Se configura con `QrSecurity__SigningKey`. Debe tener al menos 32 bytes y no se versiona en Git.

**¿Qué pasa si falta la clave QR?**

El servicio no puede firmar correctamente y falla la configuración; no se acepta una clave placeholder insegura.

## 22. Demo sugerida

Preparar antes:

1. Tener un usuario general con entrada activa.
2. Tener un funcionario asignado al evento y sector.
3. Tener un administrador para mostrar asignaciones/reportes.
4. Configurar `QrSecurity__SigningKey`.
5. Ejecutar build y tests.

Orden recomendado:

1. Login.
2. Mostrar selección de perfil si aplica.
3. Mostrar catálogo de eventos.
4. Mostrar compra o entrada ya comprada.
5. Abrir detalle de entrada y mostrar QR dinámico.
6. Descargar QR actual.
7. En sesión de funcionario, cargar imagen y validar.
8. Intentar validar otra vez para mostrar rechazo por doble validación.
9. Explicar transferencia y propietario actual.
10. Mostrar panel admin o reportes.

## 23. Comandos útiles

```bash
dotnet restore TicketingMundial.sln
dotnet build TicketingMundial.sln
dotnet test TicketingMundial.sln
QrSecurity__SigningKey="$(openssl rand -base64 32)" ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/TicketingMundial.Web --urls http://localhost:5000
```

Si el entorno tiene un runtime mayor y falta .NET 8:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test TicketingMundial.sln
```

## 24. Archivos clave para mencionar

- `src/TicketingMundial.Web/Program.cs`: autenticación, autorización, rate limiting y pipeline.
- `src/TicketingMundial.Web/Controllers/AccountController.cs`: registro, login, perfil activo.
- `src/TicketingMundial.Web/Controllers/EntradasController.cs`: detalle de entradas y QR dinámico.
- `src/TicketingMundial.Web/Controllers/FuncionarioController.cs`: pantalla y POST de validación.
- `src/TicketingMundial.Application/Services/OperativaService.cs`: reglas de compra, QR, transferencia y validación.
- `src/TicketingMundial.Infrastructure/Repositories/OperativaRepository.cs`: SQL operativo y transacciones.
- `src/TicketingMundial.Infrastructure/Security/QrTokenService.cs`: generación y validación criptográfica del QR.
- `src/TicketingMundial.Web/Views/Entradas/Detalle.cshtml`: renovación automática del QR.
- `src/TicketingMundial.Web/Views/Funcionario/Escanear.cshtml`: cámara, carga de imagen y fallback manual.
- `tests/TicketingMundial.Tests/QrTokenServiceTests.cs`: pruebas de QR firmado, vencimiento, alteración y propietario.

## 25. Cierre recomendado

Para cerrar una explicación larga:

> La aplicación no intenta reemplazar a la base, sino trabajar con ella. El backend valida y mejora la experiencia; la base sostiene la consistencia. En seguridad, evitamos confiar en el cliente: usamos roles, claims, antiforgery, SQL parametrizado, transacciones y QR firmado con vencimiento corto. La validación final de entrada siempre cruza token, estado, propietario actual y asignación del funcionario.
