import { useParams } from "react-router-dom";
import { Badge } from "@/components/Badge";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { dateTime, money } from "@/utils/format";
import { eventName, getSector } from "@/utils/selectors";

export const TicketDetailPage = () => {
  const { id } = useParams();
  const { data, regenerateToken } = useApp();
  const ticket = data.entradas.find((item) => item.idEntrada === id);
  const transfers = data.transferencias.filter((item) => item.idEntrada === id);

  if (!ticket) return <PageHeader title="Entrada no encontrada" />;

  return (
    <>
      <PageHeader title="Detalle de entrada / QR dinámico" subtitle="En producción el token mutaría cada 30 segundos." />
      <div className="grid gap-5 lg:grid-cols-[1fr_360px]">
        <section className="card p-5">
          <Badge value={ticket.estadoEntrada} />
          <h2 className="mt-4 text-2xl font-black">{eventName(data, ticket.idEvento)}</h2>
          <div className="mt-4 grid gap-3 md:grid-cols-2">
            <p><span className="font-bold text-slate-500">Sector:</span> {getSector(data, ticket.idSector)?.nombreSector}</p>
            <p><span className="font-bold text-slate-500">Costo:</span> {money(ticket.costo)}</p>
            <p><span className="font-bold text-slate-500">Emitida:</span> {dateTime(ticket.fechaEmision)}</p>
            <p><span className="font-bold text-slate-500">Último token:</span> {dateTime(ticket.fechaUltimoToken)}</p>
          </div>
          <h3 className="mt-6 font-black">Historial de transferencias</h3>
          <div className="mt-3 space-y-2">
            {transfers.length ? transfers.map((transfer) => (
              <div key={transfer.idTransferencia} className="rounded-xl bg-slate-50 p-3 text-sm">
                {transfer.usuarioOtorga.numeroDocumento} → {transfer.usuarioRecibe.numeroDocumento} · {transfer.estadoTransferencia}
              </div>
            )) : <p className="text-sm text-slate-500">Sin transferencias para esta entrada.</p>}
          </div>
        </section>
        <aside className="card p-5 text-center">
          <div className="mx-auto grid aspect-square max-w-64 place-items-center rounded-3xl bg-slate-950 p-6 text-white">
            <div className="grid h-full w-full grid-cols-5 gap-1">
              {Array.from({ length: 25 }, (_, index) => (
                <div key={index} className={index % 3 === 0 || ticket.tokenActual.charCodeAt(index % ticket.tokenActual.length) % 2 === 0 ? "bg-white" : "bg-slate-700"} />
              ))}
            </div>
          </div>
          <p className="mt-4 break-all rounded-xl bg-slate-50 p-3 text-sm font-black">{ticket.tokenActual}</p>
          <button className="btn-secondary mt-4 w-full" onClick={() => regenerateToken(ticket.idEntrada)}>
            Regenerar token
          </button>
        </aside>
      </div>
    </>
  );
};
