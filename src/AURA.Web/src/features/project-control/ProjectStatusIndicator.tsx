import type { ProjectStatusCode } from "../../lib/projectControl";
import { projectStatusLabels } from "./labels";

export function ProjectStatusIndicator({ status }: { status: ProjectStatusCode }) {
  const label = projectStatusLabels[status];

  return (
    <span
      className={`pc-project-status pc-project-status--${status.toLowerCase()}`}
      role="img"
      aria-label={`Estado: ${label}`}
      title={label}
    />
  );
}
