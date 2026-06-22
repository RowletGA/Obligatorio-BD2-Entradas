# Módulo de administración

## Objetivo

Permitir que usuarios con rol `ADMINISTRADOR` gestionen estadios, sectores, equipos y eventos usando las tablas reales del obligatorio y respetando su país sede.

## Roles y seguridad

Todas las acciones están en `AdminController` con `[Authorize(Roles = "ADMINISTRADOR")]`. La identidad se obtiene desde claims y el país sede se consulta en `Administrador`; no se recibe desde el navegador como dato confiable.

## Navegación

El layout muestra para el perfil activo `ADMINISTRADOR`: Dashboard, Estadios, Sectores, Equipos, Eventos, Funcionarios y Reportes. Si el mismo usuario también es comprador o funcionario, debe cambiar de perfil desde el selector; no se mezclan opciones comerciales con opciones administrativas.

## Estadios

Usa `Estadio`: `IDEstadio`, `Nombre`, `UbicacionPais`, `UbicacionLocalidad`, `UbicacionCalle`, `UbicacionNumero`.

Funcionalidades: listado con búsqueda/paginación, alta, detalle y edición. El país se fija al `PaisSede` del administrador.

## Sectores

Usa `Sector`: `IDSector`, `NombreSector`, `Capacidad`, `IDEstadio`.

Funcionalidades: listado general, filtro por estadio, alta y edición. Se valida que el estadio pertenezca al país sede, capacidad mayor a cero y nombre no duplicado dentro del estadio.

## Equipos

Usa `Equipo`: `IDEquipo`, `Pais`, `Grupo`.

Funcionalidades: listado, alta y edición. El país sale del catálogo versionado de aplicación. Grupo es opcional y se ofrece A-L porque la columna existe.

## Eventos

Usa `Evento`, `EventoLocal`, `EventoVisita` y `EventoSector`.

El alta se realiza con una única transacción: se inserta el evento, luego equipo local, visitante y sectores con precio. Si falla cualquier paso se hace rollback.

En el formulario de evento, `Sector.Capacidad` se muestra solo como dato informativo. La capacidad pertenece al sector y no se edita al crear un evento.

El campo editable por sector es `EventoSector.PrecioBase`, presentado como “Precio por entrada”. Debe ser mayor a cero y solo es obligatorio cuando el sector está marcado como habilitado. El servidor vuelve a consultar sectores del estadio y no confía en capacidad ni sector enviados desde el navegador.

## Triggers relevantes

- `trg_evento_before_insert`: evita eventos en el mismo estadio dentro de una ventana de 2 horas.
- `trg_evento_before_update`: aplica la misma regla al editar.
- `trg_eventosector_before_insert`: exige que el sector pertenezca al estadio del evento.
- `trg_eventolocal_before_insert` y `trg_eventovisita_before_insert`: impiden equipo local igual a visitante y equipos jugando simultáneamente.

Los errores `SQLSTATE 45000` se muestran como mensaje funcional.

## Archivos principales

- `src/TicketingMundial.Web/Controllers/AdminController.cs`
- `src/TicketingMundial.Application/Services/AdminService.cs`
- `src/TicketingMundial.Infrastructure/Repositories/AdminRepository.cs`
- `src/TicketingMundial.Web/Views/Admin/*`
- `tests/TicketingMundial.Tests/AdminServiceTests.cs`

## Pruebas manuales

1. Iniciar sesión como administrador.
2. Abrir `/Admin`.
3. Crear estadio.
4. Crear sectores para ese estadio.
5. Crear equipos.
6. Crear evento con equipos distintos y sectores habilitados.
7. Confirmar que aparece en `/Eventos`.
8. Probar un conflicto horario para verificar mensaje del trigger.

## Extensiones futuras

Edición limitada de eventos con ventas, asignación de funcionarios, reportes reales y exportación CSV.
