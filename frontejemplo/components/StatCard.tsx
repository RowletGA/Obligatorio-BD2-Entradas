export const StatCard = ({
  label,
  value,
  hint,
}: {
  label: string;
  value: string | number;
  hint?: string;
}) => (
  <div className="card p-5">
    <p className="text-sm font-bold text-slate-500">{label}</p>
    <p className="mt-2 text-3xl font-black text-slate-950">{value}</p>
    {hint ? <p className="mt-2 text-sm text-slate-500">{hint}</p> : null}
  </div>
);
