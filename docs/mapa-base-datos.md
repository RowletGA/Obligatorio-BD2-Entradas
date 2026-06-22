# Mapa de base de datos

La base `IC_Grupo4` existe previamente y es el contrato de datos de la aplicacion. La aplicacion no crea, modifica ni elimina objetos de base.

## Tablas

| Objeto | Tipo | Proposito | Claves principales | Relaciones | Casos de uso |
| --- | --- | --- | --- | --- | --- |
| Usuario | Tabla | Datos personales comunes a todos los perfiles. En esta iteracion recibe `HashContrasena` mediante `04_AgregarHashContrasena.sql`. | PK compuesta: TipoDocumento, PaisDocumento, Numero. UK CorreoElectronico. | Referenciada por UsuarioGeneral, Funcionario, Administrador y TelefonoUsuario. | Registro, login real, perfil, roles. |
| TelefonoUsuario | Tabla | Telefonos multiples de un usuario. | PK compuesta: TipoDocumento, PaisDocumento, NumeroDoc, Telefono. | FK a Usuario con cascade. | Registro y perfil. |
| UsuarioGeneral | Tabla | Perfil consumidor final. | PK compuesta: TipoDocumento, PaisDocumento, Numero. | FK a Usuario. Referenciada por Venta y Transferencia. | Compras, entradas propias, transferencias. |
| Funcionario | Tabla | Perfil operativo de validacion. | PK compuesta: TipoDocumento, PaisDocumento, Numero. UK NumLegajo. | FK a Usuario. Referenciada por FuncionarioEventoSector y Validacion. | Dashboard funcionario, validacion de entradas. |
| Administrador | Tabla | Perfil administrativo por pais sede. | PK compuesta: TipoDocumento, PaisDocumento, Numero. | FK a Usuario. | Dashboard administrador, gestion de infraestructura y eventos. |
| Estadio | Tabla | Recintos donde se disputan eventos. | PK IDEstadio. UK Nombre, UbicacionPais, UbicacionLocalidad. | Referenciada por Sector y Evento. | Catalogo, gestion de estadios, filtros. |
| Sector | Tabla | Sectores de cada estadio con capacidad. | PK IDSector. UK IDEstadio, NombreSector. | FK a Estadio. Referenciada por EventoSector, Entrada y FuncionarioEventoSector. | Disponibilidad, precios, gestion de sectores. |
| Evento | Tabla | Partido programado en un estadio. | PK IDEvento. | FK a Estadio. Referenciada por EventoSector, EventoLocal, EventoVisita. | Catalogo, detalle, gestion de eventos. |
| EventoSector | Tabla | Sectores habilitados y precio por evento. | PK compuesta: IDEvento, IDSector. | FK a Evento y Sector. Referenciada por Entrada y FuncionarioEventoSector. | Disponibilidad por sector, compra. |
| Equipo | Tabla | Equipos participantes. | PK IDEquipo. UK Pais, Grupo. | Referenciada por EventoLocal y EventoVisita. | Catalogo, filtros, asignacion de equipos. |
| EventoLocal | Tabla | Equipo local de un evento. | PK compuesta: IDEvento, IDEquipo. UK IDEvento. | FK a Evento y Equipo. | Detalle de evento, administracion. |
| EventoVisita | Tabla | Equipo visitante de un evento. | PK compuesta: IDEvento, IDEquipo. UK IDEvento. | FK a Evento y Equipo. | Detalle de evento, administracion. |
| FuncionarioEventoSector | Tabla | Asignacion de funcionarios a evento-sector. | PK compuesta: TipoDocumento, PaisDocumento, Numero, IDEvento, IDSector. | FK a Funcionario y EventoSector. | Validacion y auditoria. |
| Venta | Tabla | Compra de entradas por usuario general. | PK IDVenta. | FK compuesta a UsuarioGeneral. Referenciada por Entrada. | Compra, consulta de compras, reportes. |
| Entrada | Tabla | Boleto individual emitido por venta. | PK IDEntrada. | FK a Venta y EventoSector. Referenciada por Transferencia y Validacion. | Entradas asignadas, QR, transferencia, validacion. |
| Validacion | Tabla | Auditoria de ingreso validado. | PK IDValidacion. UK IDEntrada. | FK a Entrada y Funcionario. | Validacion unica, consultas de validaciones. |
| Transferencia | Tabla | Solicitudes e historial de traspaso de entrada. | PK IDTransferencia. | FK a UsuarioGeneral otorgante, UsuarioGeneral receptor y Entrada. | Transferencias enviadas, recibidas, aceptacion, rechazo. |

## Vistas

| Objeto | Tipo | Proposito | Claves principales | Relaciones | Casos de uso |
| --- | --- | --- | --- | --- | --- |
| V_Eventos | Vista | Eventos con estadio, equipo local y visitante. | IDEvento. | Evento, Estadio, EventoLocal, EventoVisita, Equipo. | Catalogo, detalle, filtros por estadio/equipo/fecha. |
| V_DisponibilidadSectores | Vista | Sectores habilitados con capacidad, precio y lugares disponibles. | IDEvento, IDSector. | EventoSector, Evento, Sector, Estadio, Entrada. | Detalle de evento, compra, disponibilidad. |
| V_DetalleVentas | Vista | Venta y entradas emitidas con datos de comprador, evento, sector y validacion. | IDVenta, IDEntrada. | Venta, UsuarioGeneral, Usuario, Entrada, Evento, Estadio, Sector, Validacion, Funcionario. | Mis compras, administracion de ventas. |
| V_RecaudacionPorEvento | Vista | Entradas vendidas y recaudacion por evento-sector. | IDEvento, IDSector. | EventoSector, Evento, Estadio, Sector, Entrada, equipos. | Reportes de ventas, eventos con mas entradas vendidas. |
| V_Transferencias | Vista | Historial legible de transferencias. | IDTransferencia. | Transferencia, Usuario, Entrada, Evento, Estadio, Sector. | Consulta de transferencias y auditoria. |
| V_ValidacionEntrada | Vista | Estado de una entrada y resultado preliminar de validacion. | IDEntrada. | Entrada, Evento, Estadio, Sector, equipos, Validacion, Funcionario. | Escaneo, consulta previa a validar. |
| V_PropietarioActual | Vista | Propietario vigente calculado desde venta y transferencias aceptadas. | IDEntrada. | Entrada, Venta, Transferencia. | Entradas asignadas y validacion de transferencias. |
| V_ValidacionesPorFuncionario | Vista | Conteo de validaciones por funcionario asignado. | IDEvento, IDSector, documento funcionario. | FuncionarioEventoSector, Evento, Sector, Funcionario, Usuario, Entrada, Validacion. | Reportes y dashboard funcionario. |

## Triggers

| Objeto | Tipo | Proposito | Claves principales | Relaciones | Casos de uso |
| --- | --- | --- | --- | --- | --- |
| trg_entrada_before_insert | Trigger | Limita a 5 entradas por venta y evita sobreventa de sector. | Entrada.IDVenta, Entrada.IDEvento, Entrada.IDSector. | Entrada, Sector. | Compra. |
| trg_entrada_after_insert_monto | Trigger | Recalcula monto total de venta al emitir entrada. | Entrada.IDVenta. | Entrada, Venta. | Compra. |
| trg_entrada_after_update_monto | Trigger | Recalcula monto ante cambios de entrada. | Entrada.IDVenta. | Entrada, Venta. | Administracion de ventas. |
| trg_entrada_after_delete_monto | Trigger | Recalcula monto al eliminar entrada. | Entrada.IDVenta. | Entrada, Venta. | Administracion. |
| trg_evento_before_insert | Trigger | Impide eventos en el mismo estadio dentro de 2 horas. | Evento.IDEstadio, FechaHora. | Evento. | Alta de eventos. |
| trg_evento_before_update | Trigger | Mantiene la regla de no superposicion al editar. | Evento.IDEvento. | Evento. | Edicion de eventos. |
| trg_eventosector_before_insert | Trigger | Exige que sector y evento pertenezcan al mismo estadio. | EventoSector.IDEvento, IDSector. | Evento, Sector. | Habilitar sectores. |
| trg_eventolocal_before_insert | Trigger | Evita local=visitante y conflictos horarios del equipo. | EventoLocal.IDEvento, IDEquipo. | EventoLocal, EventoVisita, Evento. | Asignar equipo local. |
| trg_eventovisita_before_insert | Trigger | Evita visitante=local y conflictos horarios del equipo. | EventoVisita.IDEvento, IDEquipo. | EventoVisita, EventoLocal, Evento. | Asignar equipo visitante. |
| trg_transferencia_before_insert | Trigger | Valida estado transferible, pendiente unica, maximo 3 aceptadas y propietario actual. | Transferencia.IDEntrada. | Transferencia, Entrada, V_PropietarioActual. | Crear transferencia. |
| trg_transferencia_before_update | Trigger | Revalida maximo de 3 transferencias aceptadas al aceptar. | Transferencia.IDTransferencia. | Transferencia. | Aceptar transferencia. |
| trg_validacion_before_insert | Trigger | Permite validar solo entradas ACTIVAS por funcionario asignado. | Validacion.IDEntrada y funcionario. | Validacion, Entrada, FuncionarioEventoSector. | Validacion de ingreso. |
| trg_validacion_after_insert | Trigger | Marca la entrada como VALIDADA luego del registro. | Validacion.IDEntrada. | Validacion, Entrada. | Validacion de ingreso. |

## Estados permitidos

| Entidad | Estados |
| --- | --- |
| UsuarioGeneral.EstadoVerificacion | PENDIENTE, VERIFICADO, RECHAZADO |
| Evento.EstadoEvento | PROGRAMADO, EN_CURSO, FINALIZADO, CANCELADO |
| Venta.EstadoVenta | pendiente, confirmada, paga |
| Entrada.EstadoEntrada | ACTIVA, VALIDADA, TRANSFERIDA, ANULADA |
| Transferencia.Estado | PENDIENTE, ACEPTADA, RECHAZADA, CANCELADA |
