import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";

export const TeamsPage = () => {
  const { data } = useApp();
  return (
    <>
      <PageHeader title="Equipos" subtitle="Selecciones mock del Mundial 2026." />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {data.equipos.map((team) => (
          <article key={team.idEquipo} className="card p-5">
            <p className="text-sm font-black text-teal-700">Grupo {team.grupo}</p>
            <h2 className="mt-2 text-2xl font-black">{team.pais}</h2>
            <p className="mt-1 text-sm text-slate-500">{team.idEquipo}</p>
          </article>
        ))}
      </div>
    </>
  );
};
