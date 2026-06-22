import { useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import type { Sector } from "@/types";
import { money } from "@/utils/format";

export const AdminSectorsPage = () => {
  const { data, saveSector } = useApp();
  const [message, setMessage] = useState("");
  const [form, setForm] = useState<Sector>({
    idSector: "",
    nombreSector: "Sector A",
    capacidad: 1000,
    costo: 100,
    idEstadio: data.estadios[0]?.idEstadio ?? "",
  });

  const submit = () => {
    try {
      saveSector(form);
      setMessage("Sector guardado.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "No se pudo guardar.");
    }
  };

  return (
    <>
      <PageHeader title="Administración de sectores" subtitle="Sectores A/B/C/D, capacidad, costo y advertencia de aforo." />
      <div className="grid gap-5 lg:grid-cols-[360px_1fr]">
        <section className="card p-5">
          <select className="input" value={form.idEstadio} onChange={(e) => setForm({ ...form, idEstadio: e.target.value })}>
            {data.estadios.map((stadium) => <option key={stadium.idEstadio} value={stadium.idEstadio}>{stadium.nombre}</option>)}
          </select>
          <input className="input mt-3" value={form.nombreSector} onChange={(e) => setForm({ ...form, nombreSector: e.target.value })} />
          <input className="input mt-3" type="number" value={form.capacidad} onChange={(e) => setForm({ ...form, capacidad: Number(e.target.value) })} />
          <input className="input mt-3" type="number" value={form.costo} onChange={(e) => setForm({ ...form, costo: Number(e.target.value) })} />
          <button className="btn-primary mt-4 w-full" onClick={submit}>Guardar sector</button>
          {message ? <p className="mt-3 rounded-xl bg-amber-50 p-3 text-sm font-bold text-amber-800">{message}</p> : null}
        </section>
        <div className="grid gap-3 md:grid-cols-2">
          {data.sectores.map((sector) => (
            <button key={sector.idSector} className="card p-4 text-left" onClick={() => setForm(sector)}>
              <p className="font-black">{sector.nombreSector}</p>
              <p className="text-sm text-slate-600">Capacidad {sector.capacidad.toLocaleString()} · {money(sector.costo)}</p>
            </button>
          ))}
        </div>
      </div>
    </>
  );
};
