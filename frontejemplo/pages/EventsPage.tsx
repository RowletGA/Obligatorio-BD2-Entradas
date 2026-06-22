import { Link } from "react-router-dom";
import { Badge } from "@/components/Badge";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { dateTime, money } from "@/utils/format";
import { getSector, getStadium, getTeam, soldTickets } from "@/utils/selectors";

export const EventsPage = () => {
  const { data } = useApp();
  return (
    <>
      <PageHeader title="Eventos disponibles" subtitle="Partidos con sectores habilitados y disponibilidad mock." />
      <div className="grid gap-4 xl:grid-cols-2">
        {data.eventos.map((event) => {
          const stadium = getStadium(data, event.idEstadio);
          return (
            <article key={event.idEvento} className="card p-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <Badge value={event.estadoEvento} />
                <span className="text-sm font-bold text-slate-500">{dateTime(event.fechaHora)}</span>
              </div>
              <h2 className="mt-4 text-2xl font-black text-slate-950">
                {getTeam(data, event.equipoLocal)?.pais} vs {getTeam(data, event.equipoVisitante)?.pais}
              </h2>
              <p className="mt-1 text-slate-600">{stadium?.nombre} · {stadium?.ubicacionLocalidad}</p>
              <div className="mt-4 grid gap-2 sm:grid-cols-2">
                {event.sectoresHabilitados.map((sectorId) => {
                  const sector = getSector(data, sectorId);
                  if (!sector) return null;
                  const sold = soldTickets(data, event.idEvento, sector.idSector);
                  return (
                    <div key={sectorId} className="rounded-xl border border-slate-200 p-3">
                      <p className="font-black">{sector.nombreSector}</p>
                      <p className="text-sm text-slate-500">{sold}/{sector.capacidad} vendidos · {money(sector.costo)}</p>
                    </div>
                  );
                })}
              </div>
              <Link className="btn-primary mt-5 w-full" to={`/app/comprar?evento=${event.idEvento}`}>
                Comprar entradas
              </Link>
            </article>
          );
        })}
      </div>
    </>
  );
};
