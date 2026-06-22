export const ProgressBar = ({ value, max }: { value: number; max: number }) => {
  const percent = Math.min(100, Math.round((value / Math.max(max, 1)) * 100));
  return (
    <div>
      <div className="h-2 overflow-hidden rounded-full bg-slate-100">
        <div className="h-full rounded-full bg-teal-700" style={{ width: `${percent}%` }} />
      </div>
      <p className="mt-1 text-xs font-bold text-slate-500">{percent}% ocupado</p>
    </div>
  );
};
