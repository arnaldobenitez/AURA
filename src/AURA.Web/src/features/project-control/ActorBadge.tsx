import type { ProjectActorTypeCode } from "../../lib/projectControl";
import { actorTypeLabels } from "./labels";

interface ActorBadgeProps {
  type: ProjectActorTypeCode | null;
  name: string | null;
  emphasize?: boolean;
}

export function ActorBadge({ type, name, emphasize = false }: ActorBadgeProps) {
  if (!type) {
    return <span className="pc-actor pc-actor--empty">—</span>;
  }

  return (
    <span className={`pc-actor pc-actor--${type.toLowerCase()}${emphasize ? " pc-actor--emphasis" : ""}`}>
      <span className="pc-actor__dot" aria-hidden="true" />
      <span className="pc-actor__type">{actorTypeLabels[type]}</span>
      {name && <span className="pc-actor__name">{name}</span>}
    </span>
  );
}
