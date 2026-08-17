import type {
  ProjectActorTypeCode,
  ProjectEventTypeCode,
  ProjectStatusCode,
  ProjectWorkStatusCode,
} from "../../lib/projectControl";

export const projectStatusLabels: Record<ProjectStatusCode, string> = {
  DRAFT: "Borrador",
  ACTIVE: "Activo",
  PAUSED: "Suspendido",
  BLOCKED: "Bloqueado",
  COMPLETED: "Cerrado",
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

export interface FormattedDateTime {
  date: string;
  time: string;
}

export function formatDateTimeUtc(value: string | null): FormattedDateTime | null {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return {
    date: new Intl.DateTimeFormat("es-AR", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    }).format(date),
    time: new Intl.DateTimeFormat("es-AR", {
      hour: "2-digit",
      minute: "2-digit",
      hourCycle: "h23",
    }).format(date),
  };
}
