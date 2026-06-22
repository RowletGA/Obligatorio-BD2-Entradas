export const EmptyState = ({ title, text }: { title: string; text?: string }) => (
  <div className="card p-8 text-center">
    <p className="text-lg font-black text-slate-900">{title}</p>
    {text ? <p className="mt-2 text-sm text-slate-500">{text}</p> : null}
  </div>
);
