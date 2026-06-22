import { Badge } from "@/components/Badge";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { useApp } from "@/context/AppContext";
import { dateTime, money } from "@/utils/format";
import { userSales } from "@/utils/selectors";

export const PurchasesPage = () => {
  const { data, currentUser } = useApp();
  const sales = userSales(data, currentUser.documento);
  return (
    <>
      <PageHeader title="Mis compras" subtitle="Ventas confirmadas con detalle de entradas incluidas." />
      {!sales.length ? <EmptyState title="Sin compras" /> : null}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr><th className="p-4">Venta</th><th>Fecha</th><th>Estado</th><th>Entradas</th><th>Total</th></tr>
            </thead>
            <tbody>
              {sales.map((sale) => (
                <tr key={sale.idVenta} className="border-t border-slate-100">
                  <td className="p-4 font-black">{sale.idVenta}</td>
                  <td>{dateTime(sale.fechaVenta)}</td>
                  <td><Badge value={sale.estadoVenta} /></td>
                  <td>{sale.entradas.length}</td>
                  <td className="font-black">{money(sale.montoTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
};
