import { initialData } from "@/data/mockData";
import type {
  AppData,
  DocumentoRef,
  Entrada,
  Estadio,
  Evento,
  PurchaseInput,
  Sector,
  TransferInput,
  ValidacionAcceso,
  Venta,
} from "@/types";
import { documentKey, generateToken, sameDocument } from "@/utils/format";

const clone = <T>(value: T): T => structuredClone(value);

const nextId = (prefix: string) => `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;

export const createInitialState = () => clone(initialData);

export const apiClient = {
  listEvents(data: AppData) {
    return clone(data.eventos);
  },

  buyTickets(data: AppData, buyer: DocumentoRef, input: PurchaseInput): AppData {
    if (input.cantidad < 1 || input.cantidad > 5) {
      throw new Error("Máximo 5 entradas por transacción.");
    }

    const event = data.eventos.find((item) => item.idEvento === input.idEvento);
    const sector = data.sectores.find((item) => item.idSector === input.idSector);
    if (!event || !sector) throw new Error("Evento o sector inválido.");
    if (!event.sectoresHabilitados.includes(sector.idSector)) {
      throw new Error("El sector no está habilitado para este evento.");
    }

    const soldInSector = data.entradas.filter(
      (ticket) => ticket.idEvento === event.idEvento && ticket.idSector === sector.idSector,
    ).length;
    if (soldInSector + input.cantidad > sector.capacidad) {
      throw new Error("La compra supera la capacidad disponible del sector.");
    }

    const subtotal = sector.costo * input.cantidad;
    const total = subtotal * 1.05;
    const saleId = nextId("VEN");
    const now = new Date().toISOString();
    const newTickets: Entrada[] = Array.from({ length: input.cantidad }, (_, index) => ({
      idEntrada: nextId(`ENT${index + 1}`),
      estadoEntrada: "activa",
      fechaEmision: now,
      tokenActual: generateToken("QR"),
      fechaUltimoToken: now,
      costo: sector.costo,
      idVenta: saleId,
      idEvento: event.idEvento,
      idSector: sector.idSector,
      propietarioActual: buyer,
    }));

    const sale: Venta = {
      idVenta: saleId,
      fechaVenta: now,
      estadoVenta: "confirmada",
      montoTotal: total,
      comprador: buyer,
      entradas: newTickets.map((ticket) => ticket.idEntrada),
    };

    return {
      ...data,
      ventas: [sale, ...data.ventas],
      entradas: [...newTickets, ...data.entradas],
    };
  },

  regenerateToken(data: AppData, ticketId: string): AppData {
    return {
      ...data,
      entradas: data.entradas.map((ticket) =>
        ticket.idEntrada === ticketId
          ? {
              ...ticket,
              tokenActual: generateToken("QR"),
              fechaUltimoToken: new Date().toISOString(),
            }
          : ticket,
      ),
    };
  },

  transferTicket(data: AppData, owner: DocumentoRef, input: TransferInput): AppData {
    const ticket = data.entradas.find((item) => item.idEntrada === input.idEntrada);
    if (!ticket) throw new Error("Entrada inexistente.");
    if (!sameDocument(ticket.propietarioActual, owner)) throw new Error("La entrada no pertenece al usuario actual.");
    if (ticket.estadoEntrada === "consumida") throw new Error("Una entrada consumida no puede transferirse.");

    const count = data.transferencias.filter(
      (transfer) => transfer.idEntrada === input.idEntrada && transfer.estadoTransferencia === "aceptada",
    ).length;
    if (count >= 3) throw new Error("La entrada ya alcanzó el máximo de 3 transferencias.");

    const transfer = {
      idTransferencia: nextId("TRF"),
      usuarioOtorga: owner,
      usuarioRecibe: input.receptor,
      idEntrada: input.idEntrada,
      fechaTransferencia: new Date().toISOString(),
      estadoTransferencia: "aceptada" as const,
    };

    return {
      ...data,
      transferencias: [transfer, ...data.transferencias],
      entradas: data.entradas.map((item) =>
        item.idEntrada === input.idEntrada
          ? { ...item, propietarioActual: input.receptor, estadoEntrada: "activa" }
          : item,
      ),
    };
  },

  validateToken(
    data: AppData,
    params: {
      token: string;
      eventId: string;
      funcionario: DocumentoRef;
      deviceId: string;
    },
  ): { data: AppData; result: ValidacionAcceso } {
    const device = data.dispositivos.find((item) => item.idDispositivo === params.deviceId);
    const ticket = data.entradas.find(
      (item) => item.tokenActual === params.token && item.idEvento === params.eventId,
    );
    const now = new Date().toISOString();

    let resultado: ValidacionAcceso["resultado"] = "invalido";
    if (!device?.autorizado) resultado = "dispositivo_no_autorizado";
    else if (!ticket) resultado = "invalido";
    else if (ticket.estadoEntrada === "consumida") resultado = "ya_usada";
    else resultado = "valido";

    const validation: ValidacionAcceso = {
      idValidacion: nextId("VAL"),
      idEntrada: ticket?.idEntrada,
      tokenAceptado: params.token,
      funcionario: params.funcionario,
      fechaHora: now,
      dispositivo: params.deviceId,
      resultado,
      idEvento: params.eventId,
    };

    return {
      result: validation,
      data: {
        ...data,
        validaciones: [validation, ...data.validaciones],
        entradas:
          resultado === "valido" && ticket
            ? data.entradas.map((item) =>
                item.idEntrada === ticket.idEntrada ? { ...item, estadoEntrada: "consumida" } : item,
              )
            : data.entradas,
      },
    };
  },

  saveStadium(data: AppData, stadium: Estadio): AppData {
    const exists = data.estadios.some((item) => item.idEstadio === stadium.idEstadio);
    return {
      ...data,
      estadios: exists
        ? data.estadios.map((item) => (item.idEstadio === stadium.idEstadio ? stadium : item))
        : [{ ...stadium, idEstadio: nextId("EST") }, ...data.estadios],
    };
  },

  deleteStadium(data: AppData, stadiumId: string): AppData {
    return {
      ...data,
      estadios: data.estadios.filter((item) => item.idEstadio !== stadiumId),
      sectores: data.sectores.filter((item) => item.idEstadio !== stadiumId),
    };
  },

  saveSector(data: AppData, sector: Sector): AppData {
    const stadium = data.estadios.find((item) => item.idEstadio === sector.idEstadio);
    const siblingCapacity = data.sectores
      .filter((item) => item.idEstadio === sector.idEstadio && item.idSector !== sector.idSector)
      .reduce((sum, item) => sum + item.capacidad, 0);
    if (stadium && siblingCapacity + sector.capacidad > stadium.aforoTotal) {
      throw new Error("La suma de sectores supera el aforo del estadio.");
    }
    const exists = data.sectores.some((item) => item.idSector === sector.idSector);
    return {
      ...data,
      sectores: exists
        ? data.sectores.map((item) => (item.idSector === sector.idSector ? sector : item))
        : [{ ...sector, idSector: nextId("SEC") }, ...data.sectores],
    };
  },

  saveEvent(data: AppData, event: Evento): AppData {
    const newDate = new Date(event.fechaHora).getTime();
    const overlap = data.eventos.some((item) => {
      if (item.idEvento === event.idEvento || item.idEstadio !== event.idEstadio) return false;
      return Math.abs(new Date(item.fechaHora).getTime() - newDate) < 3 * 60 * 60 * 1000;
    });
    if (overlap) throw new Error("Ya existe un evento cercano en ese estadio.");

    const exists = data.eventos.some((item) => item.idEvento === event.idEvento);
    return {
      ...data,
      eventos: exists
        ? data.eventos.map((item) => (item.idEvento === event.idEvento ? event : item))
        : [{ ...event, idEvento: nextId("EVT") }, ...data.eventos],
    };
  },

  getUserByDocument(data: AppData, document: DocumentoRef) {
    return data.usuarios.find((user) => documentKey(user) === documentKey(document));
  },
};
