import type { ProjectControlProject } from "../../lib/projectControl";

interface SummaryCardsProps {
  projects: ProjectControlProject[];
}

export function SummaryCards({ projects }: SummaryCardsProps) {
  const total = projects.length;
  const active = projects.filter((p) => p.projectStatus === "ACTIVE").length;
  const blocked = projects.filter((p) => p.projectStatus === "BLOCKED").length;
  const noActivity = projects.filter((p) => p.lastActivityUtc === null).length;

  const cards = [
    { label: "Total de proyectos", value: total, modifier: "total" },
    { label: "Activos", value: active, modifier: "active" },
    { label: "Bloqueados", value: blocked, modifier: "blocked" },
    { label: "Sin actividad", value: noActivity, modifier: "idle" },
  ];

  return (
    <section className="pc-summary" aria-label="Resumen de proyectos">
      {cards.map((card) => (
        <div key={card.label} className={`pc-summary-card pc-summary-card--${card.modifier}`}>
          <span className="pc-summary-card__value">{card.value}</span>
          <span className="pc-summary-card__label">{card.label}</span>
        </div>
      ))}
    </section>
  );
}
