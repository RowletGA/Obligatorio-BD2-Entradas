export type Role = "usuario" | "administrador" | "funcionario";

export type TipoDocumento = "CI" | "PASAPORTE" | "DNI";
export type EstadoVerificacion = "pendiente" | "verificado" | "rechazado";
export type EstadoVenta = "confirmada" | "pendiente" | "cancelada";
export type EstadoEntrada = "activa" | "transferida" | "consumida" | "vencida";
export type EstadoTransferencia = "pendiente" | "aceptada" | "rechazada";
export type EstadoEvento = "programado" | "en_venta" | "agotado" | "finalizado" | "cancelado";
export type ResultadoValidacion =
  | "valido"
  | "invalido"
  | "ya_usada"
  | "dispositivo_no_autorizado";

export interface DocumentoRef {
  tipoDocumento: TipoDocumento;
  paisDocumento: string;
  numeroDocumento: string;
}

export interface Usuario extends DocumentoRef {
  correoElectronico: string;
  direccionPais: string;
  direccionLocalidad: string;
  direccionCalle: string;
  direccionNumero: string;
  direccionCodigoPostal: string;
  nombre: string;
  apellido: string;
}

export interface TelefonoUsuario extends DocumentoRef {
  telefono: string;
}

export interface UsuarioGeneral extends DocumentoRef {
  fechaRegistro: string;
  estadoVerificacion: EstadoVerificacion;
}

export interface Funcionario extends DocumentoRef {
  numLegajo: string;
}

export interface Administrador extends DocumentoRef {
  fechaAsignacion: string;
  paisSede: string;
}

export interface Dispositivo {
  idDispositivo: string;
  tipoDocAdmin: TipoDocumento;
  paisDocAdmin: string;
  numeroAdmin: string;
  autorizado: boolean;
  descripcion: string;
}

export interface Equipo {
  idEquipo: string;
  pais: string;
  grupo: string;
}

export interface Estadio {
  idEstadio: string;
  nombre: string;
  ubicacionPais: string;
  ubicacionLocalidad: string;
  ubicacionCalle: string;
  ubicacionNumero: string;
  aforoTotal: number;
}

export interface Sector {
  idSector: string;
  nombreSector: string;
  capacidad: number;
  costo: number;
  idEstadio: string;
}

export interface Evento {
  idEvento: string;
  estadoEvento: EstadoEvento;
  fechaHora: string;
  idEstadio: string;
  equipoLocal: string;
  equipoVisitante: string;
  sectoresHabilitados: string[];
}

export interface Venta {
  idVenta: string;
  fechaVenta: string;
  estadoVenta: EstadoVenta;
  montoTotal: number;
  funcionarioResponsable?: DocumentoRef;
  comprador: DocumentoRef;
  entradas: string[];
}

export interface Entrada {
  idEntrada: string;
  estadoEntrada: EstadoEntrada;
  fechaEmision: string;
  tokenActual: string;
  fechaUltimoToken: string;
  costo: number;
  idVenta: string;
  idEvento: string;
  idSector: string;
  propietarioActual: DocumentoRef;
}

export interface Transferencia {
  idTransferencia: string;
  usuarioOtorga: DocumentoRef;
  usuarioRecibe: DocumentoRef;
  idEntrada: string;
  fechaTransferencia: string;
  estadoTransferencia: EstadoTransferencia;
}

export interface ValidacionAcceso {
  idValidacion: string;
  idEntrada?: string;
  tokenAceptado: string;
  funcionario: DocumentoRef;
  fechaHora: string;
  dispositivo: string;
  resultado: ResultadoValidacion;
  idEvento: string;
}

export interface DemoUser {
  role: Role;
  documento: DocumentoRef;
  displayName: string;
}

export interface AppData {
  usuarios: Usuario[];
  telefonos: TelefonoUsuario[];
  usuariosGenerales: UsuarioGeneral[];
  funcionarios: Funcionario[];
  administradores: Administrador[];
  dispositivos: Dispositivo[];
  estadios: Estadio[];
  sectores: Sector[];
  equipos: Equipo[];
  eventos: Evento[];
  ventas: Venta[];
  entradas: Entrada[];
  transferencias: Transferencia[];
  validaciones: ValidacionAcceso[];
}

export interface PurchaseInput {
  idEvento: string;
  idSector: string;
  cantidad: number;
}

export interface TransferInput {
  idEntrada: string;
  receptor: DocumentoRef;
}
