import { Link } from "react-router-dom";
import { Badge } from "@/components/Badge";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { money } from "@/utils/format";
import { eventName, getSector, userTickets } from "@/utils/selectors";

export const TicketsPage = () => {
  const { data, currentUser } = useApp();
  const tickets = userTickets(data, currentUser.documento);

  return (
    <>
      <PageHeader title="Mis entradas" subtitle="Entradas actuales, QR dinámico y transferencia." />
      {!tickets.length ? <EmptyState title="No hay entradas" text="Comprá una entrada para verla acá." /> : null}
      <div className="grid gap-4 xl:grid-cols-2">
        {tickets.map((ticket) => (
          <article key={ticket.idEntrada} className="card p-5">
            <div className="flex items-center justify-between gap-3">
              <Badge value={ticket.estadoEntrada} />
              <span className="text-sm font-black text-slate-500">{ticket.idEntrada}</span>
            </div>
            <h2 className="mt-4 text-xl font-black">{eventName(data, ticket.idEvento)}</h2>
            <p className="mt-1 text-slate-600">{getSector(data, ticket.idSector)?.nombreSector} · {money(ticket.costo)}</p>
            <div className="mt-5 flex flex-wrap gap-2">
              <Link className="btn-primary" to={`/app/entradas/${ticket.idEntrada}`}>Ver QR</Link>
              <Link className="btn-secondary" to={`/app/transferencias?entrada=${ticket.idEntrada}`}>Transferir</Link>
            </div>
          </article>
        ))}
      </div>
    </>
  );
};
