-- ============================================================
-- DDL - Sistema de venta y validación de entradas
-- Motor: MySQL 8.0.16 o superior (CHECK constraints activos)
-- ============================================================

USE IC_Grupo4;

-- ============================================================
-- 1. USUARIOS Y ESPECIALIZACIONES
-- ============================================================

CREATE TABLE Usuario (
    TipoDocumento           VARCHAR(20)  NOT NULL,
    PaisDocumento           VARCHAR(50)  NOT NULL,
    Numero                  VARCHAR(30)  NOT NULL,
    PrimerNombre            VARCHAR(60)  NOT NULL,
    PrimerApellido          VARCHAR(60)  NOT NULL,
    CorreoElectronico       VARCHAR(254) NOT NULL,
    -- Almacenar siempre como hash (bcrypt/argon2), nunca texto plano.
    -- VARCHAR(255) cubre cualquier algoritmo actual y futuro.
    Contrasena              VARCHAR(255) NOT NULL,
    DireccionPais           VARCHAR(50)  NULL,
    DireccionLocalidad      VARCHAR(100) NULL,
    DireccionCalle          VARCHAR(100) NULL,
    DireccionNumero         VARCHAR(20)  NULL,
    DireccionCodigoPostal   VARCHAR(15)  NULL,

    CONSTRAINT PK_Usuario
        PRIMARY KEY (TipoDocumento, PaisDocumento, Numero),
    CONSTRAINT UK_Usuario_Correo
        UNIQUE (CorreoElectronico),
    CONSTRAINT CHK_Usuario_Correo
        CHECK (CorreoElectronico LIKE '%_@_%._%')
) ENGINE = InnoDB;

CREATE TABLE TelefonoUsuario (
    TipoDocumento   VARCHAR(20) NOT NULL,
    PaisDocumento   VARCHAR(50) NOT NULL,
    NumeroDoc       VARCHAR(30) NOT NULL,
    Telefono        VARCHAR(30) NOT NULL,

    CONSTRAINT PK_TelefonoUsuario
        PRIMARY KEY (TipoDocumento, PaisDocumento, NumeroDoc, Telefono),
    CONSTRAINT FK_TelefonoUsuario_Usuario
        FOREIGN KEY (TipoDocumento, PaisDocumento, NumeroDoc)
        REFERENCES Usuario (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE CASCADE
        ON DELETE CASCADE
) ENGINE = InnoDB;

CREATE TABLE UsuarioGeneral (
    TipoDocumento       VARCHAR(20) NOT NULL,
    PaisDocumento       VARCHAR(50) NOT NULL,
    Numero              VARCHAR(30) NOT NULL,
    FechaRegistro       DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EstadoVerificacion  VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',

    CONSTRAINT PK_UsuarioGeneral
        PRIMARY KEY (TipoDocumento, PaisDocumento, Numero),
    CONSTRAINT FK_UsuarioGeneral_Usuario
        FOREIGN KEY (TipoDocumento, PaisDocumento, Numero)
        REFERENCES Usuario (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT CHK_UsuarioGeneral_Estado
        CHECK (EstadoVerificacion IN ('PENDIENTE', 'VERIFICADO', 'RECHAZADO'))
) ENGINE = InnoDB;

CREATE TABLE Funcionario (
    TipoDocumento   VARCHAR(20) NOT NULL,
    PaisDocumento   VARCHAR(50) NOT NULL,
    Numero          VARCHAR(30) NOT NULL,
    NumLegajo       VARCHAR(30) NOT NULL,

    CONSTRAINT PK_Funcionario
        PRIMARY KEY (TipoDocumento, PaisDocumento, Numero),
    CONSTRAINT UK_Funcionario_NumLegajo
        UNIQUE (NumLegajo),
    CONSTRAINT FK_Funcionario_Usuario
        FOREIGN KEY (TipoDocumento, PaisDocumento, Numero)
        REFERENCES Usuario (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE CASCADE
        ON DELETE CASCADE
) ENGINE = InnoDB;

CREATE TABLE Administrador (
    TipoDocumento   VARCHAR(20) NOT NULL,
    PaisDocumento   VARCHAR(50) NOT NULL,
    Numero          VARCHAR(30) NOT NULL,
    FechaAsignacion DATE        NOT NULL,
    PaisSede        VARCHAR(50) NOT NULL,

    CONSTRAINT PK_Administrador
        PRIMARY KEY (TipoDocumento, PaisDocumento, Numero),
    CONSTRAINT FK_Administrador_Usuario
        FOREIGN KEY (TipoDocumento, PaisDocumento, Numero)
        REFERENCES Usuario (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE CASCADE
        ON DELETE CASCADE
) ENGINE = InnoDB;

-- ============================================================
-- 2. ESTADIOS, SECTORES Y EVENTOS
-- ============================================================

CREATE TABLE Estadio (
    IDEstadio           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Nombre              VARCHAR(100)    NOT NULL,
    UbicacionPais       VARCHAR(50)     NOT NULL,
    UbicacionLocalidad  VARCHAR(100)    NOT NULL,
    UbicacionCalle      VARCHAR(100)    NOT NULL,
    UbicacionNumero     VARCHAR(20)     NULL,

    CONSTRAINT PK_Estadio
        PRIMARY KEY (IDEstadio),
    CONSTRAINT UK_Estadio_Nombre_Ubicacion
        UNIQUE (Nombre, UbicacionPais, UbicacionLocalidad)
) ENGINE = InnoDB;

CREATE TABLE Sector (
    IDSector        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    NombreSector    VARCHAR(100)    NOT NULL,
    Capacidad       INT UNSIGNED    NOT NULL,
    IDEstadio       BIGINT UNSIGNED NOT NULL,

    CONSTRAINT PK_Sector
        PRIMARY KEY (IDSector),
    CONSTRAINT UK_Sector_Estadio_Nombre
        UNIQUE (IDEstadio, NombreSector),
    CONSTRAINT FK_Sector_Estadio
        FOREIGN KEY (IDEstadio)
        REFERENCES Estadio (IDEstadio)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Sector_Capacidad
        CHECK (Capacidad > 0)
) ENGINE = InnoDB;

CREATE TABLE Evento (
    IDEvento        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    EstadoEvento    VARCHAR(20)     NOT NULL DEFAULT 'PROGRAMADO',
    FechaHora       DATETIME        NOT NULL,
    IDEstadio       BIGINT UNSIGNED NOT NULL,

    CONSTRAINT PK_Evento
        PRIMARY KEY (IDEvento),
    CONSTRAINT FK_Evento_Estadio
        FOREIGN KEY (IDEstadio)
        REFERENCES Estadio (IDEstadio)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Evento_Estado
        CHECK (EstadoEvento IN ('PROGRAMADO', 'EN_CURSO', 'FINALIZADO', 'CANCELADO'))
) ENGINE = InnoDB;

CREATE TABLE EventoSector (
    IDEvento    BIGINT UNSIGNED NOT NULL,
    IDSector    BIGINT UNSIGNED NOT NULL,
    PrecioBase  DECIMAL(12,2)   NOT NULL,

    CONSTRAINT PK_EventoSector
        PRIMARY KEY (IDEvento, IDSector),
    CONSTRAINT FK_EventoSector_Evento
        FOREIGN KEY (IDEvento)
        REFERENCES Evento (IDEvento)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT FK_EventoSector_Sector
        FOREIGN KEY (IDSector)
        REFERENCES Sector (IDSector)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CHK_EventoSector_PrecioBase
        CHECK (PrecioBase >= 0)
) ENGINE = InnoDB;

-- ============================================================
-- 3. EQUIPOS PARTICIPANTES
-- ============================================================

CREATE TABLE Equipo (
    IDEquipo    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Pais        VARCHAR(50)     NOT NULL,
    Grupo       VARCHAR(20)     NULL,

    CONSTRAINT PK_Equipo
        PRIMARY KEY (IDEquipo),
    CONSTRAINT UK_Equipo_Pais_Grupo
        UNIQUE (Pais, Grupo)
) ENGINE = InnoDB;

CREATE TABLE EventoLocal (
    IDEvento    BIGINT UNSIGNED NOT NULL,
    IDEquipo    BIGINT UNSIGNED NOT NULL,

    CONSTRAINT PK_EventoLocal
        PRIMARY KEY (IDEvento, IDEquipo),
    CONSTRAINT UK_EventoLocal_Evento
        UNIQUE (IDEvento),
    CONSTRAINT FK_EventoLocal_Evento
        FOREIGN KEY (IDEvento)
        REFERENCES Evento (IDEvento)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT FK_EventoLocal_Equipo
        FOREIGN KEY (IDEquipo)
        REFERENCES Equipo (IDEquipo)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE TABLE EventoVisita (
    IDEvento    BIGINT UNSIGNED NOT NULL,
    IDEquipo    BIGINT UNSIGNED NOT NULL,

    CONSTRAINT PK_EventoVisita
        PRIMARY KEY (IDEvento, IDEquipo),
    CONSTRAINT UK_EventoVisita_Evento
        UNIQUE (IDEvento),
    CONSTRAINT FK_EventoVisita_Evento
        FOREIGN KEY (IDEvento)
        REFERENCES Evento (IDEvento)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT FK_EventoVisita_Equipo
        FOREIGN KEY (IDEquipo)
        REFERENCES Equipo (IDEquipo)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

-- ============================================================
-- 4. ASIGNACIÓN DE FUNCIONARIOS
-- ============================================================

CREATE TABLE FuncionarioEventoSector (
    TipoDocumento  VARCHAR(20)     NOT NULL,
    PaisDocumento  VARCHAR(50)     NOT NULL,
    Numero         VARCHAR(30)     NOT NULL,
    IDEvento       BIGINT UNSIGNED NOT NULL,
    IDSector       BIGINT UNSIGNED NOT NULL,

    CONSTRAINT PK_FuncionarioEventoSector
        PRIMARY KEY (TipoDocumento, PaisDocumento, Numero, IDEvento, IDSector),
    CONSTRAINT FK_FuncEventoSector_Funcionario
        FOREIGN KEY (TipoDocumento, PaisDocumento, Numero)
        REFERENCES Funcionario (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT FK_FuncEventoSector_EventoSector
        FOREIGN KEY (IDEvento, IDSector)
        REFERENCES EventoSector (IDEvento, IDSector)
        ON UPDATE CASCADE
        ON DELETE CASCADE
) ENGINE = InnoDB;

-- ============================================================
-- 5. VENTAS Y ENTRADAS
-- ============================================================

CREATE TABLE Venta (
    IDVenta             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    FechaVenta          DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EstadoVenta         VARCHAR(20)     NOT NULL DEFAULT 'pendiente',
    MontoTotal          DECIMAL(12,2)   NOT NULL DEFAULT 0.00,
    PorcentajeComision  DECIMAL(5,2)    NOT NULL DEFAULT 5.00,
    TipoDocUsuario      VARCHAR(20)     NOT NULL,
    PaisDocUsuario      VARCHAR(50)     NOT NULL,
    NumeroUsuario       VARCHAR(30)     NOT NULL,

    CONSTRAINT PK_Venta
        PRIMARY KEY (IDVenta),
    CONSTRAINT FK_Venta_UsuarioGeneral
        FOREIGN KEY (TipoDocUsuario, PaisDocUsuario, NumeroUsuario)
        REFERENCES UsuarioGeneral (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Venta_Estado
        CHECK (EstadoVenta IN ('pendiente', 'confirmada', 'paga')),
    CONSTRAINT CHK_Venta_MontoTotal
        CHECK (MontoTotal >= 0),
    CONSTRAINT CHK_Venta_PorcentajeComision
        CHECK (PorcentajeComision >= 0 AND PorcentajeComision <= 100)
) ENGINE = InnoDB;

CREATE TABLE Entrada (
    IDEntrada       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    EstadoEntrada   VARCHAR(20)     NOT NULL DEFAULT 'ACTIVA',
    FechaEmision    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Costo           DECIMAL(12,2)   NOT NULL,
    IDVenta         BIGINT UNSIGNED NOT NULL,
    IDEvento        BIGINT UNSIGNED NOT NULL,
    IDSector        BIGINT UNSIGNED NOT NULL,

    CONSTRAINT PK_Entrada
        PRIMARY KEY (IDEntrada),
    CONSTRAINT FK_Entrada_Venta
        FOREIGN KEY (IDVenta)
        REFERENCES Venta (IDVenta)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT FK_Entrada_EventoSector
        FOREIGN KEY (IDEvento, IDSector)
        REFERENCES EventoSector (IDEvento, IDSector)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Entrada_Estado
        CHECK (EstadoEntrada IN ('ACTIVA', 'VALIDADA', 'TRANSFERIDA', 'ANULADA')),
    CONSTRAINT CHK_Entrada_Costo
        CHECK (Costo >= 0)
) ENGINE = InnoDB;

-- ============================================================
-- 6. VALIDACIONES
-- ============================================================
-- Tabla propia para el registro de validación de cada entrada.
-- UK_Validacion_Entrada garantiza, de forma atómica, que una
-- misma entrada no pueda validarse dos veces (sin depender de
-- un trigger leer-y-luego-escribir, que sí tendría una ventana
-- de condición de carrera ante dos escaneos casi simultáneos).
-- ============================================================

CREATE TABLE Validacion (
    IDValidacion         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    IDEntrada            BIGINT UNSIGNED NOT NULL,
    TipoDocFuncionario   VARCHAR(20)     NOT NULL,
    PaisDocFuncionario   VARCHAR(50)     NOT NULL,
    NumeroFuncionario    VARCHAR(30)     NOT NULL,
    FechaHoraValidacion  DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    TokenValidado        VARCHAR(255)    NOT NULL,

    CONSTRAINT PK_Validacion
        PRIMARY KEY (IDValidacion),
    CONSTRAINT UK_Validacion_Entrada
        UNIQUE (IDEntrada),
    CONSTRAINT FK_Validacion_Entrada
        FOREIGN KEY (IDEntrada)
        REFERENCES Entrada (IDEntrada)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT FK_Validacion_Funcionario
        FOREIGN KEY (TipoDocFuncionario, PaisDocFuncionario, NumeroFuncionario)
        REFERENCES Funcionario (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

-- ============================================================
-- 7. TRANSFERENCIAS ENTRE USUARIOS GENERALES
-- ============================================================

CREATE TABLE Transferencia (
    IDTransferencia    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    TipoDocOtorga      VARCHAR(20)     NOT NULL,
    PaisDocOtorga      VARCHAR(50)     NOT NULL,
    NumeroOtorga       VARCHAR(30)     NOT NULL,
    TipoDocRecibe      VARCHAR(20)     NOT NULL,
    PaisDocRecibe      VARCHAR(50)     NOT NULL,
    NumeroRecibe       VARCHAR(30)     NOT NULL,
    IDEntrada          BIGINT UNSIGNED NOT NULL,
    FechaSolicitud     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FechaRespuesta     DATETIME        NULL,
    Estado             VARCHAR(20)     NOT NULL DEFAULT 'PENDIENTE',

    CONSTRAINT PK_Transferencia
        PRIMARY KEY (IDTransferencia),
    CONSTRAINT FK_Transferencia_UsuarioOtorga
        FOREIGN KEY (TipoDocOtorga, PaisDocOtorga, NumeroOtorga)
        REFERENCES UsuarioGeneral (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT FK_Transferencia_UsuarioRecibe
        FOREIGN KEY (TipoDocRecibe, PaisDocRecibe, NumeroRecibe)
        REFERENCES UsuarioGeneral (TipoDocumento, PaisDocumento, Numero)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT FK_Transferencia_Entrada
        FOREIGN KEY (IDEntrada)
        REFERENCES Entrada (IDEntrada)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Transferencia_Estado
        CHECK (Estado IN ('PENDIENTE', 'ACEPTADA', 'RECHAZADA', 'CANCELADA')),
    CONSTRAINT CHK_Transferencia_UsuariosDistintos
        CHECK (
            NOT (
                TipoDocOtorga = TipoDocRecibe
                AND PaisDocOtorga = PaisDocRecibe
                AND NumeroOtorga = NumeroRecibe
            )
        ),
    CONSTRAINT CHK_Transferencia_Fechas
        CHECK (FechaRespuesta IS NULL OR FechaRespuesta >= FechaSolicitud),
    CONSTRAINT CHK_Transferencia_Respuesta
        CHECK (
            (Estado = 'PENDIENTE' AND FechaRespuesta IS NULL)
            OR
            (Estado IN ('ACEPTADA', 'RECHAZADA', 'CANCELADA') AND FechaRespuesta IS NOT NULL)
        )
) ENGINE = InnoDB;



-- ============================================================
-- 8. ÍNDICES ADICIONALES
-- ============================================================

CREATE INDEX IX_Evento_FechaHora
    ON Evento (FechaHora);

CREATE INDEX IX_Venta_FechaVenta
    ON Venta (FechaVenta);

CREATE INDEX IX_Entrada_EventoSectorEstado
    ON Entrada (IDEvento, IDSector, EstadoEntrada);

CREATE INDEX IX_Entrada_Venta
    ON Entrada (IDVenta);

CREATE INDEX IX_Transferencia_EntradaEstado
    ON Transferencia (IDEntrada, Estado);

CREATE INDEX IX_Transferencia_UsuarioRecibeEstado
    ON Transferencia (TipoDocRecibe, PaisDocRecibe, NumeroRecibe, Estado);

CREATE INDEX IX_Validacion_Funcionario
    ON Validacion (TipoDocFuncionario, PaisDocFuncionario, NumeroFuncionario);

CREATE INDEX IX_FuncionarioEventoSector_EventoSector
    ON FuncionarioEventoSector (IDEvento, IDSector);
