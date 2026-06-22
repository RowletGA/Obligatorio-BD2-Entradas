import { useState } from "react";
import { Badge } from "@/components/Badge";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { dateTime } from "@/utils/format";
import { eventName } from "@/utils/selectors";

export const ValidationPage = () => {
  const { data, validateToken } = useApp();
  const [eventId, setEventId] = useState(data.eventos[0]?.idEvento ?? "");
  const [token, setToken] = useState(data.entradas[0]?.tokenActual ?? "");
  const [result, setResult] = useState<string>("");
  const device = data.dispositivos[0];

  const submit = () => {
    const validation = validateToken(token, eventId);
    setResult(validation.resultado);
  };

  return (
    <>
      <PageHeader title="Validación de acceso" subtitle="Simulación de escaneo QR/token y auditoría de ingreso." />
      <div className="grid gap-5 lg:grid-cols-[420px_1fr]">
        <section className="card p-5">
          <div className="rounded-2xl bg-slate-950 p-4 text-white">
            <p className="text-sm font-bold text-slate-300">Dispositivo asignado</p>
            <p className="mt-1 font-black">{device?.idDispositivo}</p>
            <p className="text-sm text-slate-300">{device?.autorizado ? "Autorizado" : "No autorizado"}</p>
          </div>
          <select className="input mt-4" value={eventId} onChange={(e) => setEventId(e.target.value)}>
            {data.eventos.map((event) => <option key={event.idEvento} value={event.idEvento}>{eventName(data, event.idEvento)}</option>)}
          </select>
          <input className="input mt-3" placeholder="Token / QR" value={token} onChange={(e) => setToken(e.target.value)} />
          <button className="btn-primary mt-4 w-full" onClick={submit}>Validar entrada</button>
          {result ? <div className="mt-4"><Badge value={result} /></div> : null}
        </section>
        <section className="card p-5">
          <h2 className="font-black">Validaciones recientes</h2>
          <div className="mt-4 space-y-3">
            {data.validaciones.slice(0, 8).map((validation) => (
              <div key={validation.idValidacion} className="rounded-xl border border-slate-200 p-3">
                <Badge value={validation.resultado} />
                <p className="mt-2 text-sm font-bold">{validation.tokenAceptado}</p>
                <p className="text-xs text-slate-500">{validation.dispositivo} · {dateTime(validation.fechaHora)}</p>
              </div>
            ))}
          </div>
        </section>
      </div>
    </>
  );
};
