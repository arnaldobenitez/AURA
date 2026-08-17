interface ProgressIndicatorProps {
  value: number | null;
  label: string;
}

const progressFormatter = new Intl.NumberFormat("es-AR", {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

export function ProgressIndicator({ value, label }: ProgressIndicatorProps) {
  if (value === null) {
    return <span className="pc-progress--unknown">Sin informar</span>;
  }

  const boundedValue = Math.min(100, Math.max(0, value));
  const formattedValue = `${progressFormatter.format(value)}%`;
  const state = boundedValue === 100 ? "complete" : boundedValue === 0 ? "empty" : "active";

  return (
    <div
      className={`pc-progress pc-progress--${state}`}
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={boundedValue}
      aria-valuetext={formattedValue}
      title={`${label}: ${formattedValue}`}
    >
      <span className="pc-progress__value">{formattedValue}</span>
      <span className="pc-progress__track" aria-hidden="true">
        <span className="pc-progress__fill" style={{ width: `${boundedValue}%` }} />
      </span>
    </div>
  );
}
