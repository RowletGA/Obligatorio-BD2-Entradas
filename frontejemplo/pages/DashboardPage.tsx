import { Link } from "react-router-dom";
import { PageHeader } from "@/components/PageHeader";
import { StatCard } from "@/components/StatCard";
import { useApp } from "@/context/AppContext";
import { money } from "@/utils/format";
import { userSales, userTickets } from "@/utils/selectors";

export const DashboardPage = () => {
  const { data, currentUser } = useApp();
  const salesTotal = data.ventas.reduce((sum, sale) => sum + sale.montoTotal, 0);

  if (currentUser.role === "usuario") {
    const tickets = userTickets(data, currentUser.documento);
    const sales = userSales(data, currentUser.documento);
    return (
      <>
        <PageHeader title="Dashboard usuario" subtitle="Resumen de compras, entradas actuales y transferencias." />
        <div className="grid gap-4 md:grid-cols-3">
          <StatCard label="Entradas actuales" value={tickets.length} hint="Activas, transferidas o consumidas" />
          <StatCard label="Compras realizadas" value={sales.length} />
          <StatCard label="Transferencias" value={data.transferencias.length} />
        </div>
        <div className="mt-6 grid gap-4 md:grid-cols-3">
          <Link className="card p-5 font-black hover:border-teal-700" to="/app/eventos">Ver eventos disponibles</Link>
          <Link className="card p-5 font-black hover:border-teal-700" to="/app/comprar">Comprar entradas</Link>
          <Link className="card p-5 font-black hover:border-teal-700" to="/app/entradas">Mis entradas y QR</Link>
        </div>
      </>
    );
  }

  if (currentUser.role === "funcionario") {
    return (
      <>
        <PageHeader title="Dashboard funcionario" subtitle="Validación de acceso con token dinámico y auditoría." />
        <div className="grid gap-4 md:grid-cols-3">
          <StatCard label="Eventos disponibles" value={data.eventos.length} />
          <StatCard label="Validaciones registradas" value={data.validaciones.length} />
          <StatCard label="Dispositivo asignado" value={data.dispositivos[0]?.idDispositivo ?? "-"} />
        </div>
        <Link className="btn-primary mt-6" to="/app/validacion">Abrir validación de acceso</Link>
      </>
    );
  }

  return (
    <>
      <PageHeader title="Dashboard administrador" subtitle="Gestión por país sede y reportes operativos." />
      <div className="grid gap-4 md:grid-cols-4">
        <StatCard label="Estadios" value={data.estadios.length} />
        <StatCard label="Eventos" value={data.eventos.length} />
        <StatCard label="Entradas vendidas" value={data.entradas.length} />
        <StatCard label="Ventas totales" value={money(salesTotal)} />
      </div>
    </>
  );
};
