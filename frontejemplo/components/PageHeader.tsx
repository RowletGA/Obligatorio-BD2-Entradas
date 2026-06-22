export const PageHeader = ({
  title,
  subtitle,
  action,
}: {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
}) => (
  <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
    <div>
      <p className="text-sm font-black uppercase tracking-wide text-teal-700">WorldCup Tickets 2026</p>
      <h1 className="mt-1 text-3xl font-black text-slate-950">{title}</h1>
      {subtitle ? <p className="mt-2 max-w-3xl text-slate-600">{subtitle}</p> : null}
    </div>
    {action}
  </div>
);
