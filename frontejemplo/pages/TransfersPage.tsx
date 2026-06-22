import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Badge } from "@/components/Badge";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { dateTime, sameDocument } from "@/utils/format";
import { eventName, userTickets } from "@/utils/selectors";

export const TransfersPage = () => {
  const { data, currentUser, transferTicket } = useApp();
  const [params] = useSearchParams();
  const tickets = userTickets(data, currentUser.documento).filter((ticket) => ticket.estadoEntrada !== "consumida");
  const [ticketId, setTicketId] = useState(params.get("entrada") ?? tickets[0]?.idEntrada ?? "");
  const [receiver, setReceiver] = useState(data.usuarios[1]?.numeroDocumento ?? "");
  const [message, setMessage] = useState("");

  const submit = () => {
    const user = data.usuarios.find((item) => item.numeroDocumento === receiver);
    if (!user) return setMessage("No se encontró el usuario receptor.");
    try {
      transferTicket({ idEntrada: ticketId, receptor: user });
      setMessage("Transferencia aceptada automáticamente para la demo.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "No se pudo transferir.");
    }
  };

  const sent = data.transferencias.filter((item) => sameDocument(item.usuarioOtorga, currentUser.documento));
  const received = data.transferencias.filter((item) => sameDocument(item.usuarioRecibe, currentUser.documento));

  return (
    <>
      <PageHeader title="Transferencias" subtitle="Máximo 3 transferencias aceptadas por entrada." />
      <div className="grid gap-5 lg:grid-cols-[360px_1fr]">
        <section className="card p-5">
          <label className="text-sm font-bold text-slate-600">Entrada
            <select className="input mt-1" value={ticketId} onChange={(e) => setTicketId(e.target.value)}>
              {tickets.map((ticket) => <option key={ticket.idEntrada} value={ticket.idEntrada}>{ticket.idEntrada} · {eventName(data, ticket.idEvento)}</option>)}
            </select>
          </label>
          <label className="mt-4 block text-sm font-bold text-slate-600">Documento receptor
            <input className="input mt-1" value={receiver} onChange={(e) => setReceiver(e.target.value)} />
          </label>
          <button className="btn-primary mt-4 w-full" onClick={submit}>Confirmar transferencia</button>
          {message ? <p className="mt-3 rounded-xl bg-slate-50 p-3 text-sm font-bold">{message}</p> : null}
        </section>
        <section className="grid gap-4 md:grid-cols-2">
          {[["Enviadas", sent], ["Recibidas", received]].map(([title, rows]) => (
            <div className="card p-5" key={title as string}>
              <h2 className="font-black">{title as string}</h2>
              <div className="mt-3 space-y-3">
                {(rows as typeof sent).map((transfer) => (
                  <div key={transfer.idTransferencia} className="rounded-xl border border-slate-200 p-3">
                    <Badge value={transfer.estadoTransferencia} />
                    <p className="mt-2 text-sm">{transfer.usuarioOtorga.numeroDocumento} → {transfer.usuarioRecibe.numeroDocumento}</p>
                    <p className="text-xs text-slate-500">{transfer.idEntrada} · {dateTime(transfer.fechaTransferencia)}</p>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </section>
      </div>
    </>
  );
};
