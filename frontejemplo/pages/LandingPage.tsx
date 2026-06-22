import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useApp } from "@/context/AppContext";
import type { Role } from "@/types";

const roleText: Record<Role, { title: string; text: string }> = {
  usuario: {
    title: "Usuario General",
    text: "Compra, recibe, transfiere y valida el estado de sus entradas dinámicas.",
  },
  administrador: {
    title: "Administrador",
    text: "Gestiona estadios, sectores, eventos y reportes por país sede.",
  },
  funcionario: {
    title: "Funcionario",
    text: "Valida QR/tokens en dispositivos autorizados y audita accesos.",
  },
};

export const LandingPage = () => {
  const { demoUsers, setCurrentUser } = useApp();
  const [role, setRole] = useState<Role>("usuario");
  const navigate = useNavigate();
  const selected = demoUsers.find((user) => user.role === role) ?? demoUsers[0];

  return (
    <main className="min-h-screen bg-slate-950 text-white">
      <section className="mx-auto grid min-h-screen max-w-7xl gap-10 px-5 py-10 lg:grid-cols-[1.1fr_0.9fr] lg:items-center">
        <div>
          <div className="inline-flex rounded-full border border-white/15 bg-white/10 px-4 py-2 text-sm font-bold text-teal-100">
            Obligatorio BD2 · Mundial 2026
          </div>
          <h1 className="mt-8 max-w-4xl text-5xl font-black leading-tight md:text-7xl">
            Ticketing deportivo con entradas dinámicas y QR seguro.
          </h1>
          <p className="mt-6 max-w-2xl text-lg text-slate-300">
            Demo frontend cliente/servidor preparada para API REST. Refleja venta,
            transferencia, validación y administración de eventos deportivos.
          </p>
          <div className="mt-8 grid gap-3 sm:grid-cols-3">
            {Object.entries(roleText).map(([key, item]) => (
              <button
                key={key}
                onClick={() => setRole(key as Role)}
                className={`rounded-2xl border p-4 text-left transition ${
                  role === key ? "border-teal-300 bg-teal-400/15" : "border-white/10 bg-white/5 hover:bg-white/10"
                }`}
              >
                <p className="font-black">{item.title}</p>
                <p className="mt-2 text-sm text-slate-300">{item.text}</p>
              </button>
            ))}
          </div>
        </div>
        <div className="card border-white/10 bg-white p-6 text-slate-950">
          <p className="text-sm font-black uppercase tracking-wide text-teal-700">Entrar a la demo</p>
          <h2 className="mt-2 text-3xl font-black">{roleText[role].title}</h2>
          <p className="mt-2 text-slate-600">Usuario simulado: {selected.displayName}</p>
          <div className="mt-6 rounded-2xl bg-slate-50 p-4">
            <p className="text-sm font-bold text-slate-500">Documento</p>
            <p className="mt-1 font-black">
              {selected.documento.tipoDocumento} {selected.documento.paisDocumento} {selected.documento.numeroDocumento}
            </p>
          </div>
          <button
            className="btn-primary mt-6 w-full"
            onClick={() => {
              setCurrentUser(selected);
              navigate("/app");
            }}
          >
            Entrar al dashboard
          </button>
        </div>
      </section>
    </main>
  );
};
