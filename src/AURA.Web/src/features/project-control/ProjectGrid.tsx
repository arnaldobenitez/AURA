import type { ProjectControlProject } from "../../lib/projectControl";
import { ActorBadge } from "./ActorBadge";
import { StatusBadge } from "./StatusBadge";
import { formatDateTimeUtc } from "./labels";

interface ProjectGridProps {
  projects: ProjectControlProject[];
}

export function ProjectGrid({ projects }: ProjectGridProps) {
  if (projects.length === 0) {
    return <p className="pc-empty">No hay proyectos para este tenant todavía.</p>;
  }

  return (
    <div className="pc-grid-scroll">
      <table className="pc-grid">
        <colgroup>
          <col className="pc-col-project" />
          <col className="pc-col-status" />
          <col className="pc-col-activity" />
          <col className="pc-col-actor" />
          <col className="pc-col-model" />
          <col className="pc-col-control" />
          <col className="pc-col-summary" />
          <col className="pc-col-next" />
          <col className="pc-col-blocker" />
          <col className="pc-col-reference" />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">Proyecto</th>
            <th scope="col">Estado</th>
            <th scope="col">Última actividad</th>
            <th scope="col">Último actor</th>
            <th scope="col">Modelo</th>
            <th scope="col">Control actual</th>
            <th scope="col">Último avance</th>
            <th scope="col">Próxima acción</th>
            <th scope="col">Bloqueo</th>
            <th scope="col">Referencia</th>
          </tr>
        </thead>
        <tbody>
          {projects.map((project) => (
            <tr key={project.projectId} className={project.projectStatus === "BLOCKED" ? "pc-row--blocked" : undefined}>
              <th scope="row" className="pc-grid__project">
                <span className="pc-grid__project-name">{project.projectName}</span>
                {project.projectCode && <span className="pc-grid__project-code">{project.projectCode}</span>}
              </th>
              <td>
                <StatusBadge status={project.projectStatus} />
              </td>
              <td>{formatDateTimeUtc(project.lastActivityUtc)}</td>
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
                {project.blocker ? (
                  <span className="pc-truncate pc-blocker-text" title={project.blocker}>
                    {project.blocker}
                  </span>
                ) : (
                  "—"
                )}
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
