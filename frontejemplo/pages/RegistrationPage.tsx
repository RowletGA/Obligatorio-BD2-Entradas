import { useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";

export const RegistrationPage = () => {
  const { currentUser } = useApp();
  const [phones, setPhones] = useState(["099123456"]);

  return (
    <>
      <PageHeader title="Registro de usuario" subtitle="Formulario mock preparado para persistir contra API REST." />
      <div className="card p-5">
        <div className="grid gap-4 md:grid-cols-3">
          <label className="text-sm font-bold text-slate-600">Tipo documento<select className="input mt-1" defaultValue={currentUser.documento.tipoDocumento}><option>CI</option><option>DNI</option><option>PASAPORTE</option></select></label>
          <label className="text-sm font-bold text-slate-600">País documento<input className="input mt-1" defaultValue={currentUser.documento.paisDocumento} /></label>
          <label className="text-sm font-bold text-slate-600">Número<input className="input mt-1" defaultValue={currentUser.documento.numeroDocumento} /></label>
          <label className="text-sm font-bold text-slate-600">Correo electrónico<input className="input mt-1" defaultValue="usuario@example.com" /></label>
          <label className="text-sm font-bold text-slate-600">País residencia<input className="input mt-1" defaultValue="Uruguay" /></label>
          <label className="text-sm font-bold text-slate-600">Localidad<input className="input mt-1" defaultValue="Montevideo" /></label>
          <label className="text-sm font-bold text-slate-600">Calle<input className="input mt-1" defaultValue="18 de Julio" /></label>
          <label className="text-sm font-bold text-slate-600">Número<input className="input mt-1" defaultValue="1234" /></label>
          <label className="text-sm font-bold text-slate-600">Código postal<input className="input mt-1" defaultValue="11100" /></label>
        </div>
        <div className="mt-6">
          <p className="font-black text-slate-900">Teléfonos</p>
          <div className="mt-3 space-y-2">
            {phones.map((phone, index) => (
              <input
                key={index}
                className="input"
                value={phone}
                onChange={(event) => setPhones((items) => items.map((item, i) => i === index ? event.target.value : item))}
              />
            ))}
          </div>
          <button className="btn-secondary mt-3" onClick={() => setPhones((items) => [...items, ""])}>
            Agregar teléfono
          </button>
        </div>
        <p className="mt-4 rounded-xl bg-amber-50 p-3 text-sm font-bold text-amber-800">
          Demo visual: el alta real se conectará al endpoint de usuarios.
        </p>
      </div>
    </>
  );
};
