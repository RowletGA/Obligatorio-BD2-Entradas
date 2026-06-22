# Matriz de requerimientos

| Requerimiento del obligatorio | Pantalla | Controlador | Servicio | Repositorio | Tablas utilizadas | Vistas utilizadas | Triggers relacionados | Estado de implementacion |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Registro de usuarios con telefonos | Account/Register | AccountController | IUsuarioService, ICatalogoRegistroService | UsuarioRepository | Usuario, TelefonoUsuario, UsuarioGeneral | - | Checks de Usuario y UsuarioGeneral | Implementado con transaccion, hash, catalogos configurables y telefonos normalizados |
| Inicio/cierre de sesion y perfiles | Account/Login, Logout | AccountController | IAuthenticationService, IPasswordService | UsuarioRepository | Usuario, UsuarioGeneral, Funcionario, Administrador | - | - | Implementado con correo, contrasena, hash y cookies |
| Catalogo de eventos | Eventos/Index | EventosController | IEventoService | EventoRepository | Evento, Estadio, Equipo, EventoLocal, EventoVisita | V_Eventos | - | Implementado |
| Filtros por fecha, estadio y equipo | Eventos/Index | EventosController | IEventoService | EventoRepository | Evento, Estadio, Equipo | V_Eventos | - | Implementado |
| Detalle de evento | Eventos/Details | EventosController | IEventoService | EventoRepository | Evento, Estadio, Equipo | V_Eventos | - | Implementado |
| Disponibilidad por sector | Eventos/Details | EventosController | IEventoService | EventoRepository | EventoSector, Sector, Entrada | V_DisponibilidadSectores | trg_entrada_before_insert | Implementado |
| Compra de entradas | Eventos/Details, Compras/Confirmar | ComprasController | IOperativaService | OperativaRepository | Venta, Entrada, EventoSector | V_DisponibilidadSectores | trg_entrada_before_insert, trg_entrada_after_insert_monto | Implementado |
| Consulta de compras | Compras | ComprasController | IOperativaService | OperativaRepository | Venta, Entrada | - | - | Implementado |
| Entradas actualmente asignadas | Entradas/MisEntradas | EntradasController | IOperativaService | OperativaRepository | Entrada, Venta, Transferencia | V_PropietarioActual | trg_transferencia_before_insert | Implementado |
| Crear transferencia | Entradas/Transferir | TransferenciasController | IOperativaService | OperativaRepository | Transferencia, Entrada | V_PropietarioActual | trg_transferencia_before_insert | Implementado |
| Aceptar/rechazar transferencia | Transferencias | TransferenciasController | IOperativaService | OperativaRepository | Transferencia | V_Transferencias | trg_transferencia_before_update | Implementado |
| Gestion de estadios | Admin/Estadios | AdminController | IAdminService | AdminRepository | Estadio, Sector, Evento | - | FK Sector_Estadio | Implementado sin baja fisica |
| Gestion de sectores | Admin/Sectores | AdminController | IAdminService | AdminRepository | Sector, Estadio | - | CHK_Sector_Capacidad | Implementado |
| Gestion de eventos | Admin/Eventos | AdminController | IAdminService | AdminRepository | Evento, EventoLocal, EventoVisita, EventoSector | V_Eventos | trg_evento_before_insert, trg_evento_before_update | Implementado alta y cambio de estado |
| Asignacion de equipos | Admin/Equipos, Admin/Eventos | AdminController | IAdminService | AdminRepository | Equipo, EventoLocal, EventoVisita | V_Eventos | trg_eventolocal_before_insert, trg_eventovisita_before_insert | Implementado |
| Asignacion de sectores a eventos | Admin/Eventos/Nuevo | AdminController | IAdminService | AdminRepository | EventoSector, Sector | V_DisponibilidadSectores | trg_eventosector_before_insert | Implementado en alta de evento |
| Asignacion de funcionarios | Admin/Funcionarios | AdminController | IOperativaService | OperativaRepository | FuncionarioEventoSector | V_ValidacionesPorFuncionario | trg_validacion_before_insert | Implementado |
| Validacion de entradas | Funcionario/Validar | FuncionarioController | IOperativaService | OperativaRepository | Validacion, Entrada | V_ValidacionEntrada | trg_validacion_before_insert, trg_validacion_after_insert | Implementado |
| Consulta de transferencias | Pendiente | Pendiente | ITransferenciaService | ITransferenciaRepository | Transferencia | V_Transferencias | - | Interfaz reservada |
| Consulta de validaciones | Pendiente | Pendiente | IValidacionService | IValidacionRepository | Validacion | V_ValidacionesPorFuncionario | - | Interfaz reservada |
| Ranking de mayores compradores | Admin/Reportes | ReportesController | IOperativaService | OperativaRepository | Usuario, Venta, Entrada | - | - | Implementado básico |
| Eventos con mas entradas vendidas | Admin/Reportes | ReportesController | IOperativaService | OperativaRepository | Evento, Entrada | - | - | Implementado básico |
