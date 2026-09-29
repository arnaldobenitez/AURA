import { useState } from "react";
import { RoadmapModal } from "../project-details/RoadmapModal";
import { CreateProjectModal } from "./CreateProjectModal";
import { HealthIndicator } from "./HealthIndicator";
import { ProjectGrid } from "./ProjectGrid";
import { SummaryCards } from "./SummaryCards";
import { useProjectControlAlerts } from "./useProjectControlAlerts";
import { useProjectControlBoard } from "./useProjectControlBoard";
import type { ProjectControlProject } from "../../lib/projectControl";

const lastUpdatedFormatter = new Intl.DateTimeFormat("es-AR", { dateStyle: "medium", timeStyle: "medium" });
const TENANT_ID = 1;
const HIGHLIGHT_DURATION_MS = 4_000;

export function ProjectControlPage({ onProjectSelect, canManageProjects = false }: { onProjectSelect: (project: ProjectControlProject) => void; canManageProjects?: boolean }) {
  const { board, isLoading, error, lastUpdated, refresh } = useProjectControlBoard();
  const { soundEnabled, setSoundEnabled, needsAudioUnlock, unlockAudio } = useProjectControlAlerts(board);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [highlightedProjectId, setHighlightedProjectId] = useState<number | null>(null);
  const [roadmapProject, setRoadmapProject] = useState<ProjectControlProject | null>(null);

  const isInitialLoading = board === null && isLoading;

  const handleProjectCreated = (projectId: number) => {
    setIsCreateModalOpen(false);
    void refresh();
    setHighlightedProjectId(projectId);
    window.setTimeout(() => setHighlightedProjectId(null), HIGHLIGHT_DURATION_MS);
  };

  return (
    <div className="pc-page">
      <header className="pc-header">
        <div className="pc-header__identity">
          <h1 className="pc-header__title">Control de proyectos IA</h1>
        </div>
        <div className="pc-header__meta">
          <span className="pc-header__tenant">Tenant 1</span>
          <HealthIndicator />
          <button
            type="button"
            className="pc-sound-toggle"
            onClick={() => setSoundEnabled(!soundEnabled)}
            aria-pressed={soundEnabled}
            title={soundEnabled ? "Silenciar alertas sonoras" : "Activar alertas sonoras"}
          >
            {soundEnabled ? "🔊" : "🔇"}
          </button>
          {canManageProjects ? <button
            type="button"
            className="pc-sound-toggle"
            onClick={() => setIsCreateModalOpen(true)}
            title="Nuevo proyecto"
          >
            +
          </button> : null}
          <button type="button" className="pc-refresh-button" onClick={() => void refresh()} disabled={isLoading}>
            {isLoading ? "Actualizando…" : "Actualizar"}
          </button>
          <span className="pc-header__updated">
            {lastUpdated ? `Última actualización: ${lastUpdatedFormatter.format(lastUpdated)}` : "Sin actualizar todavía"}
          </span>
        </div>
      </header>

      <main className="pc-main">
        {needsAudioUnlock && (
          <p className="pc-audio-unlock-hint" role="status">
            El navegador bloqueó la reproducción automática de sonido.{" "}
            <button type="button" onClick={unlockAudio}>
              Click para activar los avisos sonoros
            </button>
          </p>
        )}

        {error && (
          <p className="pc-error-banner" role="alert">
            {error} Mostrando los últimos datos disponibles.
          </p>
        )}

        {isInitialLoading && <p className="pc-loading">Cargando proyectos…</p>}

        {!isInitialLoading && board && (
          <>
            <SummaryCards projects={board.projects} />
            <ProjectGrid
              projects={board.projects}
              highlightedProjectId={highlightedProjectId}
              onProjectSelect={onProjectSelect}
              onRoadmapOpen={setRoadmapProject}
            />
          </>
        )}
      </main>

      {canManageProjects ? <CreateProjectModal
        open={isCreateModalOpen}
        tenantId={TENANT_ID}
        onClose={() => setIsCreateModalOpen(false)}
        onCreated={handleProjectCreated}
      /> : null}
      <RoadmapModal
        open={roadmapProject !== null}
        projectCode={roadmapProject?.projectCode ?? null}
        tenantId={TENANT_ID}
        workStream={roadmapProject?.workStream ?? null}
        onClose={() => setRoadmapProject(null)}
      />
    </div>
  );
}
