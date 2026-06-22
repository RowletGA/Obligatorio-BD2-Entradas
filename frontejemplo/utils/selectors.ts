import type { AppData, DocumentoRef } from "@/types";
import { sameDocument } from "@/utils/format";

export const getTeam = (data: AppData, id: string) => data.equipos.find((team) => team.idEquipo === id);
export const getStadium = (data: AppData, id: string) => data.estadios.find((stadium) => stadium.idEstadio === id);
export const getSector = (data: AppData, id: string) => data.sectores.find((sector) => sector.idSector === id);
export const getEvent = (data: AppData, id: string) => data.eventos.find((event) => event.idEvento === id);
export const getUser = (data: AppData, doc: DocumentoRef) => data.usuarios.find((user) => sameDocument(user, doc));

export const eventName = (data: AppData, eventId: string) => {
  const event = getEvent(data, eventId);
  if (!event) return "Evento no encontrado";
  return `${getTeam(data, event.equipoLocal)?.pais ?? event.equipoLocal} vs ${
    getTeam(data, event.equipoVisitante)?.pais ?? event.equipoVisitante
  }`;
};

export const soldTickets = (data: AppData, eventId: string, sectorId?: string) =>
  data.entradas.filter(
    (ticket) => ticket.idEvento === eventId && (!sectorId || ticket.idSector === sectorId),
  ).length;

export const userTickets = (data: AppData, doc: DocumentoRef) =>
  data.entradas.filter((ticket) => sameDocument(ticket.propietarioActual, doc));

export const userSales = (data: AppData, doc: DocumentoRef) =>
  data.ventas.filter((sale) => sameDocument(sale.comprador, doc));
