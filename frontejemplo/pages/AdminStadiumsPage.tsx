import { useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { ProgressBar } from "@/components/ProgressBar";
import { useApp } from "@/context/AppContext";
import type { Estadio } from "@/types";

const empty: Estadio = {
  idEstadio: "",
  nombre: "",
  ubicacionPais: "Uruguay",
  ubicacionLocalidad: "",
  ubicacionCalle: "",
  ubicacionNumero: "",
  aforoTotal: 0,
};

export const AdminStadiumsPage = () => {
  const { data, saveStadium, deleteStadium } = useApp();
  const [form, setForm] = useState<Estadio>(empty);

  return (
    <>
      <PageHeader title="Administración de estadios" subtitle="ABM visual mock de estadios por país sede." />
      <div className="grid gap-5 lg:grid-cols-[380px_1fr]">
        <form className="card p-5" onSubmit={(e) => { e.preventDefault(); saveStadium(form); setForm(empty); }}>
          <h2 className="font-black">Crear / editar estadio</h2>
          {(["nombre", "ubicacionPais", "ubicacionLocalidad", "ubicacionCalle", "ubicacionNumero"] as const).map((field) => (
            <input key={field} className="input mt-3" placeholder={field} value={form[field]} onChange={(e) => setForm({ ...form, [field]: e.target.value })} />
          ))}
          <input className="input mt-3" type="number" placeholder="aforoTotal" value={form.aforoTotal || ""} onChange={(e) => setForm({ ...form, aforoTotal: Number(e.target.value) })} />
          <button className="btn-primary mt-4 w-full">Guardar estadio</button>
        </form>
        <div className="grid gap-4">
          {data.estadios.map((stadium) => {
            const capacity = data.sectores.filter((s) => s.idEstadio === stadium.idEstadio).reduce((sum, s) => sum + s.capacidad, 0);
            return (
              <article key={stadium.idEstadio} className="card p-5">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h2 className="text-xl font-black">{stadium.nombre}</h2>
                    <p className="text-slate-600">{stadium.ubicacionLocalidad}, {stadium.ubicacionPais}</p>
                  </div>
                  <div className="flex gap-2">
                    <button className="btn-secondary" onClick={() => setForm(stadium)}>Editar</button>
                    <button className="btn-secondary" onClick={() => deleteStadium(stadium.idEstadio)}>Eliminar</button>
                  </div>
                </div>
                <div className="mt-4"><ProgressBar value={capacity} max={stadium.aforoTotal} /></div>
              </article>
            );
          })}
        </div>
      </div>
    </>
  );
};
