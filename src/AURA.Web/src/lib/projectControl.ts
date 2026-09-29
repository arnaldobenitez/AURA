export type ProjectStatusCode = "DRAFT" | "ACTIVE" | "PAUSED" | "BLOCKED" | "COMPLETED" | "ARCHIVED" | "UNKNOWN";
export type ProjectEventTypeCode =
  | "REQUEST"
  | "CLAIM"
  | "PROGRESS"
  | "HANDOFF"
  | "BLOCKER"
  | "APPROVAL"
  | "REWORK"
  | "COMPLETION"
  | "DEPLOYMENT";
export type ProjectWorkStatusCode = "IN_PROGRESS" | "BLOCKED" | "COMPLETED";
export type ProjectActorTypeCode = "UNASSIGNED" | "HUMAN" | "AI_ANALYST" | "CODER" | "INGESTION";
// Texto libre, sin enum cerrado: nuevos agentes/entornos (Antigravity, Windsurf, Devin,
// Codex Desktop, etc.) no deben requerir recompilar el frontend.
export type ProjectAgentCode = string;

export interface ProjectControlProject {
  projectId: number;
  projectCode: string | null;
  projectName: string;
  projectStatus: ProjectStatusCode;
  humanResponsibleId: number | null;
  humanResponsibleName: string | null;
  lastItemId: number | null;
  lastActivityUtc: string | null;
  lastEventType: ProjectEventTypeCode | null;
  workStatus: ProjectWorkStatusCode | null;
  lastActorType: ProjectActorTypeCode | null;
  lastActorName: string | null;
  lastAgent: ProjectAgentCode | null;
  lastModel: string | null;
  lastEnvironment: string | null;
  currentControlType: ProjectActorTypeCode | null;
  currentControlName: string | null;
  currentControlAgent: ProjectAgentCode | null;
  currentControlModel: string | null;
  currentControlEnvironment: string | null;
  execId: number | null;
  execDescription: string | null;
  stageId: number | null;
  currentStage: string | null;
  stageProgress: number | null;
  totalStages: number | null;
  closedStages: number | null;
  execProgress: number | null;
  summary: string | null;
  nextAction: string | null;
  blocker: string | null;
  reference: string | null;
  workStream: string | null;
}

export interface ProjectControlBoard {
  tenantId: number;
  generatedAtUtc: string;
  projects: ProjectControlProject[];
}

export interface ProjectIntervention {
  itemId: number;
  timestampUtc: string | null;
  eventType: ProjectEventTypeCode | null;
  actorType: ProjectActorTypeCode | null;
  actorName: string | null;
  agent: ProjectAgentCode | null;
  model: string | null;
  environment: string | null;
  workStatus: ProjectWorkStatusCode | null;
  execDescription: string | null;
  execId: number | null;
  stageId: number | null;
  stage: string | null;
  stageProgress: number | null;
  execProgress: number | null;
  nextControlType: ProjectActorTypeCode | null;
  nextControlName: string | null;
  summary: string | null;
  nextAction: string | null;
  blocker: string | null;
  reference: string | null;
}

export interface ProjectHistory {
  projectId: number;
  projectCode: string | null;
  projectName: string;
  interventions: ProjectIntervention[];
}

export type RoadmapStageStatus = "PENDING" | "IN_PROGRESS" | "PENDING_REVIEW" | "PASS" | "PASS_WITH_WARNINGS";

export interface ProjectRoadmapStage {
  stageId: number;
  stageCode: string | null;
  order: number;
  description: string;
  acceptanceCriteria: string | null;
  progress: number | null;
  status: RoadmapStageStatus;
  validatorCredentialId: number | null;
  validatorName: string | null;
  approvalOutcome: string | null;
  approvedAtUtc: string | null;
  approverAgent: string | null;
  approverModel: string | null;
  approvalSummary: string | null;
  evidenceReference: string | null;
}

export interface ProjectRoadmap {
  projectId: number;
  projectCode: string;
  projectName: string;
  execId: number | null;
  workStream: string | null;
  totalStages: number;
  closedStages: number;
  execProgress: number | null;
  stages: ProjectRoadmapStage[];
}

export interface CreateProjectRequest {
  tenantId: number;
  projectCode: string;
  projectName: string;
  description?: string | null;
  workingPath?: string | null;
  repository?: string | null;
  status?: ProjectStatusCode | null;
  humanResponsibleId?: number | null;
}

export type CreateProjectResponseStatus = "CREATED" | "ALREADY_EXISTS" | "CONFLICT";

export interface CreateProjectResponse {
  status: CreateProjectResponseStatus;
  projectId: number | null;
}

export interface CreateProjectValidationError {
  status: "VALIDATION_FAILED";
  errors: string[];
}

export class CreateProjectApiError extends Error {
  readonly response: CreateProjectResponse | CreateProjectValidationError;

  constructor(response: CreateProjectResponse | CreateProjectValidationError) {
    super(
      response.status === "VALIDATION_FAILED"
        ? response.errors.join(" ")
        : `No se pudo crear el proyecto (${response.status}).`,
    );
    this.response = response;
  }
}

import { authFetch } from "./auth";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export async function fetchProjectControlBoard(
  tenantId: number,
  signal?: AbortSignal,
): Promise<ProjectControlBoard> {
  const response = await authFetch(`${apiBaseUrl}/api/project-control?tenantId=${tenantId}`, { signal });

  if (!response.ok) {
    throw new Error(`No se pudo obtener el tablero de proyectos (HTTP ${response.status}).`);
  }

  return (await response.json()) as ProjectControlBoard;
}

export async function fetchProjectHistory(
  tenantId: number,
  projectCode: string,
  signal?: AbortSignal,
): Promise<ProjectHistory | null> {
  const response = await authFetch(
    `${apiBaseUrl}/api/project-control/projects/${encodeURIComponent(projectCode)}/events?tenantId=${tenantId}`,
    { signal },
  );

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`No se pudo obtener el historial del proyecto (HTTP ${response.status}).`);
  }

  return (await response.json()) as ProjectHistory;
}

export async function fetchProjectRoadmap(
  tenantId: number,
  projectCode: string,
  workStream?: string | null,
  signal?: AbortSignal,
): Promise<ProjectRoadmap> {
  const query = new URLSearchParams({ tenantId: String(tenantId) });
  if (workStream) {
    query.set("workStream", workStream);
  }
  const response = await authFetch(
    `${apiBaseUrl}/api/project-control/projects/${encodeURIComponent(projectCode)}/roadmap?${query}`,
    { signal },
  );

  if (!response.ok) {
    throw new Error(`No se pudo obtener el Roadmap del proyecto (HTTP ${response.status}).`);
  }

  return (await response.json()) as ProjectRoadmap;
}

export async function createProject(
  request: CreateProjectRequest,
  signal?: AbortSignal,
): Promise<CreateProjectResponse> {
  const response = await authFetch(`${apiBaseUrl}/api/project-control/projects`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });

  // 200/201/409/400 traen todos un body util (status + projectId/errors); cualquier
  // otra cosa (500, red caida) no tiene una forma conocida, ahi si es un error generico.
  if (response.ok || response.status === 409 || response.status === 400) {
    const body = (await response.json()) as CreateProjectResponse | CreateProjectValidationError;
    if (body.status === "VALIDATION_FAILED" || body.status === "CONFLICT") {
      throw new CreateProjectApiError(body);
    }
    return body;
  }

  throw new Error(`No se pudo crear el proyecto (HTTP ${response.status}).`);
}
