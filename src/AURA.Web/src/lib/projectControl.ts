export type ProjectStatusCode = "ACTIVE" | "PAUSED" | "BLOCKED" | "COMPLETED" | "ARCHIVED" | "UNKNOWN";
export type ProjectEventTypeCode = "CLAIM" | "PROGRESS" | "HANDOFF" | "BLOCKER" | "COMPLETION";
export type ProjectWorkStatusCode = "IN_PROGRESS" | "BLOCKED" | "COMPLETED";
export type ProjectActorTypeCode = "UNASSIGNED" | "HUMAN" | "AI_ANALYST" | "CODER";

export interface ProjectControlProject {
  projectId: number;
  projectCode: string | null;
  projectName: string;
  projectStatus: ProjectStatusCode;
  lastItemId: number | null;
  lastActivityUtc: string | null;
  lastEventType: ProjectEventTypeCode | null;
  workStatus: ProjectWorkStatusCode | null;
  lastActorType: ProjectActorTypeCode | null;
  lastActorName: string | null;
  lastModel: string | null;
  currentControlType: ProjectActorTypeCode | null;
  currentControlName: string | null;
  summary: string | null;
  nextAction: string | null;
  blocker: string | null;
  reference: string | null;
}

export interface ProjectControlBoard {
  tenantId: number;
  generatedAtUtc: string;
  projects: ProjectControlProject[];
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export async function fetchProjectControlBoard(
  tenantId: number,
  signal?: AbortSignal,
): Promise<ProjectControlBoard> {
  const response = await fetch(`${apiBaseUrl}/api/project-control?tenantId=${tenantId}`, { signal });

  if (!response.ok) {
    throw new Error(`No se pudo obtener el tablero de proyectos (HTTP ${response.status}).`);
  }

  return (await response.json()) as ProjectControlBoard;
}
