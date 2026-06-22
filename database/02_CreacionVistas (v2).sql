-- ============================================================
-- Vistas - Sistema de venta y validación de entradas
-- Motor: MySQL 8.0+
-- Requiere ejecutar primero 01_tablas.sql
-- ============================================================

USE IC_Grupo4;

-- ------------------------------------------------------------
-- 1. Eventos con equipo local, visitante y estadio
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_Eventos AS
SELECT
    e.IDEvento,
    e.FechaHora,
    e.EstadoEvento,
    est.IDEstadio,
    est.Nombre AS Estadio,
    est.UbicacionPais AS PaisEstadio,
    est.UbicacionLocalidad AS LocalidadEstadio,
    eqLocal.IDEquipo AS IDEquipoLocal,
    eqLocal.Pais AS EquipoLocal,
    eqLocal.Grupo AS GrupoLocal,
    eqVisita.IDEquipo AS IDEquipoVisitante,
    eqVisita.Pais AS EquipoVisitante,
    eqVisita.Grupo AS GrupoVisitante
FROM Evento e
INNER JOIN Estadio est
    ON est.IDEstadio = e.IDEstadio
LEFT JOIN EventoLocal el
    ON el.IDEvento = e.IDEvento
LEFT JOIN Equipo eqLocal
    ON eqLocal.IDEquipo = el.IDEquipo
LEFT JOIN EventoVisita ev
    ON ev.IDEvento = e.IDEvento
LEFT JOIN Equipo eqVisita
    ON eqVisita.IDEquipo = ev.IDEquipo;

-- ------------------------------------------------------------
-- 2. Disponibilidad y precio por sector de cada evento
-- Las entradas anuladas no consumen capacidad.
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_DisponibilidadSectores AS
SELECT
    e.IDEvento,
    e.FechaHora,
    e.EstadoEvento,
    est.IDEstadio,
    est.Nombre AS Estadio,
    s.IDSector,
    s.NombreSector,
    s.Capacidad,
    es.PrecioBase,
    COUNT(en.IDEntrada) AS EntradasEmitidas,
    GREATEST(s.Capacidad - COUNT(en.IDEntrada), 0) AS LugaresDisponibles
FROM EventoSector es
INNER JOIN Evento e
    ON e.IDEvento = es.IDEvento
INNER JOIN Sector s
    ON s.IDSector = es.IDSector
INNER JOIN Estadio est
    ON est.IDEstadio = e.IDEstadio
LEFT JOIN Entrada en
    ON en.IDEvento = es.IDEvento
   AND en.IDSector = es.IDSector
   AND en.EstadoEntrada <> 'ANULADA'
GROUP BY
    e.IDEvento,
    e.FechaHora,
    e.EstadoEvento,
    est.IDEstadio,
    est.Nombre,
    s.IDSector,
    s.NombreSector,
    s.Capacidad,
    es.PrecioBase;

-- ------------------------------------------------------------
-- 3. Detalle de ventas y sus entradas
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_DetalleVentas AS
SELECT
    v.IDVenta,
    v.FechaVenta,
    v.EstadoVenta,
    v.MontoTotal,
    v.PorcentajeComision,
    CONCAT_WS(' ', uc.PrimerNombre, uc.PrimerApellido) AS UsuarioComprador,
    CONCAT(uc.TipoDocumento, ' ', uc.Numero) AS DocumentoComprador,
    en.IDEntrada,
    en.EstadoEntrada,
    en.FechaEmision,
    en.Costo,
    ev.IDEvento,
    ev.FechaHora AS FechaEvento,
    est.Nombre AS Estadio,
    s.IDSector,
    s.NombreSector,
    val.FechaHoraValidacion,
    CONCAT_WS(' ', uv.PrimerNombre, uv.PrimerApellido) AS FuncionarioValidador,
    fv.NumLegajo AS LegajoValidador
FROM Venta v
INNER JOIN UsuarioGeneral ug
    ON ug.TipoDocumento = v.TipoDocUsuario
   AND ug.PaisDocumento = v.PaisDocUsuario
   AND ug.Numero = v.NumeroUsuario
INNER JOIN Usuario uc
    ON uc.TipoDocumento = ug.TipoDocumento
   AND uc.PaisDocumento = ug.PaisDocumento
   AND uc.Numero = ug.Numero
INNER JOIN Entrada en
    ON en.IDVenta = v.IDVenta
INNER JOIN Evento ev
    ON ev.IDEvento = en.IDEvento
INNER JOIN Estadio est
    ON est.IDEstadio = ev.IDEstadio
INNER JOIN Sector s
    ON s.IDSector = en.IDSector
LEFT JOIN Validacion val
    ON val.IDEntrada = en.IDEntrada
LEFT JOIN Funcionario fv
    ON fv.TipoDocumento = val.TipoDocFuncionario
   AND fv.PaisDocumento = val.PaisDocFuncionario
   AND fv.Numero = val.NumeroFuncionario
LEFT JOIN Usuario uv
    ON uv.TipoDocumento = fv.TipoDocumento
   AND uv.PaisDocumento = fv.PaisDocumento
   AND uv.Numero = fv.Numero;

-- ------------------------------------------------------------
-- 4. Recaudación por evento y sector
-- Incluye sectores sin entradas vendidas.
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_RecaudacionPorEvento AS
SELECT
    ev.IDEvento,
    ev.FechaHora,
    ev.EstadoEvento,
    est.Nombre AS Estadio,
    eqLocal.Pais AS EquipoLocal,
    eqVisita.Pais AS EquipoVisitante,
    s.IDSector,
    s.NombreSector,
    COUNT(en.IDEntrada) AS EntradasVendidas,
    COALESCE(SUM(en.Costo), 0.00) AS RecaudacionSector
FROM EventoSector es
INNER JOIN Evento ev
    ON ev.IDEvento = es.IDEvento
INNER JOIN Estadio est
    ON est.IDEstadio = ev.IDEstadio
INNER JOIN Sector s
    ON s.IDSector = es.IDSector
LEFT JOIN Entrada en
    ON en.IDEvento = es.IDEvento
   AND en.IDSector = es.IDSector
   AND en.EstadoEntrada <> 'ANULADA'
LEFT JOIN EventoLocal el
    ON el.IDEvento = ev.IDEvento
LEFT JOIN Equipo eqLocal
    ON eqLocal.IDEquipo = el.IDEquipo
LEFT JOIN EventoVisita evt
    ON evt.IDEvento = ev.IDEvento
LEFT JOIN Equipo eqVisita
    ON eqVisita.IDEquipo = evt.IDEquipo
GROUP BY
    ev.IDEvento,
    ev.FechaHora,
    ev.EstadoEvento,
    est.Nombre,
    eqLocal.Pais,
    eqVisita.Pais,
    s.IDSector,
    s.NombreSector;

-- ------------------------------------------------------------
-- 5. Historial y estado de las transferencias
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_Transferencias AS
SELECT
    t.IDTransferencia,
    t.IDEntrada,
    t.FechaSolicitud,
    t.FechaRespuesta,
    t.Estado AS EstadoTransferencia,
    CONCAT_WS(' ', uOtorga.PrimerNombre, uOtorga.PrimerApellido) AS UsuarioOtorga,
    uOtorga.CorreoElectronico AS CorreoOtorga,
    CONCAT(t.TipoDocOtorga, ' ', t.NumeroOtorga) AS DocumentoOtorga,
    CONCAT_WS(' ', uRecibe.PrimerNombre, uRecibe.PrimerApellido) AS UsuarioRecibe,
    uRecibe.CorreoElectronico AS CorreoRecibe,
    CONCAT(t.TipoDocRecibe, ' ', t.NumeroRecibe) AS DocumentoRecibe,
    en.EstadoEntrada,
    ev.IDEvento,
    ev.FechaHora AS FechaEvento,
    est.Nombre AS Estadio,
    s.NombreSector
FROM Transferencia t
INNER JOIN Usuario uOtorga
    ON uOtorga.TipoDocumento = t.TipoDocOtorga
   AND uOtorga.PaisDocumento = t.PaisDocOtorga
   AND uOtorga.Numero = t.NumeroOtorga
INNER JOIN Usuario uRecibe
    ON uRecibe.TipoDocumento = t.TipoDocRecibe
   AND uRecibe.PaisDocumento = t.PaisDocRecibe
   AND uRecibe.Numero = t.NumeroRecibe
INNER JOIN Entrada en
    ON en.IDEntrada = t.IDEntrada
INNER JOIN Evento ev
    ON ev.IDEvento = en.IDEvento
INNER JOIN Estadio est
    ON est.IDEstadio = ev.IDEstadio
INNER JOIN Sector s
    ON s.IDSector = en.IDSector;

-- ------------------------------------------------------------
-- 6. Información necesaria para validar una entrada
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_ValidacionEntrada AS
SELECT
    en.IDEntrada,
    en.EstadoEntrada,
    en.FechaEmision,
    en.Costo,
    val.FechaHoraValidacion,
    ev.IDEvento,
    ev.FechaHora AS FechaEvento,
    ev.EstadoEvento,
    est.Nombre AS Estadio,
    est.UbicacionLocalidad AS LocalidadEstadio,
    s.IDSector,
    s.NombreSector,
    eqLocal.Pais AS EquipoLocal,
    eqVisita.Pais AS EquipoVisitante,
    CONCAT_WS(' ', uv.PrimerNombre, uv.PrimerApellido) AS FuncionarioValidador,
    fv.NumLegajo AS LegajoValidador,
    CASE
        WHEN en.EstadoEntrada = 'ANULADA' THEN 'ENTRADA_ANULADA'
        WHEN en.EstadoEntrada = 'VALIDADA' THEN 'ENTRADA_YA_VALIDADA'
        WHEN ev.EstadoEvento = 'CANCELADO' THEN 'EVENTO_CANCELADO'
        WHEN ev.EstadoEvento = 'FINALIZADO' THEN 'EVENTO_FINALIZADO'
        WHEN en.EstadoEntrada = 'ACTIVA'
             AND ev.EstadoEvento IN ('PROGRAMADO', 'EN_CURSO') THEN 'VALIDA'
        ELSE 'NO_VALIDA'
    END AS ResultadoValidacion
FROM Entrada en
INNER JOIN Evento ev
    ON ev.IDEvento = en.IDEvento
INNER JOIN Estadio est
    ON est.IDEstadio = ev.IDEstadio
INNER JOIN Sector s
    ON s.IDSector = en.IDSector
LEFT JOIN EventoLocal el
    ON el.IDEvento = ev.IDEvento
LEFT JOIN Equipo eqLocal
    ON eqLocal.IDEquipo = el.IDEquipo
LEFT JOIN EventoVisita evt
    ON evt.IDEvento = ev.IDEvento
LEFT JOIN Equipo eqVisita
    ON eqVisita.IDEquipo = evt.IDEquipo
LEFT JOIN Validacion val
    ON val.IDEntrada = en.IDEntrada
LEFT JOIN Funcionario fv
    ON fv.TipoDocumento = val.TipoDocFuncionario
   AND fv.PaisDocumento = val.PaisDocFuncionario
   AND fv.Numero = val.NumeroFuncionario
LEFT JOIN Usuario uv
    ON uv.TipoDocumento = fv.TipoDocumento
   AND uv.PaisDocumento = fv.PaisDocumento
   AND uv.Numero = fv.Numero;

-- ------------------------------------------------------------
-- 7. Propietario actual de cada entrada
-- Se calcula sin redundancia: si existe una transferencia
-- ACEPTADA, el dueño es el último receptor (mayor IDTransferencia
-- entre las aceptadas); si no, el dueño es el comprador original.
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_PropietarioActual AS
SELECT
    en.IDEntrada,
    COALESCE(ultima.TipoDocRecibe, v.TipoDocUsuario) AS TipoDocumento,
    COALESCE(ultima.PaisDocRecibe, v.PaisDocUsuario) AS PaisDocumento,
    COALESCE(ultima.NumeroRecibe, v.NumeroUsuario) AS Numero
FROM Entrada en
INNER JOIN Venta v
    ON v.IDVenta = en.IDVenta
LEFT JOIN (
    SELECT t1.IDEntrada, t1.TipoDocRecibe, t1.PaisDocRecibe, t1.NumeroRecibe
    FROM Transferencia t1
    INNER JOIN (
        SELECT IDEntrada, MAX(IDTransferencia) AS UltimaID
        FROM Transferencia
        WHERE Estado = 'ACEPTADA'
        GROUP BY IDEntrada
    ) ult
        ON ult.IDEntrada = t1.IDEntrada
       AND ult.UltimaID = t1.IDTransferencia
) ultima
    ON ultima.IDEntrada = en.IDEntrada;

-- ------------------------------------------------------------
-- 8. Validaciones realizadas por cada funcionario asignado
-- Por cada combinación Funcionario+Evento+Sector asignada,
-- cuenta cuántas entradas validó. Si no validó ninguna, la fila
-- queda con el conteo en 0 (no se excluye). Pensada para un
-- ranking de "top validadores", no para cazar ausentes.
-- ------------------------------------------------------------
CREATE OR REPLACE VIEW V_ValidacionesPorFuncionario AS
SELECT
    fs.IDEvento,
    ev.FechaHora AS FechaEvento,
    fs.IDSector,
    sec.NombreSector,
    fs.TipoDocumento,
    fs.PaisDocumento,
    fs.Numero,
    CONCAT_WS(' ', u.PrimerNombre, u.PrimerApellido) AS Funcionario,
    fu.NumLegajo,
    COUNT(val.IDValidacion) AS EntradasValidadas
FROM FuncionarioEventoSector fs
INNER JOIN Evento ev
    ON ev.IDEvento = fs.IDEvento
INNER JOIN Sector sec
    ON sec.IDSector = fs.IDSector
INNER JOIN Funcionario fu
    ON fu.TipoDocumento = fs.TipoDocumento
   AND fu.PaisDocumento = fs.PaisDocumento
   AND fu.Numero = fs.Numero
INNER JOIN Usuario u
    ON u.TipoDocumento = fu.TipoDocumento
   AND u.PaisDocumento = fu.PaisDocumento
   AND u.Numero = fu.Numero
LEFT JOIN Entrada en
    ON en.IDEvento = fs.IDEvento
   AND en.IDSector = fs.IDSector
LEFT JOIN Validacion val
    ON val.IDEntrada = en.IDEntrada
   AND val.TipoDocFuncionario = fs.TipoDocumento
   AND val.PaisDocFuncionario = fs.PaisDocumento
   AND val.NumeroFuncionario = fs.Numero
GROUP BY
    fs.IDEvento, ev.FechaHora, fs.IDSector, sec.NombreSector,
    fs.TipoDocumento, fs.PaisDocumento, fs.Numero, Funcionario, fu.NumLegajo
ORDER BY EntradasValidadas DESC;
