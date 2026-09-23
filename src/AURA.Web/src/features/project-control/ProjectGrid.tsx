import { useEffect, useRef } from "react";
import type { ProjectControlProject } from "../../lib/projectControl";
import { ControlSummary } from "./ControlSummary";
import { InterventionSummary } from "./InterventionSummary";
import { ProgressIndicator } from "./ProgressIndicator";
import { ProjectStatusIndicator } from "./ProjectStatusIndicator";
import { formatDateTimeUtc } from "./labels";

interface ProjectGridProps {
  projects: ProjectControlProject[];
  highlightedProjectId?: number | null;
  onProjectSelect: (project: ProjectControlProject) => void;
  onRoadmapOpen: (project: ProjectControlProject) => void;
}

export function ProjectGrid({ projects, highlightedProjectId = null, onProjectSelect, onRoadmapOpen }: ProjectGridProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (highlightedProjectId === null || !containerRef.current) {
      return;
    }

    const row = containerRef.current.querySelector<HTMLElement>(`[data-project-id="${highlightedProjectId}"]`);
    row?.scrollIntoView({ block: "center", behavior: "smooth" });
  }, [highlightedProjectId]);

  if (projects.length === 0) {
    return <p className="pc-empty">No hay proyectos para este tenant todavía.</p>;
  }

  const formatLastActivity = (value: string | null) => {
    const formatted = formatDateTimeUtc(value);
    if (!formatted) {
      return <span className="pc-value--unknown">Sin actividad</span>;
    }

    return (
      <time className="pc-activity" dateTime={value ?? undefined}>
        <span>{formatted.date}</span>
        <span>{formatted.time}</span>
      </time>
    );
  };

  return (
    <div className="pc-grid-scroll" ref={containerRef}>
      <table className="pc-grid">
        <colgroup>
          <col className="pc-col-project" />
          <col className="pc-col-exec-description" />
          <col className="pc-col-stage" />
          <col className="pc-col-exec-progress" />
          <col className="pc-col-activity" />
          <col className="pc-col-intervention" />
          <col className="pc-col-control" />
          <col className="pc-col-summary" />
          <col className="pc-col-next" />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">Proyecto</th>
            <th scope="col">Descripción EXEC</th>
            <th scope="col">Etapa actual</th>
            <th scope="col">Avance EXEC</th>
            <th scope="col">Última actividad</th>
            <th scope="col">Última intervención</th>
            <th scope="col">Control actual</th>
            <th scope="col">Último avance</th>
            <th scope="col">Próxima acción</th>
          </tr>
        </thead>
        <tbody>
          {projects.map((project) => (
            <tr
              key={project.projectId}
              data-project-id={project.projectId}
              className={[
                "pc-row--interactive",
                project.blocker || project.projectStatus === "BLOCKED" ? "pc-row--blocked" : null,
                project.projectId === highlightedProjectId ? "pc-row--highlighted" : null,
              ]
                .filter(Boolean)
                .join(" ") || undefined}
              title={project.blocker ?? undefined}
              onClick={() => onProjectSelect(project)}
            >
              <th scope="row" className="pc-grid__project">
                <span className="pc-grid__project-line">
                  <ProjectStatusIndicator status={project.projectStatus} />
                  <a
                    className="pc-grid__project-link"
                    href={`/proyectos/${encodeURIComponent(project.projectCode ?? String(project.projectId))}`}
                    onClick={(event) => {
                      event.preventDefault();
                      event.stopPropagation();
                      onProjectSelect(project);
                    }}
                  >
                    {project.projectName}
                  </a>
                </span>
                {project.projectCode && <span className="pc-grid__project-code">{project.projectCode}</span>}
                {project.humanResponsibleName && (
                  <span className="pc-grid__project-owner">{project.humanResponsibleName}</span>
                )}
                {project.blocker && <span className="aura-sr-only">Bloqueo: {project.blocker}</span>}
              </th>
              <td>
                <span className="pc-truncate" title={project.execDescription ?? undefined}>
                  {project.execDescription ?? <span className="pc-value--unknown">Sin informar</span>}
                </span>
              </td>
              <td>
                <button
                  type="button"
                  className="pc-grid__stage-button"
                  disabled={!project.projectCode}
                  onClick={(event) => {
                    event.stopPropagation();
                    onRoadmapOpen(project);
                  }}
                  aria-label={`Ver Roadmap de ${project.projectName}`}
                  title="Ver evolución del RoadMap"
                >
                  <span className="pc-truncate" title={project.currentStage ?? undefined}>
                    {project.currentStage ?? <span className="pc-value--unknown">Sin informar</span>}
                  </span>
                  <ProgressIndicator value={project.stageProgress} label="Avance de la etapa actual" />
                </button>
              </td>
              <td>
                <ProgressIndicator value={project.execProgress} label="Avance total del EXEC" />
              </td>
              <td>{formatLastActivity(project.lastActivityUtc)}</td>
              <td>
                <InterventionSummary
                  actorType={project.lastActorType}
                  actorName={project.lastActorName}
                  agent={project.lastAgent}
                  model={project.lastModel}
                  environment={project.lastEnvironment}
                />
              </td>
              <td>
                <ControlSummary
                  controlType={project.currentControlType}
                  controlName={project.currentControlName}
                  controlAgent={project.currentControlAgent}
                  controlModel={project.currentControlModel}
                  controlEnvironment={project.currentControlEnvironment}
                />
              </td>
              <td>
                <span className="pc-truncate" title={project.summary ?? undefined}>
                  {project.summary ?? "—"}
                </span>
              </td>
              <td>
                <span className="pc-truncate" title={project.nextAction ?? undefined}>
                  {project.nextAction ?? "—"}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
