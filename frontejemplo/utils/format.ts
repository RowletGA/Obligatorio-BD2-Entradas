import type { DocumentoRef, EstadoEntrada, EstadoEvento, ResultadoValidacion } from "@/types";

export const documentKey = (doc: DocumentoRef) =>
  `${doc.tipoDocumento}-${doc.paisDocumento}-${doc.numeroDocumento}`;

export const sameDocument = (a: DocumentoRef, b: DocumentoRef) =>
  documentKey(a) === documentKey(b);

export const money = (value: number) =>
  new Intl.NumberFormat("es-UY", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 0,
  }).format(value);

export const dateTime = (value: string) =>
  new Intl.DateTimeFormat("es-UY", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));

export const badgeTone = (
  status: EstadoEntrada | EstadoEvento | ResultadoValidacion | string,
) => {
  if (["activa", "en_venta", "programado", "valido", "confirmada", "aceptada"].includes(status)) {
    return "bg-emerald-100 text-emerald-800";
  }
  if (["pendiente"].includes(status)) return "bg-amber-100 text-amber-800";
  if (["consumida", "finalizado", "ya_usada"].includes(status)) return "bg-slate-200 text-slate-700";
  if (["invalido", "cancelado", "cancelada", "rechazada", "dispositivo_no_autorizado"].includes(status)) {
    return "bg-rose-100 text-rose-800";
  }
  return "bg-blue-100 text-blue-800";
};

export const humanStatus = (value?: string) =>
  (value || "sin estado").replace(/_/g, " ");

export const generateToken = (prefix = "QR") =>
  `${prefix}-${Math.random().toString(36).slice(2, 8).toUpperCase()}-${Date.now()
    .toString(36)
    .toUpperCase()}`;
