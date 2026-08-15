import type {
  ProjectActorTypeCode,
  ProjectEventTypeCode,
  ProjectStatusCode,
  ProjectWorkStatusCode,
} from "../../lib/projectControl";

export const projectStatusLabels: Record<ProjectStatusCode, string> = {
  ACTIVE: "Activo",
  PAUSED: "Pausado",
  BLOCKED: "Bloqueado",
  COMPLETED: "Completado",
  ARCHIVED: "Archivado",
  UNKNOWN: "Desconocido",
};

export const actorTypeLabels: Record<ProjectActorTypeCode, string> = {
  UNASSIGNED: "Sin asignar",
  HUMAN: "Humano",
  AI_ANALYST: "Analista IA",
  CODER: "Codificador",
};

export const workStatusLabels: Record<ProjectWorkStatusCode, string> = {
  IN_PROGRESS: "En curso",
  BLOCKED: "Bloqueado",
  COMPLETED: "Completado",
};

export const eventTypeLabels: Record<ProjectEventTypeCode, string> = {
  CLAIM: "Inicio",
  PROGRESS: "Avance",
  HANDOFF: "Handoff",
  BLOCKER: "Bloqueo",
  COMPLETION: "Finalización",
};

export function formatDateTimeUtc(value: string | null): string {
  if (!value) {
    return "Sin actividad";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Sin actividad";
  }

  return new Intl.DateTimeFormat("es-AR", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}
