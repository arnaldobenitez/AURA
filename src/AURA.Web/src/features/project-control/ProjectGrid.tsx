import type { ProjectControlProject } from "../../lib/projectControl";
import { ActorBadge } from "./ActorBadge";
import { ProgressIndicator } from "./ProgressIndicator";
import { ProjectStatusIndicator } from "./ProjectStatusIndicator";
import { formatDateTimeUtc } from "./labels";

interface ProjectGridProps {
  projects: ProjectControlProject[];
}

export function ProjectGrid({ projects }: ProjectGridProps) {
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
    <div className="pc-grid-scroll">
      <table className="pc-grid">
        <colgroup>
          <col className="pc-col-project" />
          <col className="pc-col-exec-description" />
          <col className="pc-col-stage" />
          <col className="pc-col-stage-progress" />
          <col className="pc-col-exec-progress" />
          <col className="pc-col-activity" />
          <col className="pc-col-actor" />
          <col className="pc-col-model" />
          <col className="pc-col-control" />
          <col className="pc-col-summary" />
          <col className="pc-col-next" />
          <col className="pc-col-reference" />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">Proyecto</th>
            <th scope="col">Descripción EXEC</th>
            <th scope="col">Etapa actual</th>
            <th scope="col">Avance etapa</th>
            <th scope="col">Avance EXEC</th>
            <th scope="col">Última actividad</th>
            <th scope="col">Último actor</th>
            <th scope="col">Modelo</th>
            <th scope="col">Control actual</th>
            <th scope="col">Último avance</th>
            <th scope="col">Próxima acción</th>
            <th scope="col">Referencia</th>
          </tr>
        </thead>
        <tbody>
          {projects.map((project) => (
            <tr
              key={project.projectId}
              className={project.blocker || project.projectStatus === "BLOCKED" ? "pc-row--blocked" : undefined}
              title={project.blocker ?? undefined}
            >
              <th scope="row" className="pc-grid__project">
                <span className="pc-grid__project-line">
                  <ProjectStatusIndicator status={project.projectStatus} />
                  <span className="pc-grid__project-name">{project.projectName}</span>
                </span>
                {project.projectCode && <span className="pc-grid__project-code">{project.projectCode}</span>}
                {project.blocker && <span className="aura-sr-only">Bloqueo: {project.blocker}</span>}
              </th>
              <td>
                <span className="pc-truncate" title={project.execDescription ?? undefined}>
                  {project.execDescription ?? <span className="pc-value--unknown">Sin informar</span>}
                </span>
              </td>
              <td>
                <span className="pc-truncate" title={project.currentStage ?? undefined}>
                  {project.currentStage ?? <span className="pc-value--unknown">Sin informar</span>}
                </span>
              </td>
              <td>
                <ProgressIndicator value={project.stageProgress} label="Avance de la etapa actual" />
              </td>
              <td>
                <ProgressIndicator value={project.execProgress} label="Avance total del EXEC" />
              </td>
              <td>{formatLastActivity(project.lastActivityUtc)}</td>
              <td>
                <ActorBadge type={project.lastActorType} name={project.lastActorName} />
              </td>
              <td>
                <span className="pc-model">{project.lastModel ?? "—"}</span>
              </td>
              <td>
                <ActorBadge type={project.currentControlType} name={project.currentControlName} emphasize />
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
              <td>
                <span className="pc-truncate" title={project.reference ?? undefined}>
                  {project.reference ?? "—"}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
