import { ArrowUpDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from "lucide-react";
import { useEffect, useState } from "react";
import type { ProjectIntervention } from "../../lib/projectControl";
import { ActorBadge } from "../project-control/ActorBadge";
import { InterventionSummary } from "../project-control/InterventionSummary";
import { eventTypeLabels, formatDateTimeUtc } from "../project-control/labels";

interface InterventionTimelineProps {
  interventions: ProjectIntervention[];
}

const PAGE_SIZE_OPTIONS = [10, 20, 30];

export function InterventionTimeline({ interventions }: InterventionTimelineProps) {
  // La API entrega el historial del mas antiguo al mas reciente. Por defecto se
  // muestra lo mas reciente primero; invertir el orden queda como accion del usuario.
  const [newestFirst, setNewestFirst] = useState(true);
  const [pageSize, setPageSize] = useState(10);
  const [page, setPage] = useState(1);

  const totalPages = Math.max(1, Math.ceil(interventions.length / pageSize));

  useEffect(() => {
    setPage((current) => Math.min(Math.max(1, current), totalPages));
  }, [totalPages]);

  if (interventions.length === 0) {
    return <p className="pc-empty">Todavía no hay intervenciones registradas para este proyecto.</p>;
  }

  const ordered = newestFirst ? [...interventions].reverse() : interventions;
  const visible = ordered.slice((page - 1) * pageSize, page * pageSize);
  const rangeStart = (page - 1) * pageSize + 1;
  const rangeEnd = Math.min(page * pageSize, ordered.length);

  const goToPage = (value: number) => {
    if (!Number.isNaN(value)) {
      setPage(Math.min(Math.max(1, Math.trunc(value)), totalPages));
    }
  };

  return (
    <div className="pd-timeline-wrapper">
      <button
        type="button"
        className="pd-timeline__order-toggle"
        onClick={() => {
          setNewestFirst((current) => !current);
          setPage(1);
        }}
      >
        <ArrowUpDown size={14} aria-hidden="true" />
        {newestFirst ? "Más reciente primero" : "Más antiguo primero"}
      </button>

      <ol className="pd-timeline">
        {visible.map((intervention) => {
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

      <div className="pd-pagination">
        <div className="pd-pagination__group">
          <label className="pd-pagination__label">
            Por página
            <select
              className="pd-pagination__select"
              value={pageSize}
              onChange={(event) => {
                setPageSize(Number(event.target.value));
                setPage(1);
              }}
            >
              {PAGE_SIZE_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>
          <span className="pd-pagination__info">
            {rangeStart}–{rangeEnd} de {ordered.length}
          </span>
        </div>

        <div className="pd-pagination__group" role="group" aria-label="Paginación del historial">
          <button
            type="button"
            className="pd-pagination__button"
            onClick={() => setPage(1)}
            disabled={page <= 1}
            title="Primera página"
            aria-label="Ir a la primera página"
          >
            <ChevronsLeft size={14} aria-hidden="true" />
          </button>
          <button
            type="button"
            className="pd-pagination__button"
            onClick={() => setPage(page - 1)}
            disabled={page <= 1}
            title="Página anterior"
            aria-label="Página anterior"
          >
            <ChevronLeft size={14} aria-hidden="true" />
          </button>
          <span className="pd-pagination__label">
            Página
            <input
              className="pd-pagination__input"
              type="number"
              min={1}
              max={totalPages}
              value={page}
              onChange={(event) => goToPage(event.target.valueAsNumber)}
              aria-label="Ir a una página determinada"
            />
            de {totalPages}
          </span>
          <button
            type="button"
            className="pd-pagination__button"
            onClick={() => setPage(page + 1)}
            disabled={page >= totalPages}
            title="Página siguiente"
            aria-label="Página siguiente"
          >
            <ChevronRight size={14} aria-hidden="true" />
          </button>
          <button
            type="button"
            className="pd-pagination__button"
            onClick={() => setPage(totalPages)}
            disabled={page >= totalPages}
            title="Última página"
            aria-label="Ir a la última página"
          >
            <ChevronsRight size={14} aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  );
}
