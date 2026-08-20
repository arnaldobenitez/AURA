import { ArrowUpDown } from "lucide-react";
import { useState } from "react";
import type { ProjectIntervention } from "../../lib/projectControl";
import { ActorBadge } from "../project-control/ActorBadge";
import { InterventionSummary } from "../project-control/InterventionSummary";
import { eventTypeLabels, formatDateTimeUtc } from "../project-control/labels";

interface InterventionTimelineProps {
  interventions: ProjectIntervention[];
}

export function InterventionTimeline({ interventions }: InterventionTimelineProps) {
  // El circuito real es humano -> analisis -> humano -> codificacion -> humano: el orden
  // inicial es siempre mas antiguo primero, tal como llega la API. Invertir es una accion
  // explicita del usuario, no el estado por defecto.
  const [newestFirst, setNewestFirst] = useState(false);

  if (interventions.length === 0) {
    return <p className="pc-empty">Todavía no hay intervenciones registradas para este proyecto.</p>;
  }

  const ordered = newestFirst ? [...interventions].reverse() : interventions;

  return (
    <div className="pd-timeline-wrapper">
      <button
        type="button"
        className="pd-timeline__order-toggle"
        onClick={() => setNewestFirst((current) => !current)}
      >
        <ArrowUpDown size={14} aria-hidden="true" />
        {newestFirst ? "Más reciente primero" : "Más antiguo primero"}
      </button>

      <ol className="pd-timeline">
        {ordered.map((intervention) => {
          const formatted = formatDateTimeUtc(intervention.timestampUtc);

          return (
            <li key={intervention.itemId} className="pd-timeline__item">
              <div className="pd-timeline__marker" aria-hidden="true" />
              <div className="pd-timeline__body">
                <div className="pd-timeline__header">
                  <span className="pd-timeline__event">
                    {intervention.eventType ? eventTypeLabels[intervention.eventType] : "Evento"}
                  </span>
                  {formatted && (
                    <time className="pd-timeline__time">
                      {formatted.date} · {formatted.time}
                    </time>
                  )}
                </div>

                <InterventionSummary
                  actorType={intervention.actorType}
                  actorName={intervention.actorName}
                  agent={intervention.agent}
                  model={intervention.model}
                  environment={intervention.environment}
                />

                {intervention.summary && <p className="pd-timeline__summary">{intervention.summary}</p>}

                {intervention.nextControlType && (
                  <div className="pd-timeline__handoff">
                    <span>Control pasa a</span>
                    <ActorBadge type={intervention.nextControlType} name={intervention.nextControlName} emphasize />
                  </div>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
