# Auditoria de seguridad SQL

Revision estatica de repositorios y acceso a base. No se detecto uso de ORM, Dapper, `DbContext`, `SELECT *` ni SQL construido con valores externos concatenados.

## EventoRepository

| Metodo | Operacion | Tablas/vistas | Parametros | Tipos | Origen | Controles | Resultado |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ObtenerEventosAsync | SELECT catalogo con filtros opcionales | V_Eventos | `@Desde`, `@Hasta`, `@IdEstadio`, `@Equipo` | DateTime, DateTime, UInt64, VarChar(54) | Query string MVC validado por binder | Fragmentos SQL constantes; LIKE escapado con `SqlSafety.EscapeLikePattern`; sin columnas enviadas por usuario | Seguro |
| ObtenerEventoAsync | SELECT detalle por id | V_Eventos | `@IdEvento` | UInt64 | Ruta MVC tipada | Parametro tipado | Seguro |
| ObtenerDisponibilidadAsync | SELECT sectores disponibles | V_DisponibilidadSectores | `@IdEvento` | UInt64 | Ruta MVC tipada | Parametro tipado | Seguro |
| ObtenerEstadiosAsync | SELECT lista de estadios | Estadio | Ninguno | - | - | Sin entrada externa | Seguro |

## UsuarioRepository

| Metodo | Operacion | Tablas/vistas | Parametros | Tipos | Origen | Controles | Resultado |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ObtenerParaAutenticacionAsync | SELECT usuario por correo y perfiles | Usuario, UsuarioGeneral, Funcionario, Administrador | `@CorreoElectronico` | VarChar(254) | Formulario de login, correo recortado | Parametro tipado; error publico generico | Seguro |
| ExisteCorreoAsync | SELECT existencia de correo | Usuario | `@CorreoElectronico` | VarChar(254) | Registro | Parametro tipado | Seguro |
| ExisteDocumentoAsync | SELECT existencia de clave compuesta | Usuario | `@TipoDocumento`, `@PaisDocumento`, `@Numero` | VarChar(20), VarChar(50), VarChar(30) | Registro | Parametros tipados | Seguro |
| RegistrarUsuarioGeneralAsync | INSERT transaccional | Usuario, UsuarioGeneral, TelefonoUsuario | Documento, datos personales, correo, hash, direccion, telefonos | VarChar segun columna | Registro validado en servidor | Transaccion unica; parametros tipados; rollback ante error; no loguea contrasena ni hash | Seguro |
| ActualizarHashContrasenaAsync | UPDATE hash por documento | Usuario | Documento, `@HashContrasena` | VarChar(20), VarChar(50), VarChar(30), VarChar(255) | Rehash tras login exitoso | Parametros tipados; no loguea hash | Seguro |
| AdminRepository | CRUD administrativo y alta de evento | Administrador, Estadio, Sector, Equipo, Evento, EventoLocal, EventoVisita, EventoSector | IDs, pais sede, filtros y datos de formularios | Tipados con MySqlParameter | Administracion | SQL parametrizado, LIKE escapado, paginacion con LIMIT/OFFSET parametrizados, transaccion para evento | Seguro |

## Health check

| Metodo | Operacion | Tablas/vistas | Parametros | Tipos | Origen | Controles | Resultado |
| --- | --- | --- | --- | --- | --- | --- | --- |
| MySqlHealthCheck.CheckHealthAsync | SELECT 1 | Ninguna | Ninguno | - | - | Consulta constante sin modificacion de base | Seguro |

## Controles transversales

- No se aceptan nombres de tablas, columnas, `WHERE` u `ORDER BY` desde el usuario.
- `SqlSafety.ResolveEventoOrderColumn` y `ResolveOrderDirection` proveen lista blanca para ordenamientos futuros.
- `SqlSafety.NormalizePagination` limita paginas futuras.
- Busquedas LIKE escapan `%`, `_` y `\`.
- Los valores maliciosos probados se tratan como texto y se entregan a repositorios/servicios como parametros, no como fragmentos SQL.
