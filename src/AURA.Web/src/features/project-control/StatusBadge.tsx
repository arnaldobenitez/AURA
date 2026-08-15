import type { ProjectStatusCode } from "../../lib/projectControl";
import { projectStatusLabels } from "./labels";

export function StatusBadge({ status }: { status: ProjectStatusCode }) {
  return (
    <span className={`pc-badge pc-badge--status-${status.toLowerCase()}`}>
      {projectStatusLabels[status]}
    </span>
  );
}
