import { useState } from "react";
import { Badge } from "@/components/Badge";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import type { Evento, EstadoEvento } from "@/types";
import { dateTime } from "@/utils/format";
import { eventName, getStadium } from "@/utils/selectors";

export const AdminEventsPage = () => {
  const { data, saveEvent } = useApp();
  const [message, setMessage] = useState("");
  const [form, setForm] = useState<Evento>({
    idEvento: "",
    estadoEvento: "programado",
    fechaHora: "2026-06-25T20:00",
    idEstadio: data.estadios[0]?.idEstadio ?? "",
    equipoLocal: data.equipos[0]?.idEquipo ?? "",
    equipoVisitante: data.equipos[1]?.idEquipo ?? "",
    sectoresHabilitados: [],
  });
  const stadiumSectors = data.sectores.filter((sector) => sector.idEstadio === form.idEstadio);

  const submit = () => {
    try {
      saveEvent({ ...form, fechaHora: new Date(form.fechaHora).toISOString() });
      setMessage("Evento guardado.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "No se pudo guardar.");
    }
  };

  return (
    <>
      <PageHeader title="Administración de eventos" subtitle="Asociar estadio, equipos, fecha y sectores habilitados." />
      <div className="grid gap-5 lg:grid-cols-[420px_1fr]">
        <section className="card p-5">
          <select className="input" value={form.idEstadio} onChange={(e) => setForm({ ...form, idEstadio: e.target.value, sectoresHabilitados: [] })}>
            {data.estadios.map((stadium) => <option key={stadium.idEstadio} value={stadium.idEstadio}>{stadium.nombre}</option>)}
          </select>
          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <select className="input" value={form.equipoLocal} onChange={(e) => setForm({ ...form, equipoLocal: e.target.value })}>
              {data.equipos.map((team) => <option key={team.idEquipo} value={team.idEquipo}>{team.pais}</option>)}
            </select>
            <select className="input" value={form.equipoVisitante} onChange={(e) => setForm({ ...form, equipoVisitante: e.target.value })}>
              {data.equipos.map((team) => <option key={team.idEquipo} value={team.idEquipo}>{team.pais}</option>)}
            </select>
          </div>
          <input className="input mt-3" type="datetime-local" value={form.fechaHora.slice(0, 16)} onChange={(e) => setForm({ ...form, fechaHora: e.target.value })} />
          <select className="input mt-3" value={form.estadoEvento} onChange={(e) => setForm({ ...form, estadoEvento: e.target.value as EstadoEvento })}>
            {["programado", "en_venta", "agotado", "finalizado", "cancelado"].map((status) => <option key={status}>{status}</option>)}
          </select>
          <p className="mt-4 font-black">Sectores habilitados</p>
          <div className="mt-2 grid gap-2">
            {stadiumSectors.map((sector) => (
              <label key={sector.idSector} className="flex items-center gap-2 rounded-xl border border-slate-200 p-3">
                <input type="checkbox" checked={form.sectoresHabilitados.includes(sector.idSector)} onChange={(e) => {
                  setForm((current) => ({
                    ...current,
                    sectoresHabilitados: e.target.checked
                      ? [...current.sectoresHabilitados, sector.idSector]
                      : current.sectoresHabilitados.filter((id) => id !== sector.idSector),
                  }));
                }} />
                {sector.nombreSector}
              </label>
            ))}
          </div>
          <button className="btn-primary mt-4 w-full" onClick={submit}>Guardar evento</button>
          {message ? <p className="mt-3 rounded-xl bg-amber-50 p-3 text-sm font-bold text-amber-800">{message}</p> : null}
        </section>
        <div className="space-y-3">
          {data.eventos.map((event) => (
            <button key={event.idEvento} className="card w-full p-4 text-left" onClick={() => setForm({ ...event, fechaHora: event.fechaHora.slice(0, 16) })}>
              <Badge value={event.estadoEvento} />
              <p className="mt-2 font-black">{eventName(data, event.idEvento)}</p>
              <p className="text-sm text-slate-600">{getStadium(data, event.idEstadio)?.nombre} · {dateTime(event.fechaHora)}</p>
            </button>
          ))}
        </div>
      </div>
    </>
  );
};
