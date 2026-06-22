import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { money } from "@/utils/format";
import { eventName, getSector, getStadium, soldTickets } from "@/utils/selectors";

export const PurchasePage = () => {
  const { data, purchaseTickets } = useApp();
  const [params] = useSearchParams();
  const [eventId, setEventId] = useState(params.get("evento") ?? data.eventos[0]?.idEvento ?? "");
  const event = data.eventos.find((item) => item.idEvento === eventId);
  const [sectorId, setSectorId] = useState(event?.sectoresHabilitados[0] ?? "");
  const [qty, setQty] = useState(1);
  const [message, setMessage] = useState("");
  const sector = getSector(data, sectorId);
  const subtotal = (sector?.costo ?? 0) * qty;
  const fee = subtotal * 0.05;
  const total = subtotal + fee;
  const sectors = useMemo(() => event?.sectoresHabilitados ?? [], [event]);

  const confirm = () => {
    try {
      purchaseTickets({ idEvento: eventId, idSector: sectorId, cantidad: qty });
      setMessage("Compra confirmada. Las entradas ya aparecen en Mis entradas.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "No se pudo confirmar la compra.");
    }
  };

  return (
    <>
      <PageHeader title="Compra de entradas" subtitle="Máximo 5 entradas por transacción. Comisión: 5%." />
      <div className="grid gap-5 lg:grid-cols-[1fr_360px]">
        <div className="card p-5">
          <div className="grid gap-4 md:grid-cols-2">
            <label className="text-sm font-bold text-slate-600">Evento
              <select className="input mt-1" value={eventId} onChange={(e) => {
                const next = e.target.value;
                const nextEvent = data.eventos.find((item) => item.idEvento === next);
                setEventId(next);
                setSectorId(nextEvent?.sectoresHabilitados[0] ?? "");
              }}>
                {data.eventos.map((item) => <option key={item.idEvento} value={item.idEvento}>{eventName(data, item.idEvento)}</option>)}
              </select>
            </label>
            <label className="text-sm font-bold text-slate-600">Sector
              <select className="input mt-1" value={sectorId} onChange={(e) => setSectorId(e.target.value)}>
                {sectors.map((id) => {
                  const item = getSector(data, id);
                  return item ? <option key={id} value={id}>{item.nombreSector} · {money(item.costo)}</option> : null;
                })}
              </select>
            </label>
            <label className="text-sm font-bold text-slate-600">Cantidad
              <input className="input mt-1" type="number" min={1} max={5} value={qty} onChange={(e) => setQty(Math.min(5, Math.max(1, Number(e.target.value))))} />
            </label>
          </div>
          {sector && event ? (
            <p className="mt-4 rounded-xl bg-slate-50 p-3 text-sm text-slate-600">
              Disponibles visuales: {sector.capacidad - soldTickets(data, event.idEvento, sector.idSector)} · {getStadium(data, event.idEstadio)?.nombre}
            </p>
          ) : null}
        </div>
        <aside className="card p-5">
          <p className="text-sm font-bold text-slate-500">Resumen</p>
          <div className="mt-4 space-y-2 text-sm">
            <div className="flex justify-between"><span>Subtotal</span><strong>{money(subtotal)}</strong></div>
            <div className="flex justify-between"><span>Comisión 5%</span><strong>{money(fee)}</strong></div>
            <div className="flex justify-between border-t pt-3 text-lg"><span>Total</span><strong>{money(total)}</strong></div>
          </div>
          <button className="btn-primary mt-5 w-full" onClick={confirm}>Confirmar compra</button>
          {message ? <p className="mt-3 rounded-xl bg-teal-50 p-3 text-sm font-bold text-teal-800">{message}</p> : null}
        </aside>
      </div>
    </>
  );
};
