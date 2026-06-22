-- ============================================================
-- Triggers - Sistema de venta y validación de entradas
-- Motor: MySQL 8.0+
--
-- Requiere ejecutar antes, EN ESTE ORDEN:
--   1. 01_tablas.sql
--   2. 02_vistas.sql   (el trigger de Transferencia usa V_PropietarioActual)
--
-- IMPORTANTE: crear triggers requiere el privilegio TRIGGER.
-- Es un permiso distinto al de CREATE VIEW que ya te fue denegado
-- una vez -- si este script falla con un error de permisos
-- parecido, hay que pedirle también este privilegio al
-- administrador del servidor antes de continuar.
-- ============================================================

USE IC_Grupo4;

DELIMITER $$


-- ============================================================
-- 1. ENTRADA - antes de insertar
--    (a) Una venta no puede tener más de 5 entradas asociadas.
--    (b) No se puede superar la capacidad del sector contratado
--        para ese evento (evita sobreventa).
-- ============================================================
CREATE TRIGGER trg_entrada_before_insert
BEFORE INSERT ON Entrada
FOR EACH ROW
BEGIN
    DECLARE v_entradas_venta  INT;
    DECLARE v_entradas_sector INT;
    DECLARE v_capacidad       INT;

    SELECT COUNT(*) INTO v_entradas_venta
    FROM Entrada
    WHERE IDVenta = NEW.IDVenta;

    IF v_entradas_venta >= 5 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Una venta no puede tener más de 5 entradas asociadas.';
    END IF;

    SELECT Capacidad INTO v_capacidad
    FROM Sector
    WHERE IDSector = NEW.IDSector;

    SELECT COUNT(*) INTO v_entradas_sector
    FROM Entrada
    WHERE IDEvento = NEW.IDEvento
      AND IDSector = NEW.IDSector
      AND EstadoEntrada <> 'ANULADA';

    IF v_entradas_sector >= v_capacidad THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'No hay lugares disponibles en este sector para este evento.';
    END IF;
END$$


-- ============================================================
-- 2. ENTRADA - después de insertar / actualizar / eliminar
--    Recalcula Venta.MontoTotal = SUM(Costo) * (1 + comisión/100),
--    usando la comisión propia de esa venta (no una tasa global),
--    para que ventas viejas conserven su tasa histórica.
-- ============================================================
CREATE TRIGGER trg_entrada_after_insert_monto
AFTER INSERT ON Entrada
FOR EACH ROW
BEGIN
    DECLARE v_subtotal DECIMAL(12,2);
    DECLARE v_comision DECIMAL(5,2);

    SELECT COALESCE(SUM(Costo), 0) INTO v_subtotal
    FROM Entrada
    WHERE IDVenta = NEW.IDVenta;

    SELECT PorcentajeComision INTO v_comision
    FROM Venta
    WHERE IDVenta = NEW.IDVenta;

    UPDATE Venta
    SET MontoTotal = ROUND(v_subtotal * (1 + v_comision / 100), 2)
    WHERE IDVenta = NEW.IDVenta;
END$$

CREATE TRIGGER trg_entrada_after_update_monto
AFTER UPDATE ON Entrada
FOR EACH ROW
BEGIN
    DECLARE v_subtotal DECIMAL(12,2);
    DECLARE v_comision DECIMAL(5,2);

    SELECT COALESCE(SUM(Costo), 0) INTO v_subtotal
    FROM Entrada
    WHERE IDVenta = NEW.IDVenta;

    SELECT PorcentajeComision INTO v_comision
    FROM Venta
    WHERE IDVenta = NEW.IDVenta;

    UPDATE Venta
    SET MontoTotal = ROUND(v_subtotal * (1 + v_comision / 100), 2)
    WHERE IDVenta = NEW.IDVenta;

    -- Si la entrada cambió de venta, recalcular también la venta anterior
    IF NEW.IDVenta <> OLD.IDVenta THEN
        SELECT COALESCE(SUM(Costo), 0) INTO v_subtotal
        FROM Entrada
        WHERE IDVenta = OLD.IDVenta;

        SELECT PorcentajeComision INTO v_comision
        FROM Venta
        WHERE IDVenta = OLD.IDVenta;

        UPDATE Venta
        SET MontoTotal = ROUND(v_subtotal * (1 + v_comision / 100), 2)
        WHERE IDVenta = OLD.IDVenta;
    END IF;
END$$

CREATE TRIGGER trg_entrada_after_delete_monto
AFTER DELETE ON Entrada
FOR EACH ROW
BEGIN
    DECLARE v_subtotal DECIMAL(12,2);
    DECLARE v_comision DECIMAL(5,2);

    SELECT COALESCE(SUM(Costo), 0) INTO v_subtotal
    FROM Entrada
    WHERE IDVenta = OLD.IDVenta;

    SELECT PorcentajeComision INTO v_comision
    FROM Venta
    WHERE IDVenta = OLD.IDVenta;

    UPDATE Venta
    SET MontoTotal = ROUND(v_subtotal * (1 + v_comision / 100), 2)
    WHERE IDVenta = OLD.IDVenta;
END$$


-- ============================================================
-- 3. EVENTO - antes de insertar / actualizar
--    No puede haber dos eventos en el mismo estadio dentro de
--    una ventana de 2 horas (no solo a la misma fecha y hora
--    exacta). Los eventos cancelados no bloquean el horario.
-- ============================================================
CREATE TRIGGER trg_evento_before_insert
BEFORE INSERT ON Evento
FOR EACH ROW
BEGIN
    DECLARE v_conflictos INT;

    SELECT COUNT(*) INTO v_conflictos
    FROM Evento
    WHERE IDEstadio = NEW.IDEstadio
      AND EstadoEvento <> 'CANCELADO'
      AND ABS(TIMESTAMPDIFF(MINUTE, FechaHora, NEW.FechaHora)) < 120;

    IF v_conflictos > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Ya existe otro evento en este estadio dentro de la ventana de 2 horas.';
    END IF;
END$$

CREATE TRIGGER trg_evento_before_update
BEFORE UPDATE ON Evento
FOR EACH ROW
BEGIN
    DECLARE v_conflictos INT;

    SELECT COUNT(*) INTO v_conflictos
    FROM Evento
    WHERE IDEstadio = NEW.IDEstadio
      AND IDEvento <> NEW.IDEvento
      AND EstadoEvento <> 'CANCELADO'
      AND ABS(TIMESTAMPDIFF(MINUTE, FechaHora, NEW.FechaHora)) < 120;

    IF v_conflictos > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Ya existe otro evento en este estadio dentro de la ventana de 2 horas.';
    END IF;
END$$


-- ============================================================
-- 4. EVENTOSECTOR - antes de insertar
--    El sector habilitado debe pertenecer al mismo estadio
--    que el evento que lo habilita.
-- ============================================================
CREATE TRIGGER trg_eventosector_before_insert
BEFORE INSERT ON EventoSector
FOR EACH ROW
BEGIN
    DECLARE v_estadio_evento BIGINT UNSIGNED;
    DECLARE v_estadio_sector BIGINT UNSIGNED;

    SELECT IDEstadio INTO v_estadio_evento FROM Evento WHERE IDEvento = NEW.IDEvento;
    SELECT IDEstadio INTO v_estadio_sector FROM Sector WHERE IDSector = NEW.IDSector;

    IF v_estadio_evento <> v_estadio_sector THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'El sector debe pertenecer al mismo estadio que el evento.';
    END IF;
END$$


-- ============================================================
-- 5. EVENTOLOCAL / EVENTOVISITA - antes de insertar
--    (a) El equipo local y el visitante no pueden ser el mismo.
--    (b) Un equipo no puede participar de dos eventos cuya
--        diferencia horaria sea menor a 2 horas, aunque sean
--        en estadios distintos.
-- ============================================================
CREATE TRIGGER trg_eventolocal_before_insert
BEFORE INSERT ON EventoLocal
FOR EACH ROW
BEGIN
    DECLARE v_fecha_evento DATETIME;
    DECLARE v_conflictos   INT;

    IF EXISTS (
        SELECT 1 FROM EventoVisita
        WHERE IDEvento = NEW.IDEvento AND IDEquipo = NEW.IDEquipo
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'El equipo local y visitante no pueden ser el mismo equipo.';
    END IF;

    SELECT FechaHora INTO v_fecha_evento FROM Evento WHERE IDEvento = NEW.IDEvento;

    SELECT COUNT(*) INTO v_conflictos
    FROM (
        SELECT el.IDEvento
        FROM EventoLocal el
        INNER JOIN Evento e ON e.IDEvento = el.IDEvento
        WHERE el.IDEquipo = NEW.IDEquipo
          AND el.IDEvento <> NEW.IDEvento
          AND e.EstadoEvento <> 'CANCELADO'
          AND ABS(TIMESTAMPDIFF(MINUTE, e.FechaHora, v_fecha_evento)) < 120
        UNION ALL
        SELECT evi.IDEvento
        FROM EventoVisita evi
        INNER JOIN Evento e ON e.IDEvento = evi.IDEvento
        WHERE evi.IDEquipo = NEW.IDEquipo
          AND evi.IDEvento <> NEW.IDEvento
          AND e.EstadoEvento <> 'CANCELADO'
          AND ABS(TIMESTAMPDIFF(MINUTE, e.FechaHora, v_fecha_evento)) < 120
    ) AS conflictos;

    IF v_conflictos > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'El equipo ya participa de otro evento dentro de la ventana de 2 horas.';
    END IF;
END$$

CREATE TRIGGER trg_eventovisita_before_insert
BEFORE INSERT ON EventoVisita
FOR EACH ROW
BEGIN
    DECLARE v_fecha_evento DATETIME;
    DECLARE v_conflictos   INT;

    IF EXISTS (
        SELECT 1 FROM EventoLocal
        WHERE IDEvento = NEW.IDEvento AND IDEquipo = NEW.IDEquipo
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'El equipo local y visitante no pueden ser el mismo equipo.';
    END IF;

    SELECT FechaHora INTO v_fecha_evento FROM Evento WHERE IDEvento = NEW.IDEvento;

    SELECT COUNT(*) INTO v_conflictos
    FROM (
        SELECT el.IDEvento
        FROM EventoLocal el
        INNER JOIN Evento e ON e.IDEvento = el.IDEvento
        WHERE el.IDEquipo = NEW.IDEquipo
          AND el.IDEvento <> NEW.IDEvento
          AND e.EstadoEvento <> 'CANCELADO'
          AND ABS(TIMESTAMPDIFF(MINUTE, e.FechaHora, v_fecha_evento)) < 120
        UNION ALL
        SELECT evi.IDEvento
        FROM EventoVisita evi
        INNER JOIN Evento e ON e.IDEvento = evi.IDEvento
        WHERE evi.IDEquipo = NEW.IDEquipo
          AND evi.IDEvento <> NEW.IDEvento
          AND e.EstadoEvento <> 'CANCELADO'
          AND ABS(TIMESTAMPDIFF(MINUTE, e.FechaHora, v_fecha_evento)) < 120
    ) AS conflictos;

    IF v_conflictos > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'El equipo ya participa de otro evento dentro de la ventana de 2 horas.';
    END IF;
END$$


-- ============================================================
-- 6. TRANSFERENCIA - antes de insertar (nueva solicitud)
--    (a) No se puede transferir una entrada ya validada o anulada.
--    (b) No se puede iniciar una solicitud si ya hay una pendiente
--        para esa misma entrada.
--    (c) No se puede solicitar si ya se alcanzaron las 3
--        transferencias ACEPTADAS (las rechazadas/canceladas no
--        cuentan, según lo definido).
--    (d) Quien otorga debe ser el propietario actual real de la
--        entrada (vía V_PropietarioActual, sin dato redundante).
-- ============================================================
CREATE TRIGGER trg_transferencia_before_insert
BEFORE INSERT ON Transferencia
FOR EACH ROW
BEGIN
    DECLARE v_estado_entrada VARCHAR(20);
    DECLARE v_pendientes     INT;
    DECLARE v_aceptadas      INT;
    DECLARE v_dueno_tipo     VARCHAR(20);
    DECLARE v_dueno_pais     VARCHAR(50);
    DECLARE v_dueno_numero   VARCHAR(30);

    SELECT EstadoEntrada INTO v_estado_entrada
    FROM Entrada
    WHERE IDEntrada = NEW.IDEntrada;

    IF v_estado_entrada IN ('VALIDADA', 'ANULADA') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'No se puede transferir una entrada validada o anulada.';
    END IF;

    SELECT COUNT(*) INTO v_pendientes
    FROM Transferencia
    WHERE IDEntrada = NEW.IDEntrada
      AND Estado = 'PENDIENTE';

    IF v_pendientes > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Ya existe una solicitud de transferencia pendiente para esta entrada.';
    END IF;

    SELECT COUNT(*) INTO v_aceptadas
    FROM Transferencia
    WHERE IDEntrada = NEW.IDEntrada
      AND Estado = 'ACEPTADA';

    IF v_aceptadas >= 3 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Esta entrada ya alcanzó el máximo de 3 transferencias aceptadas.';
    END IF;

    SELECT TipoDocumento, PaisDocumento, Numero
        INTO v_dueno_tipo, v_dueno_pais, v_dueno_numero
    FROM V_PropietarioActual
    WHERE IDEntrada = NEW.IDEntrada;

    IF NOT (
        v_dueno_tipo = NEW.TipoDocOtorga
        AND v_dueno_pais = NEW.PaisDocOtorga
        AND v_dueno_numero = NEW.NumeroOtorga
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Quien otorga la transferencia no es el propietario actual de la entrada.';
    END IF;
END$$


-- ============================================================
-- 7. TRANSFERENCIA - antes de actualizar (aceptar una solicitud)
--    Vuelve a chequear el máximo de 3 ACEPTADAS al momento de
--    aceptar, por si había más de una solicitud pendiente para
--    la misma entrada y ambas intentan aceptarse.
-- ============================================================
CREATE TRIGGER trg_transferencia_before_update
BEFORE UPDATE ON Transferencia
FOR EACH ROW
BEGIN
    DECLARE v_aceptadas INT;

    IF NEW.Estado = 'ACEPTADA' AND OLD.Estado <> 'ACEPTADA' THEN
        SELECT COUNT(*) INTO v_aceptadas
        FROM Transferencia
        WHERE IDEntrada = NEW.IDEntrada
          AND Estado = 'ACEPTADA';

        IF v_aceptadas >= 3 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'No se puede aceptar: esta entrada ya alcanzó el máximo de 3 transferencias aceptadas.';
        END IF;
    END IF;
END$$


-- ============================================================
-- 8. VALIDACION - antes de insertar
--    (a) Solo se pueden validar entradas en estado ACTIVA.
--    (b) El funcionario debe estar asignado a ese mismo
--        evento+sector (FuncionarioEventoSector).
--    Nota: que no se valide la misma entrada dos veces ya lo
--    garantiza UK_Validacion_Entrada de forma atómica, sin
--    necesidad de chequearlo acá.
-- ============================================================
CREATE TRIGGER trg_validacion_before_insert
BEFORE INSERT ON Validacion
FOR EACH ROW
BEGIN
    DECLARE v_estado_entrada VARCHAR(20);
    DECLARE v_id_evento      BIGINT UNSIGNED;
    DECLARE v_id_sector      BIGINT UNSIGNED;
    DECLARE v_asignado       INT;

    SELECT EstadoEntrada, IDEvento, IDSector
        INTO v_estado_entrada, v_id_evento, v_id_sector
    FROM Entrada
    WHERE IDEntrada = NEW.IDEntrada;

    IF v_estado_entrada <> 'ACTIVA' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Solo se pueden validar entradas en estado ACTIVA.';
    END IF;

    SELECT COUNT(*) INTO v_asignado
    FROM FuncionarioEventoSector
    WHERE TipoDocumento = NEW.TipoDocFuncionario
      AND PaisDocumento = NEW.PaisDocFuncionario
      AND Numero = NEW.NumeroFuncionario
      AND IDEvento = v_id_evento
      AND IDSector = v_id_sector;

    IF v_asignado = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'El funcionario no está asignado a este evento y sector.';
    END IF;
END$$


-- ============================================================
-- 9. VALIDACION - después de insertar
--    Marca la entrada como VALIDADA una vez registrada la
--    validación.
-- ============================================================
CREATE TRIGGER trg_validacion_after_insert
AFTER INSERT ON Validacion
FOR EACH ROW
BEGIN
    UPDATE Entrada
    SET EstadoEntrada = 'VALIDADA'
    WHERE IDEntrada = NEW.IDEntrada;
END$$


DELIMITER ;
