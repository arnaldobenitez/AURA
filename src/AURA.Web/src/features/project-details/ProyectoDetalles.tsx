import { AlertTriangle, ArrowLeft, RefreshCw } from "lucide-react";
import type { ReactNode } from "react";
import { ControlSummary } from "../project-control/ControlSummary";
import { HealthIndicator } from "../project-control/HealthIndicator";
import { InterventionSummary } from "../project-control/InterventionSummary";
import { eventTypeLabels, formatDateTimeUtc, projectStatusLabels, workStatusLabels } from "../project-control/labels";
import { ProgressIndicator } from "../project-control/ProgressIndicator";
import { ProjectStatusIndicator } from "../project-control/ProjectStatusIndicator";
import { useProjectControlBoard } from "../project-control/useProjectControlBoard";
import { InterventionTimeline } from "./InterventionTimeline";
import { useProjectHistory } from "./useProjectHistory";

const updatedFormatter = new Intl.DateTimeFormat("es-AR", { dateStyle: "medium", timeStyle: "short" });

interface ProyectoDetallesProps {
  projectKey: string;
  onBack: () => void;
}

function DetailValue({ children }: { children: ReactNode }) {
  return <dd>{children ?? <span className="pc-value--unknown">Sin informar</span>}</dd>;
}

export function ProyectoDetalles({ projectKey, onBack }: ProyectoDetallesProps) {
  const { board, isLoading, error, lastUpdated, refresh } = useProjectControlBoard();
  const normalizedKey = projectKey.trim().toUpperCase();
  const project = board?.projects.find(
    (candidate) =>
      candidate.projectCode?.toUpperCase() === normalizedKey || String(candidate.projectId) === projectKey,
  );
  const {
    history,
    isLoading: isHistoryLoading,
    error: historyError,
    refresh: refreshHistory,
  } = useProjectHistory(project?.projectCode ?? null);

  const handleRefresh = () => {
    void refresh();
    void refreshHistory();
  };

  if (!board && isLoading) {
    return (
      <div className="pd-page">
        <button type="button" className="pd-back" onClick={onBack}>
          <ArrowLeft size={16} aria-hidden="true" /> Volver
        </button>
        <p className="pc-loading">Cargando detalle del proyecto…</p>
      </div>
    );
  }

  if (!project) {
    return (
      <div className="pd-page">
        <button type="button" className="pd-back" onClick={onBack}>
          <ArrowLeft size={16} aria-hidden="true" /> Volver
        </button>
        <div className="pd-empty">
          <h1>Proyecto no encontrado</h1>
          <p>No existe un proyecto identificado como {projectKey} en el tenant actual.</p>
        </div>
      </div>
    );
  }

  const activity = formatDateTimeUtc(project.lastActivityUtc);

  return (
    <div className="pd-page">
      <header className="pd-header">
        <div className="pd-header__main">
          <button type="button" className="pd-back" onClick={onBack}>
            <ArrowLeft size={16} aria-hidden="true" /> Volver
          </button>
          <div className="pd-identity">
            <div className="pd-title-line">
              <ProjectStatusIndicator status={project.projectStatus} />
              <h1>{project.projectName}</h1>
            </div>
            <div className="pd-subtitle">
              <span>{project.projectCode ?? `Proyecto ${project.projectId}`}</span>
              <span aria-hidden="true">·</span>
              <span>{projectStatusLabels[project.projectStatus]}</span>
              {project.humanResponsibleName && (
                <>
                  <span aria-hidden="true">·</span>
                  <span>Responsable: {project.humanResponsibleName}</span>
                </>
              )}
            </div>
          </div>
        </div>
        <div className="pd-header__actions">
          <HealthIndicator />
          <span>{lastUpdated ? `Actualizado ${updatedFormatter.format(lastUpdated)}` : "Sin actualizar"}</span>
          <button
            type="button"
            className="pd-icon-button"
            onClick={handleRefresh}
            disabled={isLoading || isHistoryLoading}
            title="Actualizar detalle"
            aria-label="Actualizar detalle"
          >
            <RefreshCw size={16} aria-hidden="true" className={isLoading || isHistoryLoading ? "pd-spin" : undefined} />
          </button>
        </div>
      </header>

      {error && <p className="pc-error-banner">{error} Mostrando los últimos datos disponibles.</p>}
      {historyError && <p className="pc-error-banner">{historyError}</p>}

      {project.blocker && (
        <div className="pd-blocker" role="alert">
          <AlertTriangle size={18} aria-hidden="true" />
          <div>
            <strong>Bloqueo actual</strong>
            <p>{project.blocker}</p>
          </div>
        </div>
      )}

      <main className="pd-main">
        <section className="pd-situation" aria-labelledby="pd-situation-title">
          <h2 id="pd-situation-title">Situación actual</h2>
          <div className="pd-situation__grid">
            <div className="pd-progress-block">
              <span className="pd-eyebrow">Avance del EXEC</span>
              <ProgressIndicator value={project.execProgress} label="Avance total del EXEC" />
            </div>
            <div className="pd-progress-block">
              <span className="pd-eyebrow">Avance de la etapa</span>
              <ProgressIndicator value={project.stageProgress} label="Avance de la etapa actual" />
            </div>
            <div className="pd-control-block">
              <span className="pd-eyebrow">Control actual</span>
              <ControlSummary
                controlType={project.currentControlType}
                controlName={project.currentControlName}
                controlAgent={project.currentControlAgent}
                controlModel={project.currentControlModel}
                controlEnvironment={project.currentControlEnvironment}
              />
            </div>
          </div>
        </section>

        <div className="pd-content-grid">
          <section className="pd-section" aria-labelledby="pd-work-title">
            <h2 id="pd-work-title">Trabajo actual</h2>
            <dl className="pd-details-list">
              <div className="pd-detail pd-detail--wide">
                <dt>EXEC</dt>
                <DetailValue>{project.execId !== null ? `#${project.execId}` : null}</DetailValue>
              </div>
              <div className="pd-detail pd-detail--wide">
                <dt>Descripción del EXEC</dt>
                <DetailValue>{project.execDescription}</DetailValue>
              </div>
              <div className="pd-detail pd-detail--wide">
                <dt>Etapa actual</dt>
                <DetailValue>
                  {project.currentStage
                    ? `${project.stageId !== null ? `#${project.stageId} · ` : ""}${project.currentStage}`
                    : null}
                </DetailValue>
              </div>
              <div className="pd-detail pd-detail--wide">
                <dt>Último avance</dt>
                <DetailValue>{project.summary}</DetailValue>
              </div>
              <div className="pd-detail pd-detail--wide pd-detail--next">
                <dt>Próxima acción</dt>
                <DetailValue>{project.nextAction}</DetailValue>
              </div>
            </dl>
          </section>

          <section className="pd-section" aria-labelledby="pd-trace-title">
            <h2 id="pd-trace-title">Trazabilidad</h2>
            <dl className="pd-details-list pd-details-list--trace">
              <div className="pd-detail">
                <dt>Último evento</dt>
                <DetailValue>{project.lastEventType ? eventTypeLabels[project.lastEventType] : null}</DetailValue>
              </div>
              <div className="pd-detail">
                <dt>Estado del trabajo</dt>
                <DetailValue>{project.workStatus ? workStatusLabels[project.workStatus] : null}</DetailValue>
              </div>
              <div className="pd-detail pd-detail--wide">
                <dt>Última intervención</dt>
                <DetailValue>
                  <InterventionSummary
                    actorType={project.lastActorType}
                    actorName={project.lastActorName}
                    agent={project.lastAgent}
                    model={project.lastModel}
                    environment={project.lastEnvironment}
                  />
                </DetailValue>
              </div>
              <div className="pd-detail">
                <dt>Última actividad</dt>
                <DetailValue>{activity ? `${activity.date} · ${activity.time}` : null}</DetailValue>
              </div>
              <div className="pd-detail">
                <dt>Último item</dt>
                <DetailValue>{project.lastItemId ? `#${project.lastItemId}` : null}</DetailValue>
              </div>
              <div className="pd-detail pd-detail--wide">
                <dt>Referencia</dt>
                <DetailValue>{project.reference}</DetailValue>
              </div>
            </dl>
          </section>
        </div>

        <section className="pd-section pd-section--timeline" aria-labelledby="pd-history-title">
          <h2 id="pd-history-title">Historial cronológico</h2>
          {!project.projectCode && (
            <p className="pc-value--unknown">Este proyecto no tiene código asignado: no se puede cargar su historial.</p>
          )}
          {project.projectCode && isHistoryLoading && !history && <p className="pc-loading">Cargando historial…</p>}
          {project.projectCode && history && <InterventionTimeline interventions={history.interventions} />}
        </section>
      </main>
    </div>
  );
}
