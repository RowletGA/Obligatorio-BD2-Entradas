import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { money } from "@/utils/format";
import { eventName, getSector, getUser, soldTickets } from "@/utils/selectors";

const Bar = ({ label, value, max, detail }: { label: string; value: number; max: number; detail?: string }) => (
  <div>
    <div className="mb-1 flex justify-between gap-3 text-sm">
      <span className="font-bold text-slate-700">{label}</span>
      <span className="text-slate-500">{detail ?? value}</span>
    </div>
    <div className="h-3 overflow-hidden rounded-full bg-slate-100">
      <div className="h-full rounded-full bg-teal-700" style={{ width: `${Math.min(100, (value / Math.max(max, 1)) * 100)}%` }} />
    </div>
  </div>
);

export const ReportsPage = () => {
  const { data } = useApp();
  const salesByEvent = data.eventos.map((event) => ({
    event,
    sold: soldTickets(data, event.idEvento),
    total: data.entradas.filter((ticket) => ticket.idEvento === event.idEvento).reduce((sum, ticket) => sum + ticket.costo, 0),
  }));
  const maxSold = Math.max(...salesByEvent.map((item) => item.sold), 1);
  const buyerRanking = data.ventas.map((sale) => ({
    buyer: getUser(data, sale.comprador),
    total: sale.montoTotal,
  })).sort((a, b) => b.total - a.total);

  return (
    <>
      <PageHeader title="Reportes" subtitle="Indicadores mock con barras CSS livianas." />
      <div className="grid gap-5 xl:grid-cols-2">
        <section className="card p-5">
          <h2 className="font-black">Eventos con más entradas vendidas</h2>
          <div className="mt-4 space-y-4">
            {salesByEvent.map((item) => <Bar key={item.event.idEvento} label={eventName(data, item.event.idEvento)} value={item.sold} max={maxSold} />)}
          </div>
        </section>
        <section className="card p-5">
          <h2 className="font-black">Ranking de mayores compradores</h2>
          <div className="mt-4 space-y-4">
            {buyerRanking.map((item, index) => <Bar key={index} label={`${item.buyer?.nombre ?? "Usuario"} ${item.buyer?.apellido ?? ""}`} value={item.total} max={Math.max(...buyerRanking.map((r) => r.total), 1)} detail={money(item.total)} />)}
          </div>
        </section>
        <section className="card p-5">
          <h2 className="font-black">Ventas por evento</h2>
          <div className="mt-4 space-y-4">
            {salesByEvent.map((item) => <Bar key={item.event.idEvento} label={eventName(data, item.event.idEvento)} value={item.total} max={Math.max(...salesByEvent.map((r) => r.total), 1)} detail={money(item.total)} />)}
          </div>
        </section>
        <section className="card p-5">
          <h2 className="font-black">Ocupación por sector</h2>
          <div className="mt-4 space-y-4">
            {data.sectores.slice(0, 8).map((sector) => {
              const sold = data.entradas.filter((ticket) => ticket.idSector === sector.idSector).length;
              return <Bar key={sector.idSector} label={getSector(data, sector.idSector)?.nombreSector ?? sector.idSector} value={sold} max={sector.capacidad} detail={`${sold}/${sector.capacidad}`} />;
            })}
          </div>
        </section>
      </div>
    </>
  );
};
