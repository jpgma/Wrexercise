type ProgressBarProps = {
  label: string;
  valor: number;
  max: number;
  className?: string;
};

/** Uma barra contínua de progresso, para metas grandes demais para `Segments`. */
export function ProgressBar({
  label,
  valor,
  max,
  className = "",
}: ProgressBarProps) {
  const atual = Math.min(Math.max(valor, 0), max);
  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={max}
      aria-valuenow={atual}
      aria-valuetext={`${atual} de ${max}`}
      className={`h-2 overflow-hidden rounded-full bg-surface-muted ${className}`}
    >
      <div
        className="h-full rounded-full bg-progress"
        style={{ width: `${max > 0 ? (atual / max) * 100 : 0}%` }}
      />
    </div>
  );
}
