import type {
  ProjectActorTypeCode,
  ProjectAgentCode,
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
  REQUEST: "Solicitud",
  CLAIM: "Inicio",
  PROGRESS: "Avance",
  HANDOFF: "Handoff",
  BLOCKER: "Bloqueo",
  APPROVAL: "Aprobación",
  REWORK: "Retrabajo",
  COMPLETION: "Finalización",
  DEPLOYMENT: "Publicación",
};

// Catalogo de nombres "bonitos" para los codigos legacy que todavia se graban en
// mayusculas (CODEX, CLAUDE_CODE...). No es un enum cerrado: un agente nuevo que no
// este en este diccionario simplemente se muestra tal cual llego (texto libre), en vez
// de romper o quedar sin mostrarse - ver "no closed enum" en la correccion del circuito.
const knownAgentLabels: Record<string, string> = {
  UNASSIGNED: "Sin asignar",
  CODEX: "Codex",
  CLAUDE_CODE: "Claude Code",
  GROK: "Grok",
  DEVIN: "Devin",
};

export function formatAgentLabel(agent: ProjectAgentCode | null | undefined): string | null {
  if (!agent || agent === "UNASSIGNED") {
    return null;
  }

  return knownAgentLabels[agent] ?? agent;
}

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
