import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useApp } from "@/context/AppContext";
import type { Role } from "@/types";

const navByRole: Record<Role, Array<{ to: string; label: string }>> = {
  usuario: [
    { to: "/app", label: "Dashboard" },
    { to: "/app/registro", label: "Registro" },
    { to: "/app/eventos", label: "Eventos" },
    { to: "/app/comprar", label: "Comprar" },
    { to: "/app/entradas", label: "Mis entradas" },
    { to: "/app/compras", label: "Mis compras" },
    { to: "/app/transferencias", label: "Transferencias" },
  ],
  administrador: [
    { to: "/app", label: "Dashboard" },
    { to: "/app/admin/estadios", label: "Estadios" },
    { to: "/app/admin/sectores", label: "Sectores" },
    { to: "/app/admin/eventos", label: "Eventos" },
    { to: "/app/admin/equipos", label: "Equipos" },
    { to: "/app/reportes", label: "Reportes" },
  ],
  funcionario: [
    { to: "/app", label: "Dashboard" },
    { to: "/app/validacion", label: "Validación" },
  ],
};

export const AppLayout = () => {
  const { currentUser } = useApp();
  const navigate = useNavigate();
  const nav = navByRole[currentUser.role];

  return (
    <div className="min-h-screen bg-slate-50">
      <aside className="fixed inset-y-0 left-0 hidden w-72 border-r border-slate-200 bg-white p-5 lg:block">
        <button className="text-left" onClick={() => navigate("/")}>
          <div className="text-xl font-black text-slate-950">WorldCup Tickets</div>
          <div className="text-sm font-bold text-teal-700">Demo BD2 · Mundial 2026</div>
        </button>
        <div className="mt-6 rounded-2xl bg-slate-950 p-4 text-white">
          <p className="text-sm font-bold text-slate-300">Rol activo</p>
          <p className="mt-1 text-lg font-black">{currentUser.displayName}</p>
          <p className="text-sm capitalize text-slate-300">{currentUser.role}</p>
        </div>
        <nav className="mt-6 space-y-1">
          {nav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/app"}
              className={({ isActive }) =>
                `block rounded-xl px-4 py-3 text-sm font-bold transition ${
                  isActive ? "bg-teal-700 text-white" : "text-slate-700 hover:bg-slate-100"
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 px-4 py-3 backdrop-blur lg:hidden">
        <div className="font-black text-slate-950">WorldCup Tickets</div>
        <div className="mt-3 flex gap-2 overflow-x-auto pb-1">
          {nav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/app"}
              className={({ isActive }) =>
                `shrink-0 rounded-full px-3 py-2 text-xs font-bold ${
                  isActive ? "bg-teal-700 text-white" : "bg-slate-100 text-slate-700"
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </div>
      </header>

      <main className="p-4 lg:ml-72 lg:p-8">
        <Outlet />
      </main>
    </div>
  );
};
