import { useEffect, useRef, useState } from "react";
import { fetchProjectRoadmap, type ProjectRoadmap, type RoadmapStageStatus } from "../../lib/projectControl";
import { formatDateTimeUtc } from "../project-control/labels";

interface RoadmapModalProps {
  open: boolean;
  projectCode: string | null;
  tenantId?: number;
  workStream?: string | null;
  onClose: () => void;
}

const statusLabels: Record<RoadmapStageStatus, string> = {
  PENDING: "Pendiente",
  IN_PROGRESS: "En progreso",
  PENDING_REVIEW: "Pendiente de revisión",
  PASS: "PASS",
  PASS_WITH_WARNINGS: "PASS WITH WARNINGS",
};

export function RoadmapModal({ open, projectCode, tenantId = 1, workStream = null, onClose }: RoadmapModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [roadmap, setRoadmap] = useState<ProjectRoadmap | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return;
    }
    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  useEffect(() => {
    if (!open || !projectCode) {
      setRoadmap(null);
      setError(null);
      setIsLoading(false);
      return;
    }

    const controller = new AbortController();
    setRoadmap(null);
    setError(null);
    setIsLoading(true);
    void fetchProjectRoadmap(tenantId, projectCode, workStream, controller.signal)
      .then(setRoadmap)
      .catch((err: unknown) => {
        if (!controller.signal.aborted) {
          setError(err instanceof Error ? err.message : "No se pudo cargar el Roadmap.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [open, projectCode, tenantId, workStream]);

  const globalProgress = roadmap?.execProgress ?? 0;
  const remaining = Math.max(0, 100 - globalProgress);

  return (
    <dialog ref={dialogRef} className="pc-modal roadmap-modal" onCancel={onClose} onClose={onClose}>
      <section className="roadmap-modal__content" aria-labelledby="roadmap-modal-title">
        <header className="roadmap-modal__header">
          <div>
            <h2 id="roadmap-modal-title">Evolución del RoadMap</h2>
            {roadmap && <p>{roadmap.projectName}{roadmap.execId ? ` · EXEC #${roadmap.execId}` : ""}{roadmap.workStream ? ` · Workstream ${roadmap.workStream}` : ""}</p>}
          </div>
          <button type="button" className="pc-sound-toggle" onClick={onClose} aria-label="Cerrar Roadmap">
            ×
          </button>
        </header>

        {isLoading && <p className="pc-loading">Cargando Roadmap…</p>}
        {error && <p className="pc-error-banner" role="alert">{error}</p>}
        {!isLoading && roadmap && (
          <>
            {roadmap.totalStages === 0 ? (
              <p className="pc-empty">Este proyecto todavía no tiene un Roadmap definido para el EXEC actual.</p>
            ) : (
              <>
                <div className="roadmap-modal__summary">
                  <strong>{globalProgress.toFixed(2)}% completado</strong>
                  <span>{remaining.toFixed(2)}% pendiente</span>
                  <span>{roadmap.closedStages} de {roadmap.totalStages} hitos cerrados</span>
                  <div className="roadmap-modal__progress" role="progressbar" aria-valuenow={globalProgress} aria-valuemin={0} aria-valuemax={100} aria-label="Avance global del Roadmap">
                    <span style={{ width: `${globalProgress}%` }} />
                  </div>
                  <small>El porcentaje global cuenta hitos con PASS o PASS WITH WARNINGS. El avance parcial se muestra por etapa y no se suma al global.</small>
                </div>

                <div className="roadmap-modal__table-wrap">
                  <table className="roadmap-modal__table">
                    <thead>
                      <tr>
                        <th scope="col">Etapa</th>
                        <th scope="col">Avance</th>
                        <th scope="col">Estado actual</th>
                        <th scope="col">Validador designado</th>
                        <th scope="col">Última revisión</th>
                      </tr>
                    </thead>
                    <tbody>
                      {roadmap.stages.map((stage) => {
                        const approvedAt = formatDateTimeUtc(stage.approvedAtUtc);
                        return (
                          <tr key={stage.stageId}>
                            <th scope="row">
                              <span className="roadmap-modal__stage-code">{stage.stageCode ?? `Etapa ${stage.order}`}</span>
                              <span>{stage.description}</span>
                              {stage.acceptanceCriteria && <small>{stage.acceptanceCriteria}</small>}
                            </th>
                            <td>{stage.progress === null ? "Sin informar" : `${stage.progress.toFixed(2)}%`}</td>
                            <td>
                              <span className={`roadmap-status roadmap-status--${stage.status.toLowerCase().replaceAll("_", "-")}`}>
                                {statusLabels[stage.status]}
                              </span>
                            </td>
                            <td>{stage.validatorName ?? (stage.validatorCredentialId ? `Credencial ${stage.validatorCredentialId}` : "Sin asignar")}</td>
                            <td>
                              {stage.approvalSummary ? (
                                <div className="roadmap-modal__review">
                                  <strong>{stage.approverAgent ?? "AI_ANALYST"}{stage.approverModel ? ` · ${stage.approverModel}` : ""}</strong>
                                  <span>{stage.approvalSummary}</span>
                                  {approvedAt && <small>{approvedAt.date} · {approvedAt.time}</small>}
                                  {stage.evidenceReference && <small>{stage.evidenceReference}</small>}
                                </div>
                              ) : "—"}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </>
        )}
      </section>
    </dialog>
  );
}
